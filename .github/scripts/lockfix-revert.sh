#!/usr/bin/env bash
# Run inside the PR checkout. Restores every csproj the PR modified to its merge
# base with the default branch, then prints that base.
set -euo pipefail

base="$(git merge-base HEAD "origin/$DEFAULT_BRANCH")"
git diff -z --name-only --diff-filter=M "$base" HEAD -- ':(glob)src/server/**/*.csproj' \
  | xargs -0 -r git checkout "$base" --
echo "$base"
