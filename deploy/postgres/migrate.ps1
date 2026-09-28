#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TASK-FND-09: Applies EF Core migrations to the compose Postgres instance
    and ensures the pgvector extension is enabled.

.DESCRIPTION
    Prefers an explicit, developer-invoked script over auto-migrate-on-startup
    so schema changes never surprise anyone running the stack (Technical
    Approach in tasks.md). Steps:
      1. Load .env (repo root) for Postgres credentials/port, if present.
      2. Enable the pgvector extension via `docker compose exec postgres psql`
         (works regardless of whether the "diagnostics" profile is up, since
         it runs inside the postgres container rather than over the host
         network) - CREATE EXTENSION IF NOT EXISTS is idempotent.
      3. Run `dotnet ef database update` from the host against
         localhost:$POSTGRES_HOST_PORT, which requires the "diagnostics"
         Compose profile to be active (see docker-compose.override.yml).

    Safe to re-run: pgvector enablement is idempotent, and EF Core migrations
    are a no-op once applied.

.NOTES
    Until TASK-PG-01 adds the Infrastructure DbContext + initial migration,
    step 3 has nothing to apply yet; the script detects this and exits
    cleanly with an informational message instead of failing the bootstrap
    sequence.
#>
[CmdletBinding()]
param(
    [string]$InfrastructureProject = "src/infrastructure/EnterpriseKnowledgeAgent.Infrastructure.csproj",
    [string]$StartupProject = "src/api/EnterpriseKnowledgeAgent.Api.csproj"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Push-Location $repoRoot
try {
    # --- Step 1: load .env (mirrors docker compose's own auto-load behavior) ---
    $envFile = Join-Path $repoRoot ".env"
    if (Test-Path $envFile) {
        Write-Host "[migrate] Loading $envFile ..."
        Get-Content $envFile | ForEach-Object {
            $line = $_.Trim()
            if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
                $key, $value = $line -split "=", 2
                $key = $key.Trim()
                if (-not (Test-Path "env:$key")) {
                    Set-Item -Path "env:$key" -Value $value.Trim()
                }
            }
        }
    } else {
        Write-Host "[migrate] No .env found; using docker-compose.yml defaults."
    }

    $pgUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "eka" }
    $pgPassword = if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { "eka-local-dev-password" }
    $pgDb = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { "enterprise_knowledge_agent" }
    $pgHostPort = if ($env:POSTGRES_HOST_PORT) { $env:POSTGRES_HOST_PORT } else { "5432" }

    # --- Step 2: enable pgvector (idempotent; runs inside the container, no
    #     host port required) ---
    Write-Host "[migrate] Ensuring the postgres container is up..."
    docker compose up -d postgres | Out-Null

    Write-Host "[migrate] Enabling pgvector extension (CREATE EXTENSION IF NOT EXISTS vector)..."
    docker compose exec -T postgres psql -U $pgUser -d $pgDb -c "CREATE EXTENSION IF NOT EXISTS vector;"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enable pgvector extension (exit code $LASTEXITCODE)."
    }

    # --- Step 3: apply EF Core migrations from the host ---
    # Detect actual EF Core capability (not just file existence): TASK-PG-01
    # adds the DbContext + a Microsoft.EntityFrameworkCore.Design reference.
    # Until then, the .csproj already exists (scaffolded in TASK-FND-01) but
    # has nothing for `dotnet ef` to discover, which would otherwise surface
    # as a confusing raw CLI error instead of a clear "not implemented yet".
    $infrastructureProjectPath = Join-Path $repoRoot $InfrastructureProject
    $hasEfDesign = (Test-Path $infrastructureProjectPath) -and
        ((Get-Content $infrastructureProjectPath -Raw) -match "Microsoft\.EntityFrameworkCore\.Design")
    if (-not $hasEfDesign) {
        Write-Host "[migrate] No EF Core DbContext found yet in '$InfrastructureProject' (TASK-PG-01 not implemented in this checkout)."
        Write-Host "[migrate] pgvector is enabled; nothing else to migrate yet. Re-run this script once TASK-PG-01 lands."
        exit 0
    }

    $efInstalled = (dotnet tool list -g | Select-String "dotnet-ef") -ne $null
    if (-not $efInstalled) {
        Write-Host "[migrate] Installing the dotnet-ef global tool..."
        dotnet tool install --global dotnet-ef
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install dotnet-ef (exit code $LASTEXITCODE)."
        }
    }

    Write-Host "[migrate] This script applies migrations against Postgres on localhost:$pgHostPort,"
    Write-Host "[migrate] which requires the 'diagnostics' Compose profile to publish that port:"
    Write-Host "[migrate]   docker compose --profile diagnostics up -d postgres-diagnostics"

    # ASP.NET Core's configuration system binds ConnectionStrings:Postgres from
    # this env var automatically (double-underscore = ":" section separator),
    # so `dotnet ef` (which builds the app's host to discover the DbContext)
    # picks it up without needing a design-time-factory-specific CLI flag.
    $env:ConnectionStrings__Postgres = "Host=localhost;Port=$pgHostPort;Database=$pgDb;Username=$pgUser;Password=$pgPassword"

    Write-Host "[migrate] Running: dotnet ef database update --project $InfrastructureProject --startup-project $StartupProject"
    dotnet ef database update --project $InfrastructureProject --startup-project $StartupProject
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update failed (exit code $LASTEXITCODE)."
    }

    Write-Host "[migrate] Done."
}
finally {
    Pop-Location
}
