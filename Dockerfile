# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS client-build
WORKDIR /client
COPY client/package.json client/package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY server/Collectify.sln ./
COPY server/src/ ./src/
COPY server/tests/ ./tests/
RUN dotnet restore Collectify.sln
COPY --from=client-build /client/dist ./src/Collectify.Api/wwwroot
RUN dotnet publish src/Collectify.Api/Collectify.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Collectify__DataDir=/data
COPY --from=server-build /app/publish ./
RUN mkdir -p /data && chown -R 1000:1000 /data
USER 1000:1000
EXPOSE 8080
ENTRYPOINT ["dotnet", "Collectify.Api.dll"]
