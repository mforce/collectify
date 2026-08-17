#!/usr/bin/env bash
# Run the same jobs that .github/workflows/ci.yml runs, locally.
# Mirrors the workflow command-for-command so a green run here means
# CI should be green too.
#
# Usage:
#   ./scripts/ci-local.sh             # both jobs
#   ./scripts/ci-local.sh server      # server only (.NET build + xUnit)
#   ./scripts/ci-local.sh client      # client only (npm ci + build)
#   ./scripts/ci-local.sh docker      # Docker entrypoint smoke tests

set -Eeuo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${1:-all}"

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
red()  { printf '\033[31m%s\033[0m\n' "$*"; }
grn()  { printf '\033[32m%s\033[0m\n' "$*"; }

server_job() {
  bold "==> server (.NET 10)"
  cd "$REPO_ROOT/src/server"
  # --locked-mode mirrors CI: fails NU1004 if the committed lock files drifted.
  dotnet restore Collectify.slnx --locked-mode
  bold "    NuGet vulnerability gate (high+, blocking)"
  dotnet list Collectify.slnx package --vulnerable --include-transitive \
    --format json --output-version 1 \
    | node "$REPO_ROOT/.github/scripts/vuln-gate.mjs" --ecosystem nuget --level high
  dotnet build  Collectify.slnx --no-restore --configuration Release
  dotnet test   --no-build --configuration Release --logger "console;verbosity=normal"
}

client_job() {
  bold "==> client (Vite/TS)"
  # The gate's own self-tests, before trusting its verdict (pure Node).
  node --test "$REPO_ROOT/.github/scripts/vuln-gate.test.mjs"
  cd "$REPO_ROOT/src/client"
  npm ci
  bold "    npm prod vulnerability gate (high+, blocking)"
  npm audit --omit=dev --json > npm-audit-prod.json || true
  node "$REPO_ROOT/.github/scripts/vuln-gate.mjs" --ecosystem npm --level high \
    --exceptions "$REPO_ROOT/.github/security-exceptions.json" < npm-audit-prod.json
  bold "    npm full-tree audit (moderate+, advisory only)"
  npm audit --json > npm-audit-all.json || true
  node "$REPO_ROOT/.github/scripts/vuln-gate.mjs" --ecosystem npm --level moderate --warn-only \
    --exceptions "$REPO_ROOT/.github/security-exceptions.json" < npm-audit-all.json
  bold "    enum parity (server enums vs client tables)"
  npm run check:enums
  npm test
  npm run build
}

docker_job() {
  bold "==> docker"
  cd "$REPO_ROOT"
  ./scripts/test-docker-entrypoint.sh
}

start_ts=$(date +%s)
trap 'red "ci-local: FAILED (job exited non-zero)"; exit 1' ERR

case "$TARGET" in
  server) server_job ;;
  client) client_job ;;
  docker) docker_job ;;
  all)    server_job; client_job; docker_job ;;
  *)      red "unknown target: $TARGET (expected: server | client | docker | all)"; exit 2 ;;
esac

elapsed=$(( $(date +%s) - start_ts ))
grn "ci-local: OK ($TARGET, ${elapsed}s)"
