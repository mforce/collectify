#!/bin/sh
set -e

if [ "$(id -u)" != "0" ]; then
    exec dotnet Collectify.Api.dll
fi

PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
DATA_DIR="${Collectify__DataDir:-${COLLECTIFY_DATA_DIR:-/data}}"

groupmod -o -g "$PGID" app
usermod -o -u "$PUID" app

mkdir -p "$DATA_DIR"
chown -R "$PUID:$PGID" "$DATA_DIR"

exec gosu "$PUID:$PGID" dotnet Collectify.Api.dll
