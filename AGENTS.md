# AGENTS.md — Guide for AI agents working on Collectify

> **Purpose:** Help AI coding agents (Claude Code, Copilot, Cursor, etc.) get oriented in this repo quickly without re-reading every file. Read this before exploring.

## What is this project?

Collectify is a **self-hostable web app** for tracking a personal collection of:

- **Movies** — DVD / Blu-ray / UHD Blu-ray
- **Music** — CDs / vinyl
- **Videogames** — physical and digital

Single-user with a password (multi-user is a forward-compatible Phase 4). Designed to run as a single Docker container behind the user's reverse proxy / VPN.

## Stack at a glance

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 10 Minimal APIs |
| Persistence | EF Core 10 + SQLite (single-file DB in a Docker volume) |
| Auth | ASP.NET Core Identity + cookie auth |
| Frontend | React 18 + Vite + TypeScript + Tailwind + TanStack Query + React Router |
| Container | Multi-stage Dockerfile, single image, single port (8080) |
| Tests | xUnit (server), Vitest planned (client) |

## Repo layout

```
/
├── AGENTS.md              # this file
├── CLAUDE.md              # Claude-specific entry point (points here)
├── README.md              # human-facing run instructions
├── Dockerfile             # multi-stage: node → React, sdk → publish, aspnet → runtime
├── docker-compose.yml     # single service + named volume
├── .env.example           # API keys (Phase 2/3)
├── docs/
│   ├── architecture.md    # backend CLEAN architecture + layering rules
│   ├── security.md        # OWASP Top 10 + frontend hardening checklist
│   ├── data-model.md      # fields per collection category + tags + enums
│   └── conventions.md     # coding conventions, error handling, tests
└── src/
    ├── server/
    │   ├── Collectify.slnx              # solution (also references the client folder)
    │   ├── Collectify.Api/              # ASP.NET Core API + serves React build
    │   ├── Collectify.Domain/           # Entities + enums (no infra deps)
    │   ├── Collectify.Infrastructure/   # EF Core, Identity, (later) metadata clients
    │   └── tests/Collectify.Tests/      # xUnit
    └── client/                          # Vite React app (sources at root, no nested src/)
        ├── index.html                   # entry; loads /main.tsx
        ├── main.tsx, App.tsx
        ├── api/                         # TanStack Query hooks + fetch client
        ├── components/                  # forms, layout, primitives
        └── pages/                       # route components
```

## Roadmap

Tracked in GitHub issues:

- **Phase 1** — Foundation (auth, manual CRUD, Docker) — issue #2 / PR #5
- **Phase 2** — Internet metadata lookup (TMDB / MusicBrainz / IGDB) — issue #3
- **Phase 3** — UPC barcode camera scan — issue #4
- **Phase 4** — Multi-user (deferred; design hooks already in place)
- **Phase 5** — Photo-snap visual lookup (future)

Always check open issues before starting work and link PRs to the relevant issue.

## How to run things

### Local dev (two terminals)

```bash
# Terminal 1 — API on http://localhost:5041 (or 5089 from Properties/launchSettings.json)
cd src/server
dotnet run --project Collectify.Api

# Terminal 2 — Vite dev server on http://localhost:5173 with /api proxy → :8080
cd src/client
npm install
npm run dev
```

If you change the API port, update `vite.config.ts` proxy target to match.

### Production-like (single container)

```bash
docker compose up --build   # http://localhost:8080
```

### Build / test commands

```bash
# Server
cd src/server
dotnet build Collectify.slnx
dotnet test                            # runs xUnit tests
dotnet ef migrations add <Name> \      # add a migration
  --project Collectify.Infrastructure \
  --startup-project Collectify.Api \
  --output-dir Data/Migrations

# Client
cd src/client
npm run build                          # tsc -b && vite build
npm run dev
```

## Architecture quick rules

- **Domain** has zero non-BCL dependencies. Entities and enums only.
- **Infrastructure** depends on Domain. EF Core, Identity, external HTTP clients live here. No ASP.NET Core types.
- **Api** depends on Infrastructure + Domain. Endpoints, DTOs, DI wiring, auth, SPA hosting.
- **Tests** depend on Api (and transitively all others) for `WebApplicationFactory<Program>` integration tests.

See [`docs/architecture.md`](docs/architecture.md) for the full CLEAN-layered design rules.

## Security & quality

- All endpoints under `/api/` (except `/api/auth/*`) require authentication via cookie.
- Every collection row has an `OwnerId`; queries always filter by `OwnerId` from the current user.
- Cookies are `HttpOnly` + `SameSite=Lax`.
- Outbound metadata calls (Phase 2+) go through `IHttpClientFactory` with a `LookupCache` table to dedupe.
- Camera/barcode scanning (Phase 3) requires HTTPS; document `mkcert` / reverse-proxy options in README.

See [`docs/security.md`](docs/security.md) for the full OWASP Top 10 mitigation checklist (server) and frontend hardening (XSS, CSRF, dependency hygiene, etc.).

## Conventions

See [`docs/conventions.md`](docs/conventions.md). Key points:

- Minimal APIs grouped by resource in `Endpoints/*.cs` files. One group per resource.
- DTOs are `record` types defined alongside the endpoint group.
- React forms use controlled state with `useState` and a small `Field`/`Input` primitive set in `components/ui.tsx`. No `react-hook-form` until a form gets complex enough to need it.
- TanStack Query keys are `[type, 'list' | 'item', ...args]`. Mutations invalidate `[type]`.
- Comments only when a reader can't infer the intent. No "what" comments.

## When adding features

1. Find or open the GitHub issue first; link it from the PR.
2. Follow the layered architecture — never reference Infrastructure types from Domain.
3. New entity? Add to `Collectify.Domain/Entities/`, register in `CollectifyDbContext`, add an EF migration, surface via Minimal API endpoints with auth + `OwnerId` filtering.
4. New external API? Add a typed `HttpClient` in `Collectify.Infrastructure/Lookup/`, register in DI, cache via `LookupCache`. Make the API key optional and degrade gracefully.
5. New React page? Add to `pages/`, register in `App.tsx` router, reuse list/form primitives.
6. Verify: `dotnet build` clean, `npm run build` clean, smoke-test the affected endpoints with `curl` (cookie jar in `/tmp/cookies.txt`).

## Where to look first when…

| Goal | Start here |
|---|---|
| Understand data model | [`docs/data-model.md`](docs/data-model.md) (spec) and `src/server/Collectify.Domain/Entities/` (implementation) |
| Add a new endpoint | `src/server/Collectify.Api/Endpoints/` |
| Change DB schema | `Collectify.Infrastructure/Data/CollectifyDbContext.cs` then add a migration |
| Add a frontend page | `src/client/pages/` + register in `src/client/App.tsx` |
| Wire an API call | `src/client/api/` (one file per resource) |
| Configure deployment | `Dockerfile`, `docker-compose.yml`, `.env.example` |
| Read project specs | `docs/` |
