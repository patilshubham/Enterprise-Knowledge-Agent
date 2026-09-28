#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TASK-PG-05: Restores a pg_dump custom-format backup (produced by
    backup.ps1) into the compose Postgres database.

.DESCRIPTION
    Copies the dump file into the postgres container (docker compose cp) and
    runs `pg_restore` there. Uses `--clean --if-exists` so restoring into a
    non-empty database (e.g. re-running against the same dev instance) drops
    existing objects first instead of failing on "already exists" errors -
    this is a destructive operation by design (restoring IS overwriting), so
    it prompts for confirmation unless -Force is passed.

.EXAMPLE
    ./deploy/postgres/restore.ps1 -BackupFile "deploy/postgres/backups/enterprise_knowledge_agent-20250101-120000.dump"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Push-Location $repoRoot
try {
    if (-not (Test-Path $BackupFile)) {
        throw "Backup file not found: $BackupFile"
    }

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

    if (-not $Force) {
        $confirmation = Read-Host "This will DROP AND RECREATE objects in database '$pgDb' from '$BackupFile'. Type 'yes' to continue"
        if ($confirmation -ne "yes") {
            Write-Host "[restore] Aborted."
            exit 1
        }
    }

    Write-Host "[restore] Ensuring the postgres container is up..."
    docker compose up -d postgres | Out-Null

    $containerPath = "/tmp/restore.dump"
    $containerId = (docker compose ps -q postgres)
    if (-not $containerId) {
        throw "Could not resolve the postgres container id."
    }

    Write-Host "[restore] Copying '$BackupFile' into the postgres container..."
    docker cp $BackupFile "${containerId}:${containerPath}"
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed (exit code $LASTEXITCODE)."
    }

    Write-Host "[restore] Running pg_restore (--clean --if-exists) into database '$pgDb'..."
    docker compose exec -T postgres pg_restore -U $pgUser -d $pgDb --clean --if-exists --no-owner $containerPath
    $restoreExitCode = $LASTEXITCODE

    docker compose exec -T postgres rm -f $containerPath | Out-Null

    # pg_restore can exit non-zero on benign warnings (e.g. missing roles
    # from a dump taken against a different environment) - surface output
    # above but don't fail the script purely on a non-zero exit here, since
    # -1 exit combined with --clean --if-exists commonly means "some objects
    # didn't exist to drop", which is expected on a fresh database.
    if ($restoreExitCode -ne 0) {
        Write-Warning "[restore] pg_restore exited with code $restoreExitCode - review the output above for real errors vs. benign 'does not exist' warnings."
    }

    Write-Host "[restore] Done."
}
finally {
    Pop-Location
}
