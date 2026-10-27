#!/usr/bin/env bash
# Runs the untrusted restore of pr/ inside the SDK image the TRUSTED Dockerfile
# pins (see SECURITY MODEL in dependabot-lockfix.yml). Run from the workspace root.
set -euo pipefail

mapfile -t sdk < <(sed -nE 's#^FROM (mcr\.microsoft\.com/dotnet/sdk:[^ @]+@sha256:[0-9a-f]{64})( .*)?$#\1#p' trusted/Dockerfile)
[ "${#sdk[@]}" -eq 1 ] || { echo "expected one digest-pinned SDK image in trusted/Dockerfile, found ${#sdk[@]}"; exit 1; }

# The tmpfs hides pr/.git: checkout's post step runs git there on the host,
# so a container-written git config or hook would run with the runner's env.
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp -e NUGET_PACKAGES=/tmp/nuget \
  -v "$PWD/pr:/src" \
  --mount type=tmpfs,destination=/src/.git \
  -v "$PWD/trusted/.github/nuget.lockfix.config:/cfg/nuget.config:ro" \
  -w /src \
  "${sdk[0]}" \
  dotnet restore src/server/Collectify.slnx --force-evaluate --configfile /cfg/nuget.config
