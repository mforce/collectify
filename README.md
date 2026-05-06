# Collectify

A self-hostable web app for tracking your personal collection of **movies** (DVD / Blu-ray / UHD Blu-ray), **music** (CDs / vinyl), and **videogames** (physical and digital).

> **Status:** Phase 1 — manual entry / edit / search for all three types, single-user with password, packaged with Docker. Internet metadata lookup (Phase 2) and barcode camera scanning (Phase 3) are tracked as separate issues.

## Stack

- ASP.NET Core 10 (Minimal APIs) + EF Core + SQLite
- React 18 + Vite + TypeScript + Tailwind + TanStack Query
- ASP.NET Identity (cookie auth, designed to extend to multi-user)
- Docker + docker-compose, single container

## Quick start

```bash
cp .env.example .env       # edit API keys here later (none required for Phase 1)
docker compose up --build  # builds React, publishes the API, runs on :8080
```

Open <http://localhost:8080>. The first visit sends you to **/setup** to create your account; after that you log in at **/login**.

Data is persisted to a named Docker volume `collectify-data` (mounted at `/data` inside the container, holds `collectify.db`).

## Local development without Docker

Two terminals:

```bash
# Terminal 1 — API on :8080
cd src/server
dotnet run --project Collectify.Api
```

```bash
# Terminal 2 — Vite dev server on :5173, proxies /api → :8080
cd src/client
npm install
npm run dev
```

The Vite dev server proxies `/api/*` calls to the .NET app, so you get hot reload on the React side while the API rebuilds on changes.

## Project layout

```
src/
  server/
    Collectify.slnx               # solution file (also references the client folder)
    Collectify.Api/               # ASP.NET Core API + serves React build
    Collectify.Domain/            # Entities + enums
    Collectify.Infrastructure/    # EF Core DbContext, Identity, (later) metadata clients
    tests/Collectify.Tests/       # xUnit tests
  client/                         # Vite + React + TS frontend (sources at root, no nested src/)
Dockerfile                        # multi-stage: node → React, sdk → publish, aspnet → runtime
docker-compose.yml                # one service + named data volume
```

## Roadmap

See [GitHub issues](https://github.com/mforce/collectify/issues) for the active roadmap. Phases:

- **Phase 1 — Foundation** (this PR): scaffolding, auth, manual CRUD, Docker.
- **Phase 2 — Internet lookup:** TMDB / MusicBrainz / IGDB providers + cover caching.
- **Phase 3 — Barcode scanning:** in-browser webcam UPC scan with `@zxing/browser`.
- **Phase 4 — Multi-user.**
- **Phase 5 — Photo-snap visual lookup** (future).
