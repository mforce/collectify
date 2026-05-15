# syntax=docker/dockerfile:1.7

FROM node:24-alpine AS client-build
WORKDIR /client
# Silence npm's update-notifier "new version available" notice in
# build logs. Real warnings (deprecations, audit findings) still surface.
ENV NPM_CONFIG_UPDATE_NOTIFIER=false
COPY src/client/package.json src/client/package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY src/client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /server
COPY src/server/ ./
RUN dotnet restore Collectify.slnx
COPY --from=client-build /client/dist ./Collectify.Api/wwwroot
RUN dotnet publish Collectify.Api/Collectify.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl isn't in the base image; install it so HEALTHCHECK below can use
# it. Pulls in ~14 transitive deps for ~6 MiB of overhead, in exchange
# for a self-contained image that doesn't depend on the host
# orchestrator providing its own probe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Collectify__DataDir=/data
COPY --from=server-build /app/publish ./
RUN mkdir -p /data && chown -R 1000:1000 /data
USER 1000:1000
EXPOSE 8080
# Hits /api/health (anonymous, DB-free) so a migration-in-progress or
# write-stall doesn't flap the container. Orchestrators / Watchtower /
# Docker compose all key off this.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "Collectify.Api.dll"]
