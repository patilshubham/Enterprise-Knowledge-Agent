#!/usr/bin/env bash
# TASK-FND-10 (partial): Validates that the configured Azure OpenAI
# resource/deployments are reachable, without consuming meaningful quota.
# Bash twin of check-aoai-connectivity.ps1 - see that file's header comment
# for the full two-stage rationale (ARM sanity check + data-plane deployment
# reachability, entra token via `az account get-access-token` or api-key).
#
# Never exits on the first failure - reports a PASS/FAIL summary for every
# check, then exits non-zero only at the end if anything failed.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

AZURE_OPENAI_AUTH_MODE="${AZURE_OPENAI_AUTH_MODE:-entra}"
ANY_FAILED=0
declare -A RESULTS

if [ -z "${AZURE_OPENAI_ENDPOINT:-}" ] || [[ "$AZURE_OPENAI_ENDPOINT" == *"<your-resource>"* ]]; then
  echo "[aoai-check] AZURE_OPENAI_ENDPOINT is not set in .env - copy .env.example to .env and fill it in."
  exit 1
fi

# --- Stage 1 (optional): ARM-level resource/model sanity check ---
if [ -n "${AZURE_OPENAI_RESOURCE_NAME:-}" ] && [ -n "${AZURE_OPENAI_RESOURCE_GROUP:-}" ]; then
  echo "[aoai-check] Checking ARM resource '$AZURE_OPENAI_RESOURCE_NAME' (rg '$AZURE_OPENAI_RESOURCE_GROUP') via az CLI..."
  if models_json=$(az cognitiveservices account list-models --name "$AZURE_OPENAI_RESOURCE_NAME" --resource-group "$AZURE_OPENAI_RESOURCE_GROUP" -o json 2>/dev/null) && [ -n "$models_json" ]; then
    model_count=$(echo "$models_json" | grep -c '"model"' || true)
    echo "[aoai-check]   PASS - resource reachable ($model_count model entries)."
    RESULTS["arm-resource"]=1
  else
    echo "[aoai-check]   FAIL - could not list models. If this is an interactive-auth/MFA error, run 'az login' and retry."
    RESULTS["arm-resource"]=0
    ANY_FAILED=1
  fi
else
  echo "[aoai-check] Skipping ARM sanity check (AZURE_OPENAI_RESOURCE_NAME / AZURE_OPENAI_RESOURCE_GROUP not set)."
fi

# --- Stage 2: data-plane deployment reachability ---
echo "[aoai-check] Checking data-plane deployments at $AZURE_OPENAI_ENDPOINT (auth mode: $AZURE_OPENAI_AUTH_MODE)..."

auth_header=()
if [ "$AZURE_OPENAI_AUTH_MODE" = "apikey" ]; then
  if [ -z "${AZURE_OPENAI_API_KEY:-}" ]; then
    echo "[aoai-check]   FAIL - AZURE_OPENAI_AUTH_MODE=apikey but AZURE_OPENAI_API_KEY is not set."
    RESULTS["data-plane-reachable"]=0
    ANY_FAILED=1
  else
    auth_header=(-H "api-key: ${AZURE_OPENAI_API_KEY}")
  fi
else
  # Entra ID: short-lived (~1 hour) bearer token from the current `az login`
  # session - never persisted, requested fresh each run.
  if token=$(az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv 2>/dev/null) && [ -n "$token" ]; then
    auth_header=(-H "Authorization: Bearer ${token}")
  else
    echo "[aoai-check]   FAIL - az account get-access-token failed - run 'az login' (with MFA if prompted) and retry."
    RESULTS["data-plane-reachable"]=0
    ANY_FAILED=1
  fi
fi

if [ "${#auth_header[@]}" -gt 0 ]; then
  deployments_url="${AZURE_OPENAI_ENDPOINT%/}/openai/deployments?api-version=2024-10-21"
  if response=$(curl -sf "${auth_header[@]}" "$deployments_url"); then
    deployed_names=$(echo "$response" | grep -o '"id":"[^"]*"' | sed 's/"id":"//;s/"//')
    echo "[aoai-check]   PASS - data plane reachable. Deployments found: $(echo "$deployed_names" | tr '\n' ',' | sed 's/,$//')"
    RESULTS["data-plane-reachable"]=1

    for pair in "chat:${AZURE_OPENAI_CHAT_DEPLOYMENT:-}" "embedding:${AZURE_OPENAI_EMBEDDING_DEPLOYMENT:-}"; do
      kind="${pair%%:*}"
      name="${pair#*:}"
      if [ -z "$name" ] || [[ "$name" == "<your-"* ]]; then
        echo "[aoai-check]   SKIP - AZURE_OPENAI_${kind^^}_DEPLOYMENT not set."
        continue
      fi
      if echo "$deployed_names" | grep -qx "$name"; then
        echo "[aoai-check]   PASS - $kind deployment '$name' exists."
        RESULTS["$kind-deployment"]=1
      else
        echo "[aoai-check]   FAIL - $kind deployment '$name' not found among deployments."
        RESULTS["$kind-deployment"]=0
        ANY_FAILED=1
      fi
    done
  else
    echo "[aoai-check]   FAIL - could not reach data-plane deployments endpoint."
    RESULTS["data-plane-reachable"]=0
    ANY_FAILED=1
  fi
fi

echo ""
echo "[aoai-check] Summary:"
for key in "${!RESULTS[@]}"; do
  if [ "${RESULTS[$key]}" = "1" ]; then status="PASS"; else status="FAIL"; fi
  printf "  %-24s %s\n" "$key" "$status"
done

exit $ANY_FAILED
