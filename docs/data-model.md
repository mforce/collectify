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
| `Genres` | string? | in code | Comma-separated; future: normalize to a Genre table |
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
| `IsDigital` | bool | in code | |
| `DigitalStore` | enum? | in code | `Steam` / `Gog` / `Epic` / `Xbox` / `Psn` / `Nintendo` / `Other`. Required when `IsDigital`; ignored when not |
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

## Enums (planned)

```csharp
public enum CollectionStatus  { Owned, Wishlist, OnOrder, Sold }
public enum Condition         { New, LikeNew, Good, Fair, Poor }
public enum WatchStatus       { Unwatched, Watching, Watched }
public enum CompletionStatus  { NotStarted, Playing, Beaten, HundredPercent, Abandoned }
```

All enums serialize as **strings** in JSON (already configured globally via `JsonStringEnumConverter`).

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
- **Cast list for movies, tracklist for music, full company-roles for games** — providers expose these; we'll keep the full provider response in `LookupCacheEntry.JsonResponse` so we can mine it later without re-hitting the API.
- **Many-to-many `Genre`** — staying with CSV until a filter UI demands it.
