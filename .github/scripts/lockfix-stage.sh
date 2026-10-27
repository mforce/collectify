#!/usr/bin/env bash
# Copies the allowlisted locks from pr/ into $1, keeping repo-relative paths
# (lockfix-apply.sh reads them back). The restore container wrote pr/, so a
# lock or a parent directory may be a symlink to a host file: refuse it.
# Run from the workspace root.
set -euo pipefail
dest="$1"

mapfile -t LOCKS < <(node trusted/.github/scripts/lockfix.mjs --list)
[ "${#LOCKS[@]}" -gt 0 ] || { echo "empty lock allowlist"; exit 1; }

root="$(pwd -P)"
for rel in "${LOCKS[@]}"; do
  [ "$(realpath -m "pr/$rel")" = "$root/pr/$rel" ] \
    || { echo "lock path goes through a symlink: $rel"; exit 1; }
  [ -f "pr/$rel" ] || { echo "lock is not a regular file: $rel"; exit 1; }
  install -D -m 644 "pr/$rel" "$dest/$rel"
done
