#!/usr/bin/env bash
# TASK-FND-08: idempotent LocalStack bootstrap (ASDD §11.1 buckets, §11.3 queues).
#
# Runs once per fresh LocalStack container via the /etc/localstack/init/ready.d
# hook (see docker-compose.yml's localstack volume mount). Safe to re-run: every
# command below tolerates "already exists" so a manual re-invocation (or a
# LocalStack restart that somehow re-fires ready.d) never fails the container.
#
# To force a full re-seed, wipe the named volume and recreate the stack:
#   docker compose down -v && docker compose up -d
set -euo pipefail

echo "[localstack-init] Creating S3 buckets..."

# Bucket names come from .env.example (S3_SOURCE_FILES_BUCKET /
# S3_GENERATED_EXPORTS_BUCKET) so app config and this script stay in sync.
SOURCE_FILES_BUCKET="${S3_SOURCE_FILES_BUCKET:-rag-source-files}"
GENERATED_EXPORTS_BUCKET="${S3_GENERATED_EXPORTS_BUCKET:-rag-generated-exports}"

create_bucket() {
  local bucket="$1"
  if awslocal s3api head-bucket --bucket "$bucket" >/dev/null 2>&1; then
    echo "[localstack-init]   bucket '$bucket' already exists, skipping."
  else
    # Private by default (no --acl public-read); server-side key enforcement
    # is applied at the app layer in module 7.
    awslocal s3 mb "s3://$bucket"
    echo "[localstack-init]   created bucket '$bucket'."
  fi
}

create_bucket "$SOURCE_FILES_BUCKET"
create_bucket "$GENERATED_EXPORTS_BUCKET"

echo "[localstack-init] Creating SQS queues (with DLQ + redrive policy)..."

# Queue base names come from .env.example (SQS_*_QUEUE) so app config and
# this script stay in sync.
DOCUMENT_PROCESSING_QUEUE="${SQS_DOCUMENT_PROCESSING_QUEUE:-document-processing}"
AGENT_EXECUTION_QUEUE="${SQS_AGENT_EXECUTION_QUEUE:-agent-execution}"
DELETION_JOBS_QUEUE="${SQS_DELETION_JOBS_QUEUE:-deletion-jobs}"

# Creates (or finds the existing) $1-dlq, then creates (or finds the
# existing) $1 wired to it via RedrivePolicy (maxReceiveCount=$2).
create_queue_with_dlq() {
  local base_name="$1"
  local max_receive_count="$2"
  local dlq_name="${base_name}-dlq"

  local dlq_url
  dlq_url=$(awslocal sqs create-queue --queue-name "$dlq_name" --query 'QueueUrl' --output text)
  echo "[localstack-init]   queue '$dlq_name' ready at $dlq_url"

  local dlq_arn
  dlq_arn=$(awslocal sqs get-queue-attributes \
    --queue-url "$dlq_url" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' --output text)

  # The --attributes value is itself a JSON object; RedrivePolicy's value
  # must be a JSON-*encoded string* nested inside it, so the inner quotes
  # need escaping. Building this with printf (rather than a shorthand
  # `Key=Value` string) avoids ambiguity with the AWS CLI's shorthand parser.
  local redrive_policy_json
  redrive_policy_json=$(printf '{\\"deadLetterTargetArn\\":\\"%s\\",\\"maxReceiveCount\\":\\"%s\\"}' "$dlq_arn" "$max_receive_count")
  local attributes_json
  attributes_json=$(printf '{"RedrivePolicy":"%s"}' "$redrive_policy_json")

  local main_url
  main_url=$(awslocal sqs create-queue \
    --queue-name "$base_name" \
    --attributes "$attributes_json" \
    --query 'QueueUrl' --output text)
  echo "[localstack-init]   queue '$base_name' ready at $main_url (redrive -> '$dlq_name', maxReceiveCount=$max_receive_count)"
}

# maxReceiveCount=5 for all three: generous enough to survive transient
# dependency blips (SQS-04 finalizes the retry/backoff classification) while
# still routing genuinely poison messages to the DLQ instead of looping
# forever.
create_queue_with_dlq "$DOCUMENT_PROCESSING_QUEUE" 5
create_queue_with_dlq "$AGENT_EXECUTION_QUEUE" 5
create_queue_with_dlq "$DELETION_JOBS_QUEUE" 5

echo "[localstack-init] Done. Buckets:"
awslocal s3 ls
echo "[localstack-init] Queues:"
awslocal sqs list-queues
