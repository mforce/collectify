# Data model

The fields tracked per collection category. Decided in the Phase 1 spec discussion; implemented incrementally — what's already in code today is marked **(in code)**, the rest is **(planned)** and will land in a follow-up migration before Phase 2.

## Conventions

- All datetime columns are UTC (`DateTime`) unless explicitly a `DateOnly` (no time component, e.g. acquisition / play dates).
- Money is stored as `decimal(18,2)`; currency as ISO 4217 `string(3)`.
- Strings without a length are stored as SQLite `TEXT`. Set `MaxLength` only where it expresses a real domain rule (titles 500, etc.).
- `OwnerId` is on every collection row from day one (multi-user-readiness).
- Identifier nullability: external provider IDs (`TmdbId`, `MusicBrainzReleaseId`, `IgdbId`, …) are nullable — manual entries don't have them.

## Shared fields (live on Movies, MusicAlbums, Games)

| Field | Type | Status | Notes |
|---|---|---|---|
| `Id` | int (PK) | in code | |
| `OwnerId` | string (FK → AspNetUsers) | in code | Indexed; required filter on every read/write |
| `Title` | string (req, 500) | in code | Indexed |
| `Year` | int? | in code | Release / publication year |
| `Genres` | many-to-many → `Genre` | in code | Relational, like Tags — see **Genres** below |
| `Barcode` | string? | in code | Indexed; key for Phase 3 scan |
| `ImagePath` | string? | in code | Local cover/poster path (downloaded in Phase 2) |
| `Notes` | string? | in code | Free-form |
| `Description` | string? | planned | Short synopsis / blurb (from provider) |
| `PersonalRating` | int? (1–10) | planned | `null` = unrated. Validation: 1 ≤ x ≤ 10 |
| `Status` | enum | planned | `Owned` / `Wishlist` / `OnOrder` / `Sold` (default `Owned`) |
| `Condition` | enum? | planned | `New` / `LikeNew` / `Good` / `Fair` / `Poor` |
| `AcquiredOn` | DateOnly? | planned | Purchase / acquisition date |
| `AcquisitionPrice` | decimal(18,2)? | planned | |
| `AcquisitionCurrency` | string(3)? | planned | ISO 4217 (e.g. `EUR`, `USD`); default from a future user setting |
| `AcquisitionSource` | string? | planned | Free text: "Amazon", "local store" |
| `Tags` | many-to-many → `Tag` | planned | See **Tags** below |
| `AddedAt` | DateTime | in code | UTC, set on insert |
| `UpdatedAt` | DateTime | in code | UTC, bumped on update |

## Movies — `Movie`

Shared fields plus:

| Field | Type | Status | Notes |
|---|---|---|---|
| `OriginalTitle` | string? | in code | |
| `Director` | string? | in code | |
| `RuntimeMinutes` | int? | in code | |
| `Studio` | string? | in code | |
| `Formats` | flags enum | in code | `Dvd \| BluRay \| UhdBluRay`; multi-select |
| `TmdbId` | string? | in code | Primary movie provider |
| `ImdbId` | string? | in code | Cross-reference |
| `WatchStatus` | enum | planned | `Unwatched` / `Watching` / `Watched` (default `Unwatched`) |
| `LastWatchedOn` | DateOnly? | planned | |
| `WatchCount` | int | planned | Default 0 |

## Music — `MusicAlbum`

Shared fields plus:

| Field | Type | Status | Notes |
|---|---|---|---|
| `ArtistName` | string (req, 500) | in code | Indexed |
| `Format` | enum | in code | `Cd` / `Vinyl` / `Other` |
| `Label` | string? | in code | |
| `MusicBrainzReleaseId` | string? | in code | Primary provider key |
| `DiscogsId` | string? | in code | Secondary provider |
| `ListenCount` | int | planned | Default 0 |
| `LastPlayedOn` | DateOnly? | planned | |

## Videogames — `Game`

Shared fields plus:

| Field | Type | Status | Notes |
|---|---|---|---|
| `Platform` | string? | in code | Free text for now (`PS5`, `Switch`, `PC`, …); revisit if filters require an enum |
| `Publisher` | string? | in code | |
| `Developer` | string? | in code | |
| `DigitalStores` | `[Flags]` enum (int) | in code | Bitmask of the storefront(s) a digital copy is owned on: `None`=0, `Steam`=1, `Gog`=2, `Epic`=4, `Xbox`=8, `Psn`=16, `Nintendo`=32, `Other`=64. A game can own several (e.g. Steam|Gog = 3); "is digital" is derived (`DigitalStores != None`). Written as an int bitmask via the DTO (validated against defined bits); the enum is `[Flags]`. (`IsDigital` + single `DigitalStore` were merged into this in #91.) |
| `IgdbId` | string? | in code | Primary game provider |
| `CompletionStatus` | enum | planned | `NotStarted` / `Playing` / `Beaten` / `HundredPercent` / `Abandoned` (default `NotStarted`) |
| `HoursPlayed` | int? | planned | |
| `LastPlayedOn` | DateOnly? | planned | |

## Tags

Many-to-many, scoped per owner.

```csharp
public class Tag
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty; // indexed; tags are user-scoped
    public string Name { get; set; } = string.Empty;    // unique per (OwnerId, Name)
}
```

Three EF-Core-managed join tables (auto-created via `HasMany(x => x.Tags).WithMany()`):

- `MovieTag(MovieId, TagId)`
- `MusicAlbumTag(MusicAlbumId, TagId)`
- `GameTag(GameId, TagId)`

`Tag.Name` is normalized lowercase on save and presented case-preserved via a separate `DisplayName` column **only if** we need case-preserving display — for now lowercase normalization on save is enough; display the same lowercase string. Decide later if this becomes annoying.

API: `GET/POST/DELETE /api/tags`, plus `tags: string[]` on each item DTO that resolves on save (create-or-find by name).

## Genres

Many-to-many, scoped per owner — same shape as **Tags** above.

```csharp
public class Genre
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty; // indexed; genres are user-scoped
    public string Name { get; set; } = string.Empty;    // unique per (OwnerId, Name)
}
```

Three EF-Core-managed join tables (auto-created via `HasMany(x => x.Genres).WithMany()`):

- `GenreMovie(GenresId, MoviesId)`
- `GenreMusicAlbum(GenresId, MusicAlbumsId)`
- `GameGenre(GamesId, GenresId)`

`Genre.Name` is normalized lowercase on save (trim → lowercase → drop whitespace-only → distinct), mirroring `Tag.Name`.

API: `genres: string[]` on each item DTO, resolved on save (create-or-find by name), plus a bulk-update `genres` replace-set (`null` clears, malformed → 400). The list endpoints' `?genre=` filter is **exact membership**, not substring.

The migration that introduced this (`AddGenres`, owner decision 2026-08-24) was **schema-only**: the legacy comma-separated `Movies.Genres` / `MusicAlbums.Genres` string columns were dropped with no data backfill — existing genre values were discarded, not migrated.

## Store imports / provenance (in code — Steam first)

Backing tables for the "connect a digital store & import owned games" feature
(see `docs/platform-import.md`). Owner-scoped; sessions and connections are
per-user now, and the design is store-generic so Xbox / PlayStation can reuse
the same shape later.

`GameStoreConnection` — one linked store account per `(OwnerId, Store)`:

| Field | Type | Notes |
|---|---|---|
| `Id` | int (PK) | |
| `OwnerId` | string → AspNetUsers | |
| `Store` | `DigitalStore` | `Steam` today |
| `ExternalAccountId` | string | e.g. SteamID64 |
| `ExternalDisplayName` | string? | persona name |
| `LinkedAt` | DateTime | UTC |

`GameStoreOwnedTitle` — the import ledger / provenance. **This is the
idempotency source of truth:** one row per `(OwnerId, Store, ExternalGameId)`.
A row whose `GameId` is null means "was imported, then the user deleted the
Game from their collection" — it stays so a re-import (or reconnect) can't
duplicate the title.

| Field | Type | Notes |
|---|---|---|
| `Id` | int (PK) | |
| `OwnerId` | string → AspNetUsers | |
| `Store` | `DigitalStore` | |
| `ExternalGameId` | string | Steam appid |
| `ExternalAccountId` | string | account it came from |
| `Title` | string | snapshot at import time |
| `GameId` | int? → `Games` | composite FK `(GameId, OwnerId)` → `Games(Id, OwnerId)` with **Restrict** delete; nulled (not deleted) when the user deletes the game |
| `ImportedAt` / `UpdatedAt` | DateTime? | UTC |

`SteamAuthRequest` — one-time OpenID auth attempts (state + owner). The stored
`StateHash` is the SHA-256 of `state + ":" + cookieHalf` so a leaked
`return_to` alone can't complete a link.

| Field | Type | Notes |
|---|---|---|
| `Id` | int (PK) | |
| `StateHash` | string (unique) | combined OIDC state + cookie hash |
| `OwnerId` | string | the user beginning the link |
| `CreatedAt` / `ExpiresAt` | DateTime | |
| `Consumed` | bool | single-use |

Imported `Game` rows default to `Platform = Pc`, `DigitalStores = Store`
(the bitmask of the storefront it was imported from), `Status = Owned`,
`HoursPlayed` from playtime, and `AcquisitionSource = "Steam Import"`.

### DLC / add-on (schema hook, provider-agnostic)

Steam, Xbox and PSN all model DLC as a **separate product linked back to a base
game**, so one self-reference covers every storefront:

- `Game.ParentGameId` (int?, self FK → `Games.Id`, `Restrict`) — a DLC game
  points at its base game. Same-owner semantics are enforced by the
  ownership-preserving composite FK pattern applied to the ledger.
- `GameStoreOwnedTitle.ParentExternalGameId` (string?, e.g. Steam parent appid)
  — recorded at import time so a later DLC→parent backfill can populate
  `Game.ParentGameId` **without re-importing or re-fetching** per app.

This iteration keeps imports **flat**: no auto-linking of DLC to parents.
`ParentGameId` is null for today's imports, and the DLC-grouping "auto-link +
manual link" UI is a separate follow-up (needs the per-provider parent metadata
Steam serves under `/appdetails`, which is rate-limited and not available for
whole libraries in v1).

## Enums (planned)

```csharp
public enum CollectionStatus  { Owned, Wishlist, OnOrder, Sold }
public enum Condition         { New, LikeNew, Good, Fair, Poor }
public enum WatchStatus       { Unwatched, Watching, Watched }
public enum CompletionStatus  { NotStarted, Playing, Beaten, HundredPercent, Abandoned }
```

All enums serialize as **strings** in JSON (already configured globally via `JsonStringEnumConverter`), with two `[Flags]` exceptions that serialize as **integer bitmasks**:

- **`MovieFormat`** — e.g. `3` for Dvd | BluRay. The DTO carries it as `int` so the frontend can use bitwise ops to read/write checkbox state.
- **`DigitalStores`** — a per-game bitmask of virtual storefronts, e.g. `5` for Steam | Epic. The DTO (`GameDto.DigitalStores`) carries it as `int` for the same reason. `0` = physical.

All other enums remain string-serialized.

## Migration plan

The current code ships with the **(in code)** subset only. The **(planned)** additions are one EF migration:

```
dotnet ef migrations add AddPersonalAndAcquisitionFields \
  --project Collectify.Infrastructure \
  --startup-project Collectify.Api \
  --output-dir Data/Migrations
```

Order of work:

1. Add the new properties and `Tag` / per-type tag join configurations to the entities.
2. Add the new enums under `Collectify.Domain/Enums/`.
3. Add the migration; verify it round-trips via `Migrate()` on a fresh SQLite.
4. Extend the DTOs and minimal-API endpoints to accept / return the new fields, including `tags: string[]`.
5. Extend the React forms (one new "Personal" section per form, one new "Acquisition" section, a Tag input component reused across forms).
6. Update list rendering: show rating + status pill + tag chips on cards.
7. Tests: ownership scoping for tags, rating bounds (`1..10`), enum string round-trip.

Tracked separately from Phase 1 in its own GitHub issue (filed as a follow-up to PR #5).

## What we're explicitly **not** doing yet

- **Loan tracking** (`LoanedTo` / return date / history) — the `Status` enum doesn't include `Loaned` for that reason. Add later if you want it.
- **Goldmine media + sleeve grading for music** — sticking with one generic `Condition` field.
- **Region / Edition / Packaging / DiscCount / HasManual / HasBox / vinyl color / RPM / weight** — collector-tier fields, deferred.
- **Cast list for movies, tracklist for music, full company-roles for games** — providers expose these. Full provider payloads are not persisted in the application database. The memory cache is restart-ephemeral; opted-in Redis stores TTL-bounded payloads at rest and must be secured. A full re-fetch after cache expiry re-queries the provider.
