#!/usr/bin/env bash
# TASK-PG-05: Bash twin of restore.ps1 - see that file's header for rationale.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

BACKUP_FILE="${1:?Usage: restore.sh <backup-file> [--force]}"
FORCE="${2:-}"

if [ ! -f "$BACKUP_FILE" ]; then
  echo "Backup file not found: $BACKUP_FILE" >&2
  exit 1
fi

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

POSTGRES_USER="${POSTGRES_USER:-eka}"
POSTGRES_DB="${POSTGRES_DB:-enterprise_knowledge_agent}"

if [ "$FORCE" != "--force" ]; then
  read -r -p "This will DROP AND RECREATE objects in database '$POSTGRES_DB' from '$BACKUP_FILE'. Type 'yes' to continue: " CONFIRM
  if [ "$CONFIRM" != "yes" ]; then
    echo "[restore] Aborted."
    exit 1
  fi
fi

echo "[restore] Ensuring the postgres container is up..."
docker compose up -d postgres >/dev/null

CONTAINER_ID="$(docker compose ps -q postgres)"
CONTAINER_PATH="/tmp/restore.dump"

echo "[restore] Copying '$BACKUP_FILE' into the postgres container..."
docker cp "$BACKUP_FILE" "${CONTAINER_ID}:${CONTAINER_PATH}"

echo "[restore] Running pg_restore (--clean --if-exists) into database '$POSTGRES_DB'..."
set +e
docker compose exec -T postgres pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner "$CONTAINER_PATH"
RESTORE_EXIT_CODE=$?
set -e

docker compose exec -T postgres rm -f "$CONTAINER_PATH" >/dev/null

if [ "$RESTORE_EXIT_CODE" -ne 0 ]; then
  echo "[restore] pg_restore exited with code $RESTORE_EXIT_CODE - review the output above for real errors vs. benign 'does not exist' warnings." >&2
fi

echo "[restore] Done."
