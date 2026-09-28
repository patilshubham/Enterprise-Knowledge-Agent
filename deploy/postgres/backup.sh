#!/usr/bin/env bash
# TASK-PG-05: Bash twin of backup.ps1 - see that file's header for rationale.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

OUTPUT_DIR="${1:-deploy/postgres/backups}"

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

POSTGRES_USER="${POSTGRES_USER:-eka}"
POSTGRES_DB="${POSTGRES_DB:-enterprise_knowledge_agent}"

echo "[backup] Ensuring the postgres container is up..."
docker compose up -d postgres >/dev/null

mkdir -p "$OUTPUT_DIR"

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_PATH="$OUTPUT_DIR/${POSTGRES_DB}-${TIMESTAMP}.dump"

echo "[backup] Running pg_dump (custom format) for database '$POSTGRES_DB'..."
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc > "$OUTPUT_PATH"

SIZE_KB=$(( $(stat -c%s "$OUTPUT_PATH" 2>/dev/null || stat -f%z "$OUTPUT_PATH") / 1024 ))
echo "[backup] Done: $OUTPUT_PATH (${SIZE_KB} KB)"
