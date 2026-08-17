# Collectify

![Collectify banner](src/client/public/brand/collectify-sample.png)

A self-hostable web app for tracking your personal collection of **movies** (DVD / Blu-ray / UHD Blu-ray), **music** (CDs / vinyl), and **videogames** (physical and digital).

> **Status:** Phase 1 complete — manual entry, edit, search for all three types. Internet metadata lookup (TMDB / MusicBrainz / IGDB) and barcode camera scanning are included. Single-user with password, packaged as a Docker image.

## Quick start

```bash
docker compose up -d
```

Open <http://localhost:8080>. The first visit sends you to **/setup** to create your account; after that you log in at **/login**.

Data is persisted to a named Docker volume `collectify-data` (mounted at `/data` inside the container, holds `collectify.db`).

The container follows the LinuxServer-style `PUID`/`PGID` convention. On startup it runs a small root entrypoint, ensures `/data` is owned by the configured IDs, then drops privileges before launching Collectify. Both values default to `1000`:

```bash
PUID=$(id -u) PGID=$(id -g) docker compose up -d
```

For named volumes the defaults usually work. For bind mounts, set `PUID` and `PGID` to the host user that should own the data directory.

### PostgreSQL (optional)

Collectify defaults to SQLite (zero-config, single-file DB). If you prefer PostgreSQL for persistence, backups, or operational tooling:

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d
```

This adds a `postgres` service and configures Collectify to use it. Data lives in the `collectify-postgres-data` named volume. The database is created automatically on first boot. Schema evolution uses
`EnsureCreated()` (not EF migrations) — if the model changes, you need to
reset the `collectify-postgres-data` volume and let it recreate.

For your own Postgres instance, set the connection string in `.env`:

```
Collectify__Database__Provider=postgres
Collectify__Database__ConnectionString=Host=my-db;Port=5432;Database=collectify;Username=collectify;Password=secret
```

The connection string user needs `CREATEDB` permission so the app can create the database on first run. Subsequent starts just apply migrations.

### Configuration

Copy `.env.example` to `.env`, set the variables you need, and restart:

```bash
cp .env.example .env   # edit API keys here
docker compose restart
```

All provider keys are optional — lookups degrade gracefully when unconfigured. The container reads `.env` via `env_file` in [`docker-compose.yml`](docker-compose.yml), so every variable flows into the runtime automatically.

#### Metadata lookup providers

| Provider | Variable(s) | Required? | Purpose |
|---|---|---|---|
| **TMDB** (movies) | `Collectify__Metadata__Tmdb__ApiKey` | Yes | Movie title search, cover images. Get a v3 key at [themoviedb.org](https://www.themoviedb.org/settings/api). |
| **MusicBrainz** (music) | `Collectify__Metadata__MusicBrainz__UserAgent` | Yes | Music release lookup by barcode/title. Format: `"AppName/Version (contact@example.com)"`. No API key needed — the User-Agent is your identity per [their etiquette policy](https://musicbrainz.org/doc/MusicBrainz_API#etiquette). |
| **IGDB** (games) | `Collectify__Metadata__Igdb__TwitchClientId`<br>`Collectify__Metadata__Igdb__TwitchClientSecret` | Both | Game title search and cover images. Create a Twitch app at [dev.twitch.tv](https://dev.twitch.tv/console/apps). |
| **UPCitemdb** (barcode fallback) | *(none)* | No | Free trial endpoint, IP rate-limited (~100/day). Used for movies/games when TMDB/IGDB don't recognize a UPC. Override `Collectify__Metadata__Upc__BaseUrl` only for tests or paid-tier swaps. |

#### Platform import providers

| Provider | Variable(s) | Required? | Purpose |
|---|---|---|---|
| **Steam** (games) | `Collectify__Platforms__Steam__ApiKey` | Yes (to enable) | Connect a Steam account and import the owned games you list. Get a Steam Web API key at [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey). Fail-soft: leave it empty and the import page shows a "set the Steam API key to enable" hint. If you run behind a reverse proxy, also set `Collectify__PublicBaseUrl` so the OpenID callback URL matches your external host, and set `Collectify__ReverseProxy__KnownProxies` to your proxy's IP/CIDR so the Steam callback rate limiter can key on the real client address (otherwise every public caller shares the proxy's bucket and can exhaust the 30/min allowance). |

#### Other settings

| Variable | Default | Purpose |
|---|---|---|
| `Collectify__Metadata__CacheTtl` | `30.00:00:00` | How long a cached lookup stays fresh (`.NET TimeSpan`). |
| `Collectify__Auth__AllowRegistration` | `false` | Flip to `true` to expose `/register` and allow sign-ups. |
| `Collectify__IgdbBackfill__Enabled` | `true` | Background IGDB backfill (games with no IGDB id get developer/publisher/year/description/cover filled after a Steam import). Only active when IGDB credentials are configured; fill-only, so it never overwrites existing data. See `.env.example` for `Interval`, `PacingDelay`, `MaxGamesPerSweep`, `EmptyResultAbortThreshold`. |

See [`.env.example`](.env.example) for the full list including optional base URL overrides.

### Changing the image source

Override via environment variables or a `.env` file:

| Variable | Default | Purpose |
|---|---|---|
| `REGISTRY` | `ghcr.io` | Registry host |
| `IMAGE_NAME` | `mforce/collectify` | Image path inside the registry |
| `TAG` | `latest` | Image tag (`v0.1.0`, `v0.1`, etc.) |

Example — pulling from a self-hosted Gitea registry:

```bash
REGISTRY=git.example.com IMAGE_NAME=mforce/collectify TAG=v0.1.0 docker compose up -d
```

## Local development without Docker

Two terminals:

```bash
# Terminal 1 — API on :5041 (default from launchSettings)
cd src/server
dotnet run --project Collectify.Api

# Terminal 2 — Vite dev server on :5173, proxies /api → :5041
cd src/client
npm install
npm run dev
```

The Vite dev server proxies `/api/*` calls to the .NET app, so you get hot reload on the React side while the API rebuilds on changes.

### Building from source (Docker)

Use the development compose override to build locally instead of pulling:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

## Stack

- ASP.NET Core 10 (Minimal APIs) + EF Core + SQLite
- React 18 + Vite + TypeScript + Tailwind + TanStack Query
- ASP.NET Identity (cookie auth, designed to extend to multi-user)
- Docker + docker-compose, single container

## Project layout

```
src/
  server/
    Collectify.slnx               # solution file (also references the client folder)
    Collectify.Api/               # ASP.NET Core API + serves React build
    Collectify.Domain/            # Entities + enums
    Collectify.Infrastructure/    # EF Core DbContext, Identity, metadata clients
    tests/Collectify.Tests/       # xUnit tests
  client/                         # Vite + React + TS frontend (sources at root, no nested src/)
Dockerfile                        # multi-stage: node → React, sdk → publish, aspnet → runtime
docker-compose.yml                # consumer compose (pull from registry)
docker-compose.dev.yml            # dev override (build from source)
```

## Barcode scanning

The "Scan barcode" button on each form uses the device camera via
`@zxing/browser` (lazy-loaded — the ~450 KB decoder bundle only ships
when a user actually scans). Browsers gate `getUserMedia` on a **secure
context**, so the scanner only works at:

- `http://localhost` / `http://127.0.0.1` (during development), or
- `https://…` URLs (production).

Plain HTTP on a LAN address (e.g. `http://192.168.x.x:8080`) will surface
"Camera access requires a secure context" instead of a viewfinder. To
test from a phone, terminate TLS at a reverse proxy (Caddy, Traefik,
Nginx + Let's Encrypt) or generate a local cert with
[`mkcert`](https://github.com/FiloSottile/mkcert).

Backend dispatch:

- **Music** → MusicBrainz `release?query=barcode:CODE` (no UPC round-trip).
- **Movies / Games** → UPCitemdb's free trial endpoint resolves the code
  to a product title, then TMDB / IGDB run their normal title search.
  UPCitemdb is rate-limited (~100 lookups/day on the free tier); every
  result is cached in `LookupCache` so a re-scan is free.

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for the git-hooks setup, the
conventional-commit rules that drive the changelog (and the two traps that
silently cost a release entry), and the reviewing checklist.

### Releases & container images

Two stages, deliberately separate — **CI publishes an image per merge; the release PR turns one into a version:**

1. **Every merge to `main`** runs [`ci.yml`](.github/workflows/ci.yml), which builds the image, Trivy-scans it, boot-tests it against a throwaway Postgres, and publishes it under the commit it came from: `ghcr.io/mforce/collectify:sha-<commit>` — plus a build-provenance attestation.
2. **`release-please` opens a "Release vX.Y.Z" PR**, accumulating a changelog from the conventional commits since the last release. **Merging that PR** drafts the release, **promotes** that commit's already-scanned image to `:vX.Y.Z` (a server-side retag of the exact digest — never a rebuild), then publishes the release.

The version tag therefore always points at bytes CI already gated. The release stays a draft until promotion succeeds, so a failed promotion leaves no tag pointing at nothing.

> **Images are multi-arch (`linux/amd64` + `linux/arm64`)**, so they pull on Raspberry Pi, Apple Silicon, and Graviton as well as x86. amd64 is scanned *and* boot-tested; arm64 is built from the same source and scanned but not boot-tested — see [`docs/decisions/113-releases.md`](docs/decisions/113-releases.md) for exactly where that boundary sits.

**Deploy by digest, never by tag** — a tag can be moved, a digest cannot. Every release carries an `image.json` asset with the exact reference:

```bash
gh release download vX.Y.Z -p image.json -R mforce/collectify
# → {"reference": "ghcr.io/mforce/collectify@sha256:…", …}
```

**Verify the bytes came from this repo's CI before deploying.** The attestation is checkable without trusting the release notes:

```bash
# requires a prior `docker login ghcr.io` — an oci:// subject needs registry credentials
gh attestation verify oci://ghcr.io/mforce/collectify@sha256:… \
  --repo mforce/collectify \
  --signer-workflow mforce/collectify/.github/workflows/ci.yml \
  --source-ref refs/heads/main \
  --bundle-from-oci
```

All three flags are load-bearing: `--signer-workflow` binds to the workflow (not just the repo), `--source-ref` binds to the branch it ran from, and `--bundle-from-oci` reads the attestation stored beside the image in the registry.

> Publishing to a registry other than GHCR: set `REGISTRY` / `IMAGE_NAME` repository **variables** and `REGISTRY_USER` / `REGISTRY_TOKEN` **secrets**. Provenance attestation requires a registry that stores OCI referrers.

## Roadmap

See [GitHub issues](https://github.com/mforce/collectify/issues) for the active roadmap. Phases:

- **Phase 1 — Foundation** (done): scaffolding, auth, manual CRUD, Docker.
- **Phase 2 — Internet lookup** (done): TMDB / MusicBrainz / IGDB providers + cover caching.
- **Phase 3 — Barcode scanning** (done): in-browser webcam UPC scan with `@zxing/browser`.
- **Phase 4 — Multi-user.**
- **Phase 5 — Photo-snap visual lookup** (future).
