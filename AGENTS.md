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
│   ├── testing.md         # TDD workflow + test pyramid + endpoint coverage rules
│   └── conventions.md     # coding conventions, error handling, tests
├── graphify-out/                        # knowledge graph (see "Knowledge graph" below)
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

## Knowledge graph (consult before grepping)

This repo ships a pre-built [graphify](https://github.com/safishamsi/graphify/blob/v7/README.md) knowledge graph at [`graphify-out/`](graphify-out/). It maps every code symbol, doc concept, and cross-file relationship into a navigable graph with confidence-tagged edges (`EXTRACTED` / `INFERRED` / `AMBIGUOUS`). **Use it instead of brute-force file searches when you need to understand how things connect.** Average query is ~24× cheaper in tokens than re-reading the relevant files.

**Files:**

- [`graphify-out/GRAPH_REPORT.md`](graphify-out/GRAPH_REPORT.md) — **read this first.** Lists god nodes (most-connected concepts), surprising cross-module links, suggested questions the graph is uniquely positioned to answer, and per-community cohesion scores.
- [`graphify-out/graph.html`](graphify-out/graph.html) — interactive browser viz; humans only.
- [`graphify-out/graph.json`](graphify-out/graph.json) — raw graph data; the CLI below reads this automatically.

**Query from the terminal** (preferred path for agents):

```bash
graphify query   "how does the auth cookie reach the data hooks?"   # BFS context
graphify query   "trace request from MoviesEndpoints to SQLite" --dfs --budget 1500
graphify path    "App (root router component)" "useList()"         # shortest connection
graphify explain "CollectifyDbContext"                              # everything linked to a node
```

Each result cites `source_file:source_location`, so you can jump straight to the line that justifies the edge. Confidence tags tell you what was structurally extracted vs. semantically inferred — never act on an `AMBIGUOUS` edge without verifying.

**Keeping the graph fresh:**

| Change | Refresh command | Cost |
|---|---|---|
| Code only | post-commit hook (`graphify hook install`, one-time) | free — AST only, no LLM |
| Docs / specs / new files | `/graphify . --update` | LLM (only changed files re-extracted) |
| After a large refactor | `/graphify .` | LLM (full rebuild) |

If you change a `docs/*.md` file or add a new entity, run `/graphify . --update` before claiming the work is done so the next session's agent doesn't navigate by a stale map.

**MCP option:** for tool-call access from inside another agent, `python -m graphify.serve graphify-out/graph.json` exposes `query_graph`, `get_node`, `get_neighbors`, `shortest_path`, and `god_nodes` over stdio MCP.

See the [graphify v7 README](https://github.com/safishamsi/graphify/blob/v7/README.md) for the full command reference, ignore-file syntax, and team workflow.

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
| Get oriented / trace a relationship | [`graphify-out/GRAPH_REPORT.md`](graphify-out/GRAPH_REPORT.md), then `graphify query` / `path` / `explain` |
| Understand data model | [`docs/data-model.md`](docs/data-model.md) (spec) and `src/server/Collectify.Domain/Entities/` (implementation) |
| Add a new endpoint | `src/server/Collectify.Api/Endpoints/` |
| Change DB schema | `Collectify.Infrastructure/Data/CollectifyDbContext.cs` then add a migration |
| Add a frontend page | `src/client/pages/` + register in `src/client/App.tsx` |
| Wire an API call | `src/client/api/` (one file per resource) |
| Configure deployment | `Dockerfile`, `docker-compose.yml`, `.env.example` |
| Touch CI | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — runs server build + xUnit suite and client build on every push / PR. Run the same jobs locally with [`./scripts/ci-local.sh`](scripts/ci-local.sh) (optionally `server` or `client` to scope). |
| Read project specs | `docs/` |
| Write a test | [`docs/testing.md`](docs/testing.md) — TDD workflow, layers, required coverage |
