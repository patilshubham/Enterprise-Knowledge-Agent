#!/usr/bin/env bash
# TASK-FND-09: Applies EF Core migrations to the compose Postgres instance
# and ensures the pgvector extension is enabled. Bash twin of migrate.ps1 -
# see that file's header comment for the full step-by-step rationale.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

INFRASTRUCTURE_PROJECT="${1:-src/infrastructure/EnterpriseKnowledgeAgent.Infrastructure.csproj}"
STARTUP_PROJECT="${2:-src/api/EnterpriseKnowledgeAgent.Api.csproj}"

# --- Step 1: load .env (mirrors docker compose's own auto-load behavior) ---
if [ -f .env ]; then
  echo "[migrate] Loading .env ..."
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
else
  echo "[migrate] No .env found; using docker-compose.yml defaults."
fi

POSTGRES_USER="${POSTGRES_USER:-eka}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-eka-local-dev-password}"
POSTGRES_DB="${POSTGRES_DB:-enterprise_knowledge_agent}"
POSTGRES_HOST_PORT="${POSTGRES_HOST_PORT:-5432}"

# --- Step 2: enable pgvector (idempotent; runs inside the container, no
#     host port required) ---
echo "[migrate] Ensuring the postgres container is up..."
docker compose up -d postgres >/dev/null

echo "[migrate] Enabling pgvector extension (CREATE EXTENSION IF NOT EXISTS vector)..."
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "CREATE EXTENSION IF NOT EXISTS vector;"

# --- Step 3: apply EF Core migrations from the host ---
# Detect actual EF Core capability (not just file existence): TASK-PG-01 adds
# the DbContext + a Microsoft.EntityFrameworkCore.Design reference. Until
# then, the .csproj already exists (scaffolded in TASK-FND-01) but has
# nothing for `dotnet ef` to discover, which would otherwise surface as a
# confusing raw CLI error instead of a clear "not implemented yet".
if [ ! -f "$INFRASTRUCTURE_PROJECT" ] || ! grep -q "Microsoft.EntityFrameworkCore.Design" "$INFRASTRUCTURE_PROJECT"; then
  echo "[migrate] No EF Core DbContext found yet in '$INFRASTRUCTURE_PROJECT' (TASK-PG-01 not implemented in this checkout)."
  echo "[migrate] pgvector is enabled; nothing else to migrate yet. Re-run this script once TASK-PG-01 lands."
  exit 0
fi

if ! dotnet tool list -g | grep -q dotnet-ef; then
  echo "[migrate] Installing the dotnet-ef global tool..."
  dotnet tool install --global dotnet-ef
fi

echo "[migrate] This script applies migrations against Postgres on localhost:$POSTGRES_HOST_PORT,"
echo "[migrate] which requires the 'diagnostics' Compose profile to publish that port:"
echo "[migrate]   docker compose --profile diagnostics up -d postgres-diagnostics"

# ASP.NET Core's configuration system binds ConnectionStrings:Postgres from
# this env var automatically (double-underscore = ":" section separator), so
# `dotnet ef` (which builds the app's host to discover the DbContext) picks
# it up without needing a design-time-factory-specific CLI flag.
export ConnectionStrings__Postgres="Host=localhost;Port=${POSTGRES_HOST_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

echo "[migrate] Running: dotnet ef database update --project $INFRASTRUCTURE_PROJECT --startup-project $STARTUP_PROJECT"
dotnet ef database update --project "$INFRASTRUCTURE_PROJECT" --startup-project "$STARTUP_PROJECT"

echo "[migrate] Done."
