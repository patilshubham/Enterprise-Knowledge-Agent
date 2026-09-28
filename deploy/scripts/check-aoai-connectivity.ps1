#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TASK-FND-10 (partial): Validates that the configured Azure OpenAI
    resource/deployments are reachable, without consuming meaningful quota.

.DESCRIPTION
    This is a standalone connectivity check that does NOT depend on the C#
    Azure OpenAI adapters (module 12 AOI, phase P5) - it talks to Azure
    directly via the `az` CLI + the Azure OpenAI REST API, so it can be used
    from day one of the Foundation phase to validate `.env` configuration.
    Once module 12 lands, `TASK-AOI-05` (availability checks) supersedes this
    for in-app health reporting; this script remains useful as a quick,
    dependency-free manual/CI check.

    Two-stage check:
      1. (Optional, entra-mode friendly) ARM-level sanity check via
         `az cognitiveservices account list-models --name ... --resource-group ...`
         - confirms the resource exists, is reachable, and lists which base
         models are available in it. Skipped if AZURE_OPENAI_RESOURCE_NAME /
         AZURE_OPENAI_RESOURCE_GROUP are not set in `.env`.
      2. Data-plane check: lists the resource's *deployments* (a lightweight
         metadata call - it does not consume generation/embedding quota) and
         confirms the configured chat + embedding deployment names exist.
         Auth is either:
           - "entra" (preferred): `az account get-access-token --resource
             https://cognitiveservices.azure.com` - yields a bearer token
             valid ~1 hour, used as `Authorization: Bearer <token>`.
           - "apikey" (fallback): `api-key: <AZURE_OPENAI_API_KEY>` header.

    Never throws on unreachability - reports a structured PASS/FAIL summary
    per check and exits non-zero only at the very end if anything failed, so
    callers (CI, README bootstrap steps) can branch on exit code without a
    stack trace.

.NOTES
    Requires `az login` (interactive, MFA-aware) to have been run already;
    this script only reads the current CLI session's token, it never
    performs the login itself.
#>
[CmdletBinding()]
param()

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

    $endpoint = $env:AZURE_OPENAI_ENDPOINT
    $authMode = if ($env:AZURE_OPENAI_AUTH_MODE) { $env:AZURE_OPENAI_AUTH_MODE } else { "entra" }
    $chatDeployment = $env:AZURE_OPENAI_CHAT_DEPLOYMENT
    $embeddingDeployment = $env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT
    $resourceName = $env:AZURE_OPENAI_RESOURCE_NAME
    $resourceGroup = $env:AZURE_OPENAI_RESOURCE_GROUP

    $results = [ordered]@{}

    if (-not $endpoint -or $endpoint -like "*<your-resource>*") {
        Write-Host "[aoai-check] AZURE_OPENAI_ENDPOINT is not set in .env - copy .env.example to .env and fill it in." -ForegroundColor Yellow
        exit 1
    }

    # --- Stage 1 (optional): ARM-level resource/model sanity check ---
    if ($resourceName -and $resourceGroup) {
        Write-Host "[aoai-check] Checking ARM resource '$resourceName' (rg '$resourceGroup') via az CLI..."
        try {
            $modelsJson = az cognitiveservices account list-models --name $resourceName --resource-group $resourceGroup -o json 2>$null
            if ($LASTEXITCODE -ne 0 -or -not $modelsJson) {
                throw "az cognitiveservices account list-models returned no data (exit code $LASTEXITCODE)."
            }
            $models = $modelsJson | ConvertFrom-Json
            Write-Host "[aoai-check]   PASS - resource reachable, $($models.Count) base model(s) available." -ForegroundColor Green
            $results["arm-resource"] = $true
        } catch {
            Write-Host "[aoai-check]   FAIL - $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "[aoai-check]   If this is an interactive-auth/MFA error, run 'az login' and retry." -ForegroundColor Yellow
            $results["arm-resource"] = $false
        }
    } else {
        Write-Host "[aoai-check] Skipping ARM sanity check (AZURE_OPENAI_RESOURCE_NAME / AZURE_OPENAI_RESOURCE_GROUP not set)."
    }

    # --- Stage 2: data-plane deployment reachability ---
    Write-Host "[aoai-check] Checking data-plane deployments at $endpoint (auth mode: $authMode)..."
    try {
        $headers = @{}
        if ($authMode -eq "apikey") {
            if (-not $env:AZURE_OPENAI_API_KEY) {
                throw "AZURE_OPENAI_AUTH_MODE=apikey but AZURE_OPENAI_API_KEY is not set (see .env / dotnet user-secrets)."
            }
            $headers["api-key"] = $env:AZURE_OPENAI_API_KEY
        } else {
            # Entra ID: short-lived (~1 hour) bearer token from the current
            # `az login` session - never persisted, requested fresh each run.
            $token = (az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv 2>$null)
            if ($LASTEXITCODE -ne 0 -or -not $token) {
                throw "az account get-access-token failed - run 'az login' (with MFA if prompted) and retry."
            }
            $headers["Authorization"] = "Bearer $token"
        }

        $deploymentsUrl = "$($endpoint.TrimEnd('/'))/openai/deployments?api-version=2024-10-21"
        $response = Invoke-RestMethod -Uri $deploymentsUrl -Headers $headers -Method Get -TimeoutSec 30
        $deployedNames = $response.data | ForEach-Object { $_.id }

        Write-Host "[aoai-check]   PASS - data plane reachable. Deployments found: $($deployedNames -join ', ')" -ForegroundColor Green
        $results["data-plane-reachable"] = $true

        foreach ($pair in @(@("chat", $chatDeployment), @("embedding", $embeddingDeployment))) {
            $kind, $name = $pair
            if (-not $name -or $name -like "<your-*") {
                Write-Host "[aoai-check]   SKIP - AZURE_OPENAI_$($kind.ToUpper())_DEPLOYMENT not set." -ForegroundColor Yellow
                continue
            }
            if ($deployedNames -contains $name) {
                Write-Host "[aoai-check]   PASS - $kind deployment '$name' exists." -ForegroundColor Green
                $results["$kind-deployment"] = $true
            } else {
                Write-Host "[aoai-check]   FAIL - $kind deployment '$name' not found among deployments." -ForegroundColor Red
                $results["$kind-deployment"] = $false
            }
        }
    } catch {
        Write-Host "[aoai-check]   FAIL - $($_.Exception.Message)" -ForegroundColor Red
        $results["data-plane-reachable"] = $false
    }

    Write-Host ""
    Write-Host "[aoai-check] Summary:"
    $anyFailed = $false
    foreach ($key in $results.Keys) {
        if ($results[$key]) {
            $status = "PASS"
        } else {
            $status = "FAIL"
            $anyFailed = $true
        }
        Write-Host ("  {0,-24} {1}" -f $key, $status)
    }

    if ($anyFailed) {
        exit 1
    }
    exit 0
}
finally {
    Pop-Location
}
