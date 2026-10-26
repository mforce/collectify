#!/usr/bin/env bash
# Trusted applicator for the dependabot-lockfix workflow (#224). Runs in the
# `commit` job (write token present) and executes NO project code, only git,
# file ops and the trusted classifier. In the PR checkout under `pr/` it reverts
# Dependabot's csproj edits to the merge base, applies the regenerated locks from
# $ARTIFACT_DIR, and commits and pushes iff nothing else changed.
set -euo pipefail

mapfile -t LOCKS < <(node trusted/.github/scripts/lockfix.mjs --list)
[ "${#LOCKS[@]}" -gt 0 ] || { echo "empty lock allowlist"; exit 1; }

# Apply each artifact file onto its exact allowlisted path, after checking it is
# a regular file and parses as JSON (reject a poisoned artifact).
for rel in "${LOCKS[@]}"; do
  src="$ARTIFACT_DIR/$rel"
  [ -f "$src" ] && [ ! -L "$src" ] || { echo "missing regenerated lock: $rel"; exit 1; }
  node -e "JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'))" "$src" \
    || { echo "regenerated lock is not valid JSON: $rel"; exit 1; }
  cp "$src" "pr/$rel"
done

cd pr

# The same revert the compute job made before restoring, so the locks match.
base="$(git merge-base HEAD "origin/$DEFAULT_BRANCH")"
git diff -z --name-only --diff-filter=M "$base" HEAD -- 'src/server/*.csproj' \
  | xargs -0 -r git checkout "$base" --

set +e
git status --porcelain -z | node ../trusted/.github/scripts/lockfix.mjs "$base"
verdict=$?
set -e

case "$verdict" in
  3) echo "Nothing changed: locks current, no csproj edits."; exit 0 ;;
  0) : ;;  # commit below
  *) echo "Classifier refused (exit $verdict), not committing."; exit 1 ;;
esac

# Against HEAD: the csproj revert above is staged, the copied locks are not.
locks="$(git diff HEAD --name-only -- "${LOCKS[@]}" | sed 's/^/- /')"
reverted="$(git diff HEAD --name-only -- 'src/server/*.csproj' | sed 's/^/- /')"

git config user.name "collectify-lockfix[bot]"
git config user.email "collectify-lockfix[bot]@users.noreply.github.com"
git add -A
git commit -F - <<EOF
chore(deps): regenerate packages.lock.json and revert csproj edits

Regenerated lock files:
${locks:-(none)}

Reverted to merge base ${base}
(versions live in Directory.Packages.props, so a bump needs no csproj edit):
${reverted:-(none)}
EOF

# Compare-and-swap: only fast-forward the branch if its tip is still the SHA we
# built on. A moved branch is rejected (no --force); the next CI cycle re-fixes.
git push origin "HEAD:refs/heads/${HEAD_BRANCH}"
