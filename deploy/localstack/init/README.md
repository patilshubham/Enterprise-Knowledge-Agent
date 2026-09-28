# LocalStack init scripts

`10-create-buckets-and-queues.sh` (TASK-FND-08) is an idempotent `awslocal`
script that creates the S3 buckets (`rag-source-files`, `rag-generated-exports`)
and SQS queues (`document-processing`, `agent-execution`, `deletion-jobs`,
each with a `-dlq` and a `maxReceiveCount=5` redrive policy) that this stack
depends on. Bucket/queue names are read from the `S3_*`/`SQS_*` environment
variables (see `.env.example`, wired into the `localstack` service in
`docker-compose.yml`) so app config and this script never drift.

The script tolerates re-runs (checks for existing buckets/queues before
creating), but LocalStack itself only fires `ready.d` hooks once per fresh
container/volume. To force re-seeding, recreate the LocalStack volume:

```powershell
docker compose down -v
docker compose up -d
```

Smoke-check after startup:

```powershell
docker compose exec localstack awslocal s3 ls
docker compose exec localstack awslocal sqs list-queues
```
