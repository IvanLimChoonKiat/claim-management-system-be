#!/usr/bin/env bash
# Repeatable functional verification for the Claims Management backend - cross-platform
# companion to scripts/verify.ps1 (the primary script in this environment). Build, test, then a
# real REST lifecycle smoke test against a running API instance on an isolated SQLite database.
#
# Usage: ./scripts/verify.sh [--with-kafka]

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

WITH_KAFKA=false
[[ "${1:-}" == "--with-kafka" ]] && WITH_KAFKA=true

FAILURES=0
PORT=5277
BASE_URL="http://localhost:${PORT}"
TEST_DB="$(mktemp -u /tmp/claims-verify-XXXXXX.db)"
API_PID=""

pass() { echo -e "\033[32m[PASS]\033[0m $1"; }
fail() { echo -e "\033[31m[FAIL]\033[0m $1"; FAILURES=$((FAILURES + 1)); }
check() { if [[ "$2" == "true" || "$2" == "0" ]]; then pass "$1"; else fail "$1 $3"; fi; }

cleanup() {
    [[ -n "$API_PID" ]] && kill "$API_PID" >/dev/null 2>&1
    rm -f "$TEST_DB"
}
trap cleanup EXIT

echo "=== 1. Environment prerequisite checks ==="
DOTNET_VERSION="$(dotnet --version 2>/dev/null)"
check "dotnet SDK available ($DOTNET_VERSION)" "$([[ -n "$DOTNET_VERSION" ]] && echo true || echo false)"

echo -e "\n=== 2. Backend dependency restoration ==="
dotnet restore ClaimsManagement.sln >/dev/null 2>&1
check "dotnet restore" "$([[ $? -eq 0 ]] && echo true || echo false)"

echo -e "\n=== 3. Backend build (Release) ==="
dotnet build ClaimsManagement.sln --configuration Release --no-restore >/dev/null 2>&1
check "dotnet build" "$([[ $? -eq 0 ]] && echo true || echo false)"

echo -e "\n=== 4. Unit and integration tests ==="
dotnet test ClaimsManagement.sln --configuration Release --no-build >/dev/null 2>&1
check "dotnet test (unit + API integration)" "$([[ $? -eq 0 ]] && echo true || echo false)"

echo -e "\n=== 5. SQLite test database initialization ==="
export ConnectionStrings__ClaimsDatabase="Data Source=${TEST_DB}"
export Jwt__SigningKey="VERIFY-SCRIPT-ONLY-SIGNING-KEY-NOT-FOR-PRODUCTION-USE"
export Jwt__ExpiryHours="1"
export Kafka__Enabled="$WITH_KAFKA"
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="$BASE_URL"

dotnet run --no-build --configuration Release --project src/ClaimsManagement.Api --no-launch-profile >/tmp/verify-api.log 2>&1 &
API_PID=$!

READY=false
for _ in $(seq 1 30); do
    sleep 1
    if curl -sf "${BASE_URL}/swagger/v1/swagger.json" >/dev/null 2>&1; then
        READY=true
        break
    fi
done
check "SQLite database initialized (migrations applied on startup)" "$READY" "(API did not become ready - see /tmp/verify-api.log)"

echo -e "\n=== 6. API health check ==="
check "API responds on $BASE_URL" "$READY"

echo -e "\n=== 7-9. Claim submission, retrieval, assignment (REST) ==="
RAND="$(date +%s%N)"
CLAIMANT_EMAIL="verify-claimant-${RAND}@example.com"
OFFICER_EMAIL="verify-officer-${RAND}@example.com"

CLAIMANT_RESP="$(curl -s -X POST "${BASE_URL}/api/auth/dev-login" -H "Content-Type: application/json" \
    -d "{\"email\":\"${CLAIMANT_EMAIL}\",\"displayName\":\"Verify Claimant\",\"role\":\"Claimant\"}")"
OFFICER_RESP="$(curl -s -X POST "${BASE_URL}/api/auth/dev-login" -H "Content-Type: application/json" \
    -d "{\"email\":\"${OFFICER_EMAIL}\",\"displayName\":\"Verify Officer\",\"role\":\"ClaimsOfficer\"}")"
CLAIMANT_TOKEN="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).token)" <<<"$CLAIMANT_RESP" 2>/dev/null)"
OFFICER_TOKEN="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).token)" <<<"$OFFICER_RESP" 2>/dev/null)"
OFFICER_ID="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).user.id)" <<<"$OFFICER_RESP" 2>/dev/null)"
check "dev-login (claimant + officer)" "$([[ -n "$CLAIMANT_TOKEN" && -n "$OFFICER_TOKEN" ]] && echo true || echo false)"

INCIDENT_DATE="$(date -u -d '-1 day' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v-1d +%Y-%m-%dT%H:%M:%SZ)"
SUBMIT_RESP="$(curl -s -X POST "${BASE_URL}/api/claims" -H "Content-Type: application/json" -H "Authorization: Bearer ${CLAIMANT_TOKEN}" \
    -d "{\"type\":\"Motor\",\"incidentDateUtc\":\"${INCIDENT_DATE}\",\"incidentDescription\":\"Verification run: rear-ended at a traffic light\",\"incidentLocation\":\"Orchard Road, Singapore\",\"vehicleRegistration\":\"SGX1234A\",\"propertyAddress\":null,\"estimatedLiability\":5000}")"
CLAIM_ID="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).id)" <<<"$SUBMIT_RESP" 2>/dev/null)"
CLAIM_REF="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).claimReference)" <<<"$SUBMIT_RESP" 2>/dev/null)"
check "Claim submission (POST /api/claims)" "$([[ "$CLAIM_REF" == CLM-* ]] && echo true || echo false)"

FETCH_STATUS="$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/api/claims/${CLAIM_ID}" -H "Authorization: Bearer ${CLAIMANT_TOKEN}")"
check "Claim retrieval (GET /api/claims/{id})" "$([[ "$FETCH_STATUS" == "200" ]] && echo true || echo false)"

ASSIGN_RESP="$(curl -s -X POST "${BASE_URL}/api/claims/${CLAIM_ID}/assign" -H "Authorization: Bearer ${OFFICER_TOKEN}")"
ASSIGNED_OFFICER="$(node -e "console.log(JSON.parse(require('fs').readFileSync(0)).assignedOfficerUserId)" <<<"$ASSIGN_RESP" 2>/dev/null)"
check "Claim assignment (POST /api/claims/{id}/assign)" "$([[ "$ASSIGNED_OFFICER" == "$OFFICER_ID" ]] && echo true || echo false)"

echo -e "\n=== 10. Valid claim lifecycle transition ==="
DECIDE_STATUS="$(curl -s -o /dev/null -w "%{http_code}" -X POST "${BASE_URL}/api/claims/${CLAIM_ID}/decision" -H "Content-Type: application/json" -H "Authorization: Bearer ${OFFICER_TOKEN}" \
    -d '{"outcome":"Approved","settlementAmount":4500,"reason":"Verification run: valid claim"}')"
check "Valid transition Assigned -> Approved" "$([[ "$DECIDE_STATUS" == "200" ]] && echo true || echo false)"

echo -e "\n=== 11. Rejection of an invalid operation ==="
INVALID_STATUS="$(curl -s -o /dev/null -w "%{http_code}" -X POST "${BASE_URL}/api/claims/${CLAIM_ID}/decision" -H "Content-Type: application/json" -H "Authorization: Bearer ${OFFICER_TOKEN}" \
    -d '{"outcome":"Rejected","reason":"Already decided"}')"
check "Invalid transition on already-decided claim rejected with 409" "$([[ "$INVALID_STATUS" == "409" ]] && echo true || echo false)"

if [[ "$WITH_KAFKA" == "true" ]]; then
    echo -e "\n=== 13. Kafka connectivity ==="
    if (exec 3<>/dev/tcp/localhost/9092) 2>/dev/null; then
        check "Kafka broker reachable on localhost:9092" "true"
        exec 3>&-
    else
        check "Kafka broker reachable on localhost:9092" "false" "(run scripts/kafka-start.ps1 first)"
    fi
else
    echo -e "\n=== 13. Kafka connectivity (skipped - pass --with-kafka to enable) ==="
fi

echo
echo "=================================="
if [[ $FAILURES -eq 0 ]]; then
    echo "All checks passed."
    exit 0
else
    echo "${FAILURES} check(s) failed."
    exit 1
fi
