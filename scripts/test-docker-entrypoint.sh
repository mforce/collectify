#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
entrypoint="$repo_root/docker/entrypoint.sh"

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

log="$tmpdir/calls.log"
mkdir -p "$tmpdir/bin" "$tmpdir/data"

cat > "$tmpdir/bin/id" <<'STUB'
#!/bin/sh
if [ "$1" = "-u" ]; then
  printf '%s\n' "${TEST_UID:-0}"
  exit 0
fi
exec /usr/bin/id "$@"
STUB

cat > "$tmpdir/bin/groupmod" <<'STUB'
#!/bin/sh
printf 'groupmod %s\n' "$*" >> "$ENTRYPOINT_TEST_LOG"
STUB

cat > "$tmpdir/bin/usermod" <<'STUB'
#!/bin/sh
printf 'usermod %s\n' "$*" >> "$ENTRYPOINT_TEST_LOG"
STUB

cat > "$tmpdir/bin/chown" <<'STUB'
#!/bin/sh
printf 'chown %s\n' "$*" >> "$ENTRYPOINT_TEST_LOG"
STUB

cat > "$tmpdir/bin/gosu" <<'STUB'
#!/bin/sh
printf 'gosu %s\n' "$*" >> "$ENTRYPOINT_TEST_LOG"
exit 0
STUB

cat > "$tmpdir/bin/dotnet" <<'STUB'
#!/bin/sh
printf 'dotnet %s\n' "$*" >> "$ENTRYPOINT_TEST_LOG"
exit 0
STUB

chmod +x "$tmpdir/bin"/*

run_entrypoint() {
  : > "$log"
  PATH="$tmpdir/bin:$PATH" \
  ENTRYPOINT_TEST_LOG="$log" \
  COLLECTIFY_DATA_DIR="$tmpdir/data" \
  "$entrypoint"
}

assert_log_contains() {
  local expected="$1"
  if ! grep -Fxq "$expected" "$log"; then
    echo "Expected log line not found: $expected" >&2
    echo "Actual log:" >&2
    cat "$log" >&2
    exit 1
  fi
}

run_entrypoint
assert_log_contains "groupmod -o -g 1000 app"
assert_log_contains "usermod -o -u 1000 app"
assert_log_contains "chown -R 1000:1000 $tmpdir/data"
assert_log_contains "gosu 1000:1000 dotnet Collectify.Api.dll"

PUID=1234 PGID=5678 run_entrypoint
assert_log_contains "groupmod -o -g 5678 app"
assert_log_contains "usermod -o -u 1234 app"
assert_log_contains "chown -R 1234:5678 $tmpdir/data"
assert_log_contains "gosu 1234:5678 dotnet Collectify.Api.dll"

TEST_UID=1000 run_entrypoint
if grep -Eq 'groupmod|usermod|chown|gosu' "$log"; then
  echo "Non-root startup should not mutate users or call gosu." >&2
  cat "$log" >&2
  exit 1
fi
assert_log_contains "dotnet Collectify.Api.dll"

echo "docker entrypoint tests passed"
