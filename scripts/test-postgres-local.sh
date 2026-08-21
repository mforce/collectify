#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo/src/server/tests/Collectify.PostgresTests/Collectify.PostgresTests.csproj"
filter=""
expect_count=""
gate=false
verify_trx=""

while (($#)); do
  case "$1" in
    --filter)
      (($# >= 2)) || { echo 'missing value for --filter' >&2; exit 2; }
      filter="$2"; shift 2 ;;
    --expect-count)
      (($# >= 2)) || { echo 'missing value for --expect-count' >&2; exit 2; }
      [[ "$2" =~ ^[1-9][0-9]*$ ]] || { echo '--expect-count must be a positive integer' >&2; exit 2; }
      expect_count="$2"; shift 2 ;;
    --gate)
      gate=true; shift ;;
    --verify-trx)
      (($# >= 2)) || { echo 'missing value for --verify-trx' >&2; exit 2; }
      verify_trx="$2"; shift 2 ;;
    *)
      printf 'unknown argument: %s\n' "$1" >&2; exit 2 ;;
  esac
done

if $gate; then
  echo 'Runbook B PostgreSQL manifest/TRX gate is not installed yet' >&2
  exit 2
fi

verify_results() {
  local results_dir="$1"
  local expected="${2:-}"
  python3 - "$results_dir" "$expected" <<'PY'
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1])
expected = int(sys.argv[2]) if sys.argv[2] else None
files = sorted(root.glob('*.trx')) if root.is_dir() else [root]
files = [path for path in files if path.is_file()]
if len(files) != 1:
    raise SystemExit(f'expected exactly one TRX file, found {len(files)}')
try:
    document = ET.parse(files[0])
except ET.ParseError as error:
    raise SystemExit(f'malformed TRX: {error}') from error
results = [node for node in document.iter() if node.tag.rsplit('}', 1)[-1] == 'UnitTestResult']
if not results:
    raise SystemExit('zero executed tests')
outcomes = [node.attrib.get('outcome', '') for node in results]
not_executed = [value for value in outcomes if value.lower() in {'notexecuted', 'skipped'}]
if not_executed:
    raise SystemExit(f'non-executed tests present: {not_executed}')
if expected is not None and len(results) != expected:
    raise SystemExit(f'expected exactly {expected} executed tests, found {len(results)}')
print(f'trx-count-valid executed={len(results)} skipped=0')
PY
}

if [[ -n "$verify_trx" ]]; then
  verify_results "$verify_trx" "$expect_count"
  exit 0
fi

run_root="$(mktemp -d "${TMPDIR:-/tmp}/collectify-postgres-tests.XXXXXXXX")"
cleanup() { rm -rf "$run_root"; }
trap cleanup EXIT INT TERM
mkdir -p "$run_root/work" "$run_root/results" "$run_root/output"

export COLLECTIFY_REPOSITORY_ROOT="$repo"
export COLLECTIFY_TEST_OUTPUT_ROOT="$run_root/output"

cd "$repo/src/server"
dotnet restore Collectify.slnx --locked-mode
dotnet build Collectify.slnx --configuration Release --no-restore

test_args=(
  test "$project"
  --configuration Release
  --no-build
  --no-restore
  --logger 'console;verbosity=normal'
  --logger 'trx;LogFileName=postgres-tests.trx'
  --results-directory "$run_root/results"
)
if [[ -n "$filter" ]]; then
  test_args+=(--filter "$filter")
fi

set +e
if docker info >/dev/null 2>&1; then
  (
    cd "$run_root/work"
    dotnet "${test_args[@]}"
  )
  test_rc=$?
else
  sudo -n env \
    "COLLECTIFY_REPOSITORY_ROOT=$repo" \
    "COLLECTIFY_TEST_OUTPUT_ROOT=$run_root/output" \
    "PATH=$PATH" \
    sh -c 'cd "$1" && shift && exec dotnet "$@"' sh "$run_root/work" "${test_args[@]}"
  test_rc=$?
fi
set -e

verify_results "$run_root/results" "$expect_count"
exit "$test_rc"
