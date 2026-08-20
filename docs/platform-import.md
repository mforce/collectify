# Platform game import — Connect Steam & import owned games

Status: **Spec v3 (revised after independent Codex + Claude reviews)**
Scope: **Steam-concrete now**; the store-import service is shaped so Xbox / PlayStation can be
added later as their own endpoint groups (see "Provider seam decision").

Let a user connect their digital gaming account to Collectify so the games they already own
can be imported into their collection instead of being entered by hand.

> **Review history**
> - **v1** — first draft.
> - **v2** — folded in the Codex/OpenAI review (all 3 Critical + 10 Should-fix).
> - **v3** — folded in the Claude Code review (3 Critical + 13 Should-fix). See
>   [Review reconciliation](#review-reconciliation).

## Goals

- Connect a user's Steam account once (auth via Steam OpenID).
- Fetch the user's owned games from the Steam Web API.
- Let the user review and select which owned games to import (preview before committing).
- Save them as normal `Game` rows (`DigitalStores = Steam`).
- Re-running an import is **idempotent** — already-imported games are never duplicated, even
  after a disconnect + reconnect.
- Imported games can be deleted normally without 500ing (see C1).

## Non-goals (this iteration)

- Linking multiple accounts of the same store (single Steam link per user).
- Automatic periodic sync (a manual "Sync" button is enough; a cron can come later).
- Xbox / PlayStation connectors — see "Provider seam decision"; their auth differs entirely.
- Programmatic DLC/demo filtering — Steam's `GetOwnedGames` gives **no reliable app-type
  discriminator**, so **user selection is the only filter** (confirmed). The only type source,
  `store.steampowered.com/api/appdetails`, is undocumented and rate-limited (~200 req/5min) —
  unusable per-app for a large library.

## Feasibility note (why Steam only right now)

- **Steam** — official, documented, stable: Steam Web API key + OpenID login +
  `IPlayerService/GetOwnedGames`. What we build.
- **Xbox** — no official "owned games" API for third-party apps; only unofficial Xbox Live web
  APIs (xapi.us / xbl.io / OpenXbox) needing a captured Microsoft token. Brittle.
- **PlayStation** — no official public API for a user's owned library; only unofficial/undocumented
  endpoints. Highest fragility and ToS risk.

## Provider seam decision (S5, S4)

The v2 plan put a generic `IPlatformGameImportProvider` in Domain. Claude's review (S5) correctly
called out that this was the worst of both worlds: five hardcoded `/api/accounts/steam/*` routes
already, while a Domain-level provider-neutral interface added abstraction with no payoff — and
the OpenID leg isn't behind the seam anyway (Xbox/PSN need entirely different auth endpoints).

**Resolution — Steam-concrete now.** Map `Endpoints/SteamStoreEndpoints.cs` with the
`/api/accounts/steam/*` group. Put the shared, genuinely-reusable logic in one Infrastructure
service `SteamStoreImportService` (fetch + ledger + transaction). No Domain-level provider
interface is introduced — this matches `architecture.md:141-148` ("no service interfaces purely for
swappability… don't pre-introduce"). When an Xbox/PS connector is real, it gets its own
`XboxStoreEndpoints` / service and its own auth flow; the `GameStoreConnection` /
`GameStoreOwnedTitle` tables already carry the `Store` discriminator they need.

## Data model

### `GameStoreConnection` (new entity)

One row per (owner, store). Represents a linked/connected digital store account.

| Field | Type | Notes |
|---|---|---|
| `Id` | int (PK) | |
| `OwnerId` | string (required) | FK → AspNetUsers |
| `Store` | `DigitalStore` | `Steam` now; `Xbox`, `Psn` later |
| `ExternalAccountId` | string (required, MaxLength) | SteamID64; decimal string |
| `ExternalDisplayName` | string? (MaxLength) | Steam persona (GetPlayerSummaries, best-effort) |
| `LinkedAt` | DateTime (UTC) | |
| `LastSyncedAt` | DateTime (UTC)? | set on successful fetch; for sync later |

Constraints: `MaxLength` on string fields; **unique index `(OwnerId, Store)`** → one link per
owner/store. `OwnerId` is the leading column of that unique index, so **no separate `OwnerId`
index** (Claude S12).

> Cross-user uniqueness `(Store, ExternalAccountId)` — one external identity → one user — is a
> **product decision**, **default: not enforced** (self-hosted app is typically single-user;
> enforcing adds a cross-owner global unique complicating future multi-user).

### `GameStoreOwnedTitle` (new entity) — import ledger / provenance

Source of truth for "which store titles are already in my collection", doubling as the
**idempotency + provenance** record.

| Field | Type | Notes |
|---|---|---|
| `Id` | int (PK) | |
| `OwnerId` | string (required) | |
| `Store` | `DigitalStore` | |
| `ExternalGameId` | string (required, MaxLength) | Steam `appid`, canonical decimal string |
| `ExternalAccountId` | string? (informational) | which linked account the title came from (Claude NI) — **not** part of the idempotency key |
| `Title` | string (required, MaxLength) | from provider; **truncated to 500** (Game.Title cap) |
| `GameId` | int? | FK to `Games` |
| `ImportedAt` | DateTime (UTC)? | set when imported |
| `UpdatedAt` | DateTime (UTC) | bumped on change |

Unique index on **(OwnerId, Store, ExternalGameId)** — the natural idempotency key. `OwnerId` is
the leading column; **no separate `OwnerId` index** (S12).

Ledger state (linear):
- Row **absent** → importable (never seen).
- Row present with `GameId != null` → **imported**.
- Row present with `GameId == null` → the linked game was **deleted** (the only way to reach this
  state now — skips were removed in v2 and are **not** reintroduced here). It is importable again
  on a re-run.

### `Games` — one small change (C1)

Claude C1: a composite FK `(GameId, OwnerId) → Games(Id, OwnerId)` with a **nullable** `GameId` and
**required** `OwnerId` breaks `DELETE /api/games/{id}`. `SetNull` on a composite FK nulls *every*
child column (including `OwnerId`) → violates `NOT NULL`; `NoAction` fails the delete outright. The
existing delete path (`GamesEndpoints.cs:140-148`) has no knowledge of the ledger → first imported
game deleted 500s.

**Resolution (Claude's preferred fix):**
- Add composite alternate key `(Id, OwnerId)` on `Games`; `GameStoreOwnedTitle.GameId` becomes a
  composite FK `(GameId, OwnerId) → Games(Id, OwnerId)` with **`DeleteBehavior.Restrict`** (no
  auto-SET-NULL).
- **Change `GamesEndpoints.cs` DELETE**: in an owner-scoped transaction, first set
  `GameStoreOwnedTitle.GameId = null` for rows whose `(GameId, OwnerId)` matches, then delete the
  `Game`. This nulls only `GameId`, keeping `OwnerId` intact. **This endpoint change is in scope
  for this feature** and is called out explicitly.
- Net: DB-enforced owner integrity (a ledger row can only reference a game owned by the same user)
  AND a working delete path.

## Steam flow

### 1. Connect

- `GET /api/accounts/steam/connect` (auth) → `{ configured, redirectUrl }`.
- Server generates an **opaque random `state`**; stores only its **hash** in a `SteamAuthRequest`
  row (`OwnerId`, one-time, expiry ~10 min, consumed flag).
- **Second factor (Claude S1):** also set a short-lived `HttpOnly`, `SameSite=Lax`, `Secure`
  cookie holding a random second half, so a leaked `state` URL alone can't complete a link.
- `return_to = {PublicBaseUrl}/account/steam/authenticated?state=<STATE>`.
- `redirectUrl` → `https://steamcommunity.com/openid/login` with standard OpenID 2.0 params.

(Review C1-v1 applies: state rides in `return_to`, NOT in `openid.nonce`/`assoc_handle`.)

### 2. OpenID callback (public)

`GET /account/steam/authenticated` is **public** (browser is on Steam's domain mid-flow). It is a
GET that mutates state, safe **only** because it's guarded by a fully-verified OpenID assertion +
one-time state + the browser-bound cookie. **Never** take `OwnerId` from request params or fall
back to "the only user."

**Verification order (Claude S2 — cheap local checks FIRST, then network):**

1. `openid.mode == "id_res"`, correct OpenID namespace.
2. `openid.op_endpoint` equals the exact known/hard-coded Steam endpoint.
3. `openid.return_to` byte-for-byte equals the expected callback URL (incl. `state`).
4. The bound cookie is present and matches; **`state` exists, is unexpired, unconsumed, belongs to
   one owner.** (Local, no network.)
5. *(Per-IP rate limit on this endpoint.)*
6. **Direct verification**: POST the complete OpenID response back to the hard-coded Steam endpoint
   with `openid.mode=check_authentication`, echo **every** `openid.*` param received (incl.
   `openid.signed` and `openid.sig`) — only `mode` changes — and require `is_valid:true`.
7. **`openid.signed` coverage (Claude C2):** assert the signed-list actually contains
   `op_endpoint`, `return_to`, `response_nonce`, `assoc_handle`, `claimed_id`, `identity`.
8. `openid.identity == openid.claimed_id`; claimed ID's terminal path segment is a positive 64-bit
   decimal SteamID64 (reject lookalike hosts, extra segments, query/fragment, prefix-only).
9. `openid.response_nonce` freshness — **replay is already covered by the one-time `state`**, so we
   do **not** add a separate nonce table (Claude S3). Replayed assertions carry a consumed `state`
   and are rejected at step 4.
10. **Atomically consume** the `state` in the same transaction that upserts the connection
    (and clears the cookie).

On success: upsert `GameStoreConnection`, fetch persona name best-effort, log the link event
(Information), respond with `Cache-Control: no-store` on the 302, redirect to the fixed
`{PublicBaseUrl}/import/steam`. On failure: redirect to `{PublicBaseUrl}/import/steam?steam=error`
with no state mutation.

### 3. Fetch owned games (preview)

`GET /api/accounts/steam/games` (auth). Backend reads the user's `GameStoreConnection`, then calls:

```
IPlayerService/GetOwnedGames/v1?key=…&steamid=<steamid64>&include_appinfo=true&include_played_free_games=true&format=json
```

- **Trusted source of truth:** ownership + titles come **only** from this provider response; the
  server never trusts client-supplied titles or IDs for what the account owns.
- **Caching (Claude S6):** route this via `ILookupCache`, keyed on **SteamID64** (never on
  `OwnerId` alone — the cache is a shared memory/Redis store keyed by `(Provider, Key)`), with a
  **short TTL** (minutes, e.g. 5), overriding the 30-day default. A user's private library must not
  sit in the shared cache for 30 days (privacy + staleness); with Redis the cached Steam library
  payload at rest must be treated as private data.
- **Profile-visibility caveat:** `GetOwnedGames` returns `{"response":{}}` for both a private
  profile and an empty library. Show a **qualified** message ("No games returned — your game
  details may be private"), not a hard diagnosis.
- **`include_played_free_games=true`** (confirmed) widens results beyond purchased ownership; UI
  discloses some rows may be played-free titles.
- Response: ordered list of `{ externalGameId, title, playtimeMinutes, iconUrl, state }`
  (`state` = `imported | importable`), **ordered by playtime desc** (Claude S9).

### 4. Import selected

`POST /api/accounts/steam/import` with `{ externalGameIds: string[] }`.

- The server **re-fetches `GetOwnedGames`** from the trusted source and selects requested IDs from
  that result; it will not import an ID the account doesn't own. **Do not trust client titles.**
- Validate: cap array size (e.g. ≤ 500), reject non-`uint32` IDs, dedupe, drop IDs absent from the
  trusted fetch. Re-fetches go through the same short-TTL cache as the preview.

Processing, per ID, in one DB transaction (v1 SF3):
- Create the `Game` row (`Title` = Steam name **truncated to 500**, `DigitalStores=Steam`,
  `Platform=Pc`, `AcquisitionSource="Steam Import"`, and from the preview
  `HoursPlayed` = `playtime_forever` — Claude NI) **and** the `GameStoreOwnedTitle` row together.
- Handle the unique-constraint race by re-reading the winning ledger row on conflict (reuse the
  already-created game; never duplicate). A failed batch never leaves an orphaned `Game`.
- Do **not** set `IgdbId` (Steam appid ≠ IGDB id). **Cover/enrichment** per Claude NI (below) is a
  separate bounded step, not part of the transactional import.
- Return per-ID summary `{ imported, alreadyImported, notOwned, invalid }` + created games.

Return 400 if the selection would exceed the cap even nominally; the UI enforces the cap visibly.

### 5. Sync (later, out of scope)

`LastSyncedAt` is on the connection for a future `POST /api/accounts/steam/sync`. Not built now.

## Platform mapping note

`GamePlatform.Pc` (value `1`) already exists and `GamePlatformMapping` resolves
`"pc"`/`"windows"`/`"microsoft windows"` to it. Import sets `Platform = GamePlatform.Pc`, no enum
change, no `SteamDeck` inference (account ownership ≠ device). Broad PC-family modeling is tracked
in #102 / #103.

## Config (`.env.example` additions)

New top-level section `Collectify:Platforms:Steam:ApiKey` (Claude NI — a stated decision: this is
not metadata lookup, so it gets its own section, documented in `.env.example` in the same commented
style as the other providers), plus `Collectify:PublicBaseUrl`.

| Variable | Default | Purpose |
|---|---|---|
| `Collectify__PublicBaseUrl` | `http://localhost:5173` (dev) | Base URL for Steam OpenID `return_to` / SPA redirects |
| `Collectify__Platforms__Steam__ApiKey` | *(empty)* | Steam Web API key |

Steam is fail-soft: `ApiKey` empty → `configured=false` → "set Steam ApiKey to enable" hint.

### `PublicBaseUrl` — fail-soft (Claude S11)

Do **not** crash app boot on a malformed URL (every other provider degrades to `configured=false`).
Validate and, if invalid, report `configured:false` with a diagnostic instead. When valid: require
absolute http/https, reject userinfo/query/fragment/unexpected base paths, never accept a
request-provided post-login redirect (no open redirect), and never log the `state` (it's a one-time
secret; note reverse-proxy logs are outside the app's control — hence the browser-bound cookie in
S1).

## API surface (summary)

Public:
- `GET /account/steam/authenticated` — OpenID callback (no cookie auth), outside the
  `RequireAuthorization()` group (Claude S4), guarded per §2.

Authenticated (`RequireAuthorization`):
- `GET    /api/accounts/steam/connect` → `{ configured, redirectUrl }`
- `GET    /api/accounts/steam` → `{ connected, personaName, steamId }`
- `GET    /api/accounts/steam/games` → owned games preview (trusted fetch, short-TTL cached)
- `POST   /api/accounts/steam/import` → import selected (rejects IDs not in trusted fetch)
- `DELETE /api/accounts/steam` → disconnect

**Changed existing endpoint (in scope):**
- `DELETE /api/games/{id}` — owner-scoped transaction that nulls matching ledger `GameId`s first,
  then deletes the game (C1). No behavior change for non-imported games.

### Ownership integrity — every operation uses the full key

- Connection lookup: `(OwnerId, Store)`.
- Preview / import ledger join: `(OwnerId, Store, ExternalGameId)`.
- Any `Game` lookup or ledger write: includes `OwnerId`.
- Disconnect deletion: `(OwnerId, Store)` only — never the ledger.

### Disconnect (v1 C3 — keep provenance)

Delete **only** `GameStoreConnection`. **Keep** `GameStoreOwnedTitle` rows + imported `Game` rows.
Deleting the ledger would destroy idempotency (every retained game would look new on reconnect).
Keep the `(OwnerId, Store, ExternalGameId, GameId)` provenance so reconnect + re-sync marks
already-imported games correctly.

- **FK delete behavior:**
  - **Disconnect**: delete connection only; ledger + games survive.
  - **Deleting an imported game**: handled explicitly in the games DELETE (null `GameId` → row
    reverts to "not imported / importable", never "skipped").
  - **Deleting a user**: **removed claim** — the repo has no user-delete endpoint and `Game.OwnerId`
    is a bare indexed string with no FK to `AspNetUsers` (Claude S10). No cascade is specified;
    this is pre-existing and out of scope.

## Frontend

New `/import/steam` page (route in `App.tsx`), reached from a header button on `GamesList`:

1. **Not connected** → "Connect Steam" → `GET connect` → `window.location = redirectUrl`; or "set
   Steam ApiKey to enable" hint if `configured=false`.
2. **Connected** → "Fetch games" → list of owned games (cards/checkboxes, `ui.tsx` primitives),
   ordered playtime-desc, with search + filter + **select-all that respects the import cap with a
   visible count** (Claude S9 — a 900-game library must not render 900 flat cards and silently 400
   on select-all). Badges: `Imported` / `Importable`. Qualified empty-state message.
3. **Import** → `POST import` → toast summary → navigate to `/games`.
4. Entry point: header button on `GamesList`.

TanStack Query keys: `['steam', 'list', 'games']` for the preview. On import, invalidate **both**
`['games']` **and** `['steam']` (Claude S13 — the import-state badges come from `['steam', ...]`).

### Dev-mode flow (Claude S8)

`PublicBaseUrl` defaults to `http://localhost:5173` (the Vite dev server, not :5041 — the API has
no `wwwroot` in dev so `MapFallbackToFile` never registers). The Vite proxy only forwards `/api`
and `/covers`, so the `/account/steam/authenticated` callback would 404 in dev. Add a Vite proxy
entry for the callback path (documented like the `/covers` one), **or** move the callback under
`/api/steam/authenticated` with explicit anonymous access (safe: `Program.cs:47-51` returns 401
for unauthenticated `/api` paths, harmless for an already-anonymous endpoint). Choose the Vite proxy
entry; keep the canonical callback path stable.

## Tests

**Domain** — ledger state derivation; `GamePlatform.Pc` mapping; SteamID64 validation.

**Steam provider** (unit, `WireMock.Net`): `GetOwnedGames` parsing, hidden/empty response, timeout,
non-success status, malformed JSON, cancellation. **No "401 retry"** (Steam key is static; fail
soft, log status **without the key**, return a stable provider error).

**OpenID callback** (integration, `WebApplicationFactory<Program>`): end-to-end connect → games →
import vs a faked Steam; plus security cases (v1 SF9/NI): expired/unknown/replayed state; missing
or mismatched bound cookie; modified `return_to`; bad `op_endpoint`; missing `openid.signed`
coverage; reused `response_nonce`; invalid/mismatched identity/claimed ID; cross-owner `GameId`;
concurrent double import.

**Delete path (Claude C1)**: deleting an imported game succeeds and leaves an importable ledger row;
never a 500; non-imported games unaffected.

**Endpoints** (integration): auth required on `/api/accounts/steam/*`; disconnect deletes the
connection but **keeps** ledger + games; reconnect doesn't re-import; ownership scoping.

**Client** (Vitest): `GamesList` header shows Connect when unconnected; `ImportSteam` page
render/select/import mock flow, incl. select-all-respects-cap.

## Migration

One EF migration `AddStoreImport`:

- `GameStoreConnection` (+ unique `(OwnerId, Store)`).
- `GameStoreOwnedTitle` (+ unique `(OwnerId, Store, ExternalGameId)`).
- `SteamAuthRequest` (one-time state; hash key or unique, OwnerId, expiry, consumed).
- Alternate key `(Id, OwnerId)` on `Games`; composite FK
  `GameStoreOwnedTitle(GameId, OwnerId) → Games(Id, OwnerId)` with **`Restrict`** delete behavior.

**Schema-lifecycle reality (Claude C3):** the repo runs `MigrateAsync()` on SQLite but
`EnsureCreatedAsync()` on Postgres (`Program.cs:88-98`), and `EnsureCreated` is a no-op against an
existing database. So on an **existing Postgres install, the new tables are NOT created** and the
Steam endpoints would 500 on first query. **This is a pre-existing repo limitation** (the
`Program.cs:85-87` comment acknowledges "schema evolution requires a DB reset"), but this is the
first feature to add tables since. **Release note / rollout:** Postgres operators must reset the
database volume to pick up the new tables. The rollout section must NOT promise "no behavior change
for existing Postgres installs."

### SteamAuthRequest cleanup (Claude S7)

`SteamAuthRequest` grows unbounded. Reuse the repo's `CoverImageGarbageCollector.SweepAsync`
pattern (runs at startup, `Program.cs:106`): sweep expired/consumed `SteamAuthRequest` rows the
same way.

## Rollout / flags

- Opt-in by config: Steam key absent → `configured=false`, UI degrades to a hint. No behavior
  change for **SQLite** installs until a key is set.
- **Postgres**: existing installs need a **database reset/volume recreate** to gain the new tables
  (see Migration). This is the accepted schema-lifecycle trade-off of the repo, now surfaced
  explicitly.
- Owner scoping rules apply as everywhere (never query across owners), keeping Phase 4 safe.

## Decisions (confirmed)

1. **DLC / free-to-play**: user-selection-only — show every owned app, user picks. No per-app type
   calls.
2. **Disconnect**: imported `Game` rows **and** the provenance ledger are kept; only the connection
   is removed (protects idempotency).
3. **Provider seam**: Steam-concrete for now (no Domain-level provider interface); shared logic in
   an Infrastructure `SteamStoreImportService`. Xbox/PS later as their own endpoint groups.
4. **Config**: new `Collectify:Platforms:Steam:ApiKey` top-level section (not under `Metadata`).

## Review reconciliation

**Codex (v1 → v2):** all folded (see v2). New mappings for v2→v3 from **Claude**:

- **C1** composite-FK breaks game delete → `Restrict` + explicit null in the DELETE endpoint (in scope).
- **C2** `openid.signed` coverage + full echo → §2 steps 7 & 6.
- **C3** Postgres tables not created → §Migration / §Rollout (reset required).
- **S1** browser-bound second-factor cookie → §1, §2.
- **S2** verification order (local first) + rate limit → §2.
- **S3** nonce replay already covered by one-time state; drop separate nonce table → §2 step 9.
- **S4** provider interface in Infrastructure (matches code) / result records in `Infrastructure/Import/`; callback outside `RequireAuthorization` → §Provider seam, §API.
- **S5** Steam-concrete, drop generic seam → §Provider seam decision.
- **S6** cache `GetOwnedGames` keyed on SteamID64, short TTL → §3.
- **S7** `SteamAuthRequest` sweep → §Migration cleanup.
- **S8** dev-mode callback/proxy + PublicBaseUrl → §Config, §Dev-mode flow.
- **S9** cap-vs-UI: search/filter/select-all ordering → §Frontend.
- **S10** remove false user-cascade claim → §Disconnect.
- **S11** `PublicBaseUrl` fail-soft → §Config.
- **S12** MaxLength + truncation + redundant OwnerId indexes → §Data model.
- **S13** invalidate `['steam']` too → §Frontend.

**Nice-to-have adopted:** `HoursPlayed` from `playtime_forever`; `ExternalAccountId` on ledger;
cover art via deterministic `https://cdn.cloudflare.steamstatic.com/steam/apps/{appid}/header.jpg`
through `ICoverImageStore.EnsureLocalAsync`, bounded (lazy / N per import, not 500 synchronous
downloads); tighten `GameId==null` wording (delete-only); note the ledger `Store`
column asserts the store discriminator explicitly rather than assuming a default; `Cache-Control: no-store` on the
callback; explicit soft-match-against-manual-entries is out of scope (a future preview nicety).
