#!/usr/bin/env bash
# Holds the write token, so it runs no project code: only git, file copies and
# the trusted classifier. Reverts csproj edits and applies the regenerated locks
# in pr/, then commits and pushes iff nothing else changed.
set -euo pipefail

mapfile -t LOCKS < <(node trusted/.github/scripts/lockfix.mjs --list)
[ "${#LOCKS[@]}" -gt 0 ] || { echo "empty lock allowlist"; exit 1; }

root="$(pwd -P)"
for rel in "${LOCKS[@]}"; do
  src="$ARTIFACT_DIR/$rel"
  # Reject a poisoned artifact.
  [ -f "$src" ] && [ ! -L "$src" ] || { echo "missing regenerated lock: $rel"; exit 1; }
  # The PR tree controls the destination: a symlink on its path would write
  # outside pr/, for example over these trusted scripts.
  [ "$(realpath -m "pr/$rel")" = "$root/pr/$rel" ] \
    || { echo "lock path goes through a symlink: $rel"; exit 1; }
  [ "$(git -C pr ls-files -s -- "$rel" | cut -c1-6)" = 100644 ] \
    || { echo "lock is not a tracked regular file: $rel"; exit 1; }
  cp "$src" "pr/$rel"
done

cd pr
base="$(bash ../trusted/.github/scripts/lockfix-revert.sh)"

set +e
git status --porcelain -z | node ../trusted/.github/scripts/lockfix.mjs "$base"
verdict=$?
set -e

case "$verdict" in
  3) echo "Nothing changed: locks current, no csproj edits."; exit 0 ;;
  4) : ;;
  *) echo "Classifier refused (exit $verdict), not committing."; exit 1 ;;
esac

# Against HEAD: the csproj revert above is staged, the copied locks are not.
locks="$(git diff HEAD --name-only -- "${LOCKS[@]}" | sed 's/^/- /')"
reverted="$(git diff HEAD --name-only -- ':(glob)src/server/**/*.csproj' | sed 's/^/- /')"

git config user.name "collectify-lockfix[bot]"
git config user.email "collectify-lockfix[bot]@users.noreply.github.com"
git add -A
git commit -F - <<MSG
chore(deps): regenerate packages.lock.json and revert csproj edits

Regenerated lock files:
${locks:-(none)}

Reverted to merge base ${base}
(versions live in Directory.Packages.props, so a bump needs no csproj edit):
${reverted:-(none)}
MSG

# No --force: a moved tip rejects the push; the next CI cycle re-fixes.
git push origin "HEAD:refs/heads/${HEAD_BRANCH}"
