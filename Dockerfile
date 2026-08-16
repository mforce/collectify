# syntax=docker/dockerfile:1.7

# Base images are pinned to immutable digests, not floating tags, so a rebuild
# reproduces the exact bytes CI's Trivy scan cleared. A pinned digest never moves
# on its own, so Dependabot's `docker` ecosystem owns bumping these (see
# .github/dependabot.yml) and the Trivy gate then confirms the new base is clean.
FROM node:24-alpine@sha256:d32cdf619f63fe0471182d08996dd516c6275bb5fd31ae06e55a570bd9e1ad43 AS client-build
WORKDIR /client
# Silence npm's update-notifier "new version available" notice in
# build logs. Real warnings (deprecations, audit findings) still surface.
ENV NPM_CONFIG_UPDATE_NOTIFIER=false
COPY src/client/package.json src/client/package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY src/client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS server-build
WORKDIR /server
# Stamped into the published assembly; /api/health reads the informational
# version. Both default to 0.0.0 for a local build. CI passes version.txt's
# content as VERSION and `<version>+<short-sha>` as INFORMATIONAL_VERSION.
# Because release-please bumps version.txt IN the release commit — the exact
# commit CI builds and promotion retags — the promoted release image reports the
# real release version (e.g. 0.0.8+abc1234), while an interim commit image
# reports <last release>+<sha>. VERSION stays a bare X.Y.Z so AssemblyVersion,
# which rejects build metadata, is happy; the `+sha` rides InformationalVersion.
ARG VERSION=0.0.0
ARG INFORMATIONAL_VERSION=0.0.0
COPY src/server/ ./
# --locked-mode: restore must match the committed packages.lock.json exactly, so
# the image cannot be built against a dependency graph that drifted from the one
# CI resolved and audited. Fails with NU1004 when a package was added or bumped
# without regenerating the locks.
RUN dotnet restore Collectify.slnx --locked-mode
COPY --from=client-build /client/dist ./Collectify.Api/wwwroot
RUN dotnet publish Collectify.Api/Collectify.Api.csproj -c Release -o /app/publish \
    /p:UseAppHost=false \
    /p:Version=${VERSION} \
    /p:InformationalVersion=${INFORMATIONAL_VERSION} \
    /p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:207cc51496778557731c81ff670333d8ade4a4fec22768fd1be8e78474a84ecf AS runtime
WORKDIR /app
# curl isn't in the base image; install it so HEALTHCHECK below can use
# it. Pulls in ~14 transitive deps for ~6 MiB of overhead, in exchange
# for a self-contained image that doesn't depend on the host
# orchestrator providing its own probe.
ARG DEFAULT_PUID=1000
ARG DEFAULT_PGID=1000
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl gosu \
    && rm -rf /var/lib/apt/lists/* \
    && if ! getent group app >/dev/null; then groupadd -o -g "$DEFAULT_PGID" app; fi \
    && if ! id -u app >/dev/null 2>&1; then useradd -o -u "$DEFAULT_PUID" -g app -d /app -s /usr/sbin/nologin app; fi
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Collectify__DataDir=/data \
    PUID=${DEFAULT_PUID} \
    PGID=${DEFAULT_PGID}
COPY --from=server-build /app/publish ./
COPY docker/entrypoint.sh /usr/local/bin/collectify-entrypoint
RUN chmod +x /usr/local/bin/collectify-entrypoint \
    && mkdir -p /data \
    && chown -R app:app /data
EXPOSE 8080
# Hits /api/health (anonymous, DB-free) so a migration-in-progress or
# write-stall doesn't flap the container. Orchestrators / Watchtower /
# Docker compose all key off this.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1
ENTRYPOINT ["collectify-entrypoint"]
