#!/usr/bin/env bash
# Run the same jobs that .github/workflows/ci.yml runs, locally.
# Mirrors the workflow command-for-command so a green run here means
# CI should be green too.
#
# Usage:
#   ./scripts/ci-local.sh             # both jobs
#   ./scripts/ci-local.sh server      # server only (.NET build + xUnit)
#   ./scripts/ci-local.sh client      # client only (npm ci + build)

set -Eeuo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${1:-all}"

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
red()  { printf '\033[31m%s\033[0m\n' "$*"; }
grn()  { printf '\033[32m%s\033[0m\n' "$*"; }

server_job() {
  bold "==> server (.NET 10)"
  cd "$REPO_ROOT/src/server"
  dotnet restore Collectify.slnx
  dotnet build  Collectify.slnx --no-restore --configuration Release
  dotnet test   --no-build --configuration Release --logger "console;verbosity=normal"
}

client_job() {
  bold "==> client (Vite/TS)"
  cd "$REPO_ROOT/src/client"
  npm ci
  npm run build
}

start_ts=$(date +%s)
trap 'red "ci-local: FAILED (job exited non-zero)"; exit 1' ERR

case "$TARGET" in
  server) server_job ;;
  client) client_job ;;
  all)    server_job; client_job ;;
  *)      red "unknown target: $TARGET (expected: server | client | all)"; exit 2 ;;
esac

elapsed=$(( $(date +%s) - start_ts ))
grn "ci-local: OK ($TARGET, ${elapsed}s)"
