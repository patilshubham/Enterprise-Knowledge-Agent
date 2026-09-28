#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TASK-PG-05: Backs up the compose Postgres database to a local file using
    pg_dump's custom format (compressed, supports parallel/selective restore).

.DESCRIPTION
    Runs `pg_dump` INSIDE the postgres container (via `docker compose exec`),
    identically to how migrate.ps1 runs `psql` - works whether or not the
    "diagnostics" profile has published Postgres's port to the host. The
    dump is streamed back to the host and written under deploy/postgres/backups/
    (git-ignored) with a timestamped filename so repeated backups don't
    overwrite each other.

.EXAMPLE
    ./deploy/postgres/backup.ps1
    ./deploy/postgres/backup.ps1 -OutputDirectory "D:\backups"
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = "deploy/postgres/backups"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Push-Location $repoRoot
try {
    $envFile = Join-Path $repoRoot ".env"
    if (Test-Path $envFile) {
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
    }

    $pgUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "eka" }
    $pgDb = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { "enterprise_knowledge_agent" }

    Write-Host "[backup] Ensuring the postgres container is up..."
    docker compose up -d postgres | Out-Null

    if (-not (Test-Path $OutputDirectory)) {
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $fileName = "${pgDb}-${timestamp}.dump"
    $outputPath = Join-Path $OutputDirectory $fileName

    Write-Host "[backup] Running pg_dump (custom format) for database '$pgDb'..."
    # `-Fc` (custom format) is compressed and restorable with pg_restore
    # (including selective/parallel restore); safe to redirect binary output
    # from `docker compose exec -T` directly to a file.
    docker compose exec -T postgres pg_dump -U $pgUser -d $pgDb -Fc > $outputPath
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -Path $outputPath -ErrorAction SilentlyContinue
        throw "pg_dump failed (exit code $LASTEXITCODE)."
    }

    $sizeKb = [Math]::Round((Get-Item $outputPath).Length / 1KB, 1)
    Write-Host "[backup] Done: $outputPath ($sizeKb KB)"
}
finally {
    Pop-Location
}
