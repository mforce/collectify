# Phase 5: Photo-snap visual lookup — Design Spec

**Issue:** [#82](https://github.com/mforce/collectify/issues/82)
**Date:** 2026-05-30 (revised 2026-05-30)
**Status:** Approved

## Overview

Capture a photo of physical media cover art → server sends it to Google Cloud
Vision API (TEXT_DETECTION + WEB_DETECTION) → combines OCR text, web entities,
and known-domain URL routing into ranked candidate queries → feeds into existing
metadata providers → returns candidates in the familiar candidate list UI. Same
import flow as barcode and title search.

Works for covers with readable text (OCR path) and covers with little or
no text when WEB_DETECTION finds useful web entities or matching pages
(visual matching). Accuracy for textless covers depends on whether the
cover art exists in Google's web index — obscure, regional, or private
pressings may not match.

All three media types (movies, music, games) ship together.

## Decision log

| # | Decision | Rationale |
|---|---|---|
| 1 | **Server-side processing** | Keeps API keys off the client; fits existing lookup architecture |
| 2 | **`IVisionClient` (single interface)** | Provider-agnostic, swap via DI. Rich result with text + web signals |
| 3 | **Single endpoint, full chain** | `POST /api/lookup/{type}/by-image` orchestrates vision → metadata in one round-trip. Same `LookupResponse<T>` shape |
| 4 | **Separate "Snap cover" button** | Parallel to "Scan barcode" in forms. Different user intent, different lazy-load boundary |
| 5 | **Live camera preview + capture** | Same `getUserMedia` as BarcodeScanner. Confirm/retake step. No crop |
| 6 | **Client-side resize** | ~1200px longest side, JPEG 0.7 via `<canvas>`. Cuts 10 MB → ~300 KB before upload |
| 7 | **Simple preview, no crop** | Vision API works on full image. Crop adds UX complexity for marginal gain |
| 8 | **Multi-signal matching** | OCR text → web entities → known-domain URL routing. Covers textless covers |
| 9 | **Cloud Vision TEXT_DETECTION + WEB_DETECTION** | Same API key, two features. OCR for text-heavy covers, web detection for visual matching |
| 10 | **No caching (spike for image-hash cache later)** | Existing `LookupCache` covers the metadata search step. Image-hash cache impractical now; spike task added |
| 11 | **Explicit "no match" feedback** | Return `hint` on response when all paths exhaust. Guides user to retake or try barcode/title search |
| 12 | **All three media types** | Endpoint and component are generic over `MediaType`. Same cost as shipping one |
| 13 | **Fake `IVisionClient` + integration tests** | Matches existing `FakeUpcClient` / `Stub*Provider` pattern. Full chain via `WebApplicationFactory<Program>` |
| 14 | **Config in `MetadataLookupOptions.Vision`** | Follows existing pattern. Env var: `Collectify__Metadata__Vision__ApiKey` |

## Pricing

Google Cloud Vision API bills per feature per image. Running both
`TEXT_DETECTION` and `WEB_DETECTION` on one photo = 2 billable feature
units. Each feature has its own first-1,000-units/month free tier.

| Feature | Free tier | Beyond free |
|---|---|---|
| TEXT_DETECTION | 1,000/month | $1.50 / 1,000 |
| WEB_DETECTION | 1,000/month | $3.50 / 1,000 |

For a single-user self-hosted app sending both features per photo:
~1,000 photos/month free (each feature independently within its free
allowance). Plenty for manual collection entry.

## Backend

### New files

#### `Infrastructure/Lookup/Vision/IVisionClient.cs`

```csharp
namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>Scored web entity from WEB_DETECTION.</summary>
public record WebEntitySignal(string Description, float Score);

/// <summary>Categorised matching URL from WEB_DETECTION.</summary>
public record MatchingUrlSignal(Uri Uri, string Category);
// Category: "pagesWithMatchingImages" | "fullMatch" | "partialMatch" | "visuallySimilar"

/// <summary>
/// Multi-signal result from analysing a cover photo. Callers combine
/// these signals into ranked queries for the metadata provider.
/// </summary>
public record VisionExtractResult(
    string[] DetectedText,
    float TextConfidence,
    WebEntitySignal[] WebEntities,
    MatchingUrlSignal[] MatchingUrls);

public interface IVisionClient
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<VisionExtractResult> AnalyseAsync(byte[] imageBytes, CancellationToken ct = default);
}
```

Provider-agnostic. `Name` for logging and response attribution. `IsConfigured`
follows the fail-soft contract: false → endpoint returns
`{ configured: false, results: [] }`.

#### `Infrastructure/Lookup/Vision/CloudVisionClient.cs`

Google Cloud Vision API implementation. Sends image bytes to
`images:annotate` with both `TEXT_DETECTION` and `WEB_DETECTION` features.

- Maps `TEXT_DETECTION` → `DetectedText` (individual word/line detections,
  filtering out single characters and very long runs)
- Maps `WEB_DETECTION.webEntities` → `WebEntities` (description + score)
- Maps `WEB_DETECTION.pagesWithMatchingImages` → `MatchingUrls` with
  category `"pagesWithMatchingImages"`
- Maps `WEB_DETECTION.fullMatchingImageUrls` → category `"fullMatch"`
- Maps `WEB_DETECTION.partialMatchingImageUrls` → category `"partialMatch"`
- Maps `WEB_DETECTION.visuallySimilarImages` → category `"visuallySimilar"`

Reads API key from `MetadataLookupOptions.Vision.ApiKey`.
Uses `IHttpClientFactory` with named client `"vision"`.
Logs warnings (not errors) on HTTP failures; returns empty result.

#### `Infrastructure/Lookup/Vision/VisionServiceCollectionExtensions.cs`

```csharp
public static class VisionServiceCollectionExtensions
{
    public static IServiceCollection AddVisionClient(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<CloudVisionClient>("vision")
            .SetHandlerLifetime(TimeSpan.FromMinutes(2));

        services.AddScoped<IVisionClient, CloudVisionClient>();
        return services;
    }
}
```

Called from `Program.cs` after `AddMetadataLookup()`. Options are already
bound by `AddMetadataLookup` — `CloudVisionClient` reads them via
`IOptions<MetadataLookupOptions>`.

#### `Infrastructure/Lookup/Vision/UrlRouter.cs`

Static helper that scans a set of URIs for trusted metadata provider domains
and extracts provider IDs:

```csharp
public static class UrlRouter
{
    // themoviedb.org/movie/27205-inception -> "27205"
    public static string? ExtractTmdbId(Uri uri);

    // musicbrainz.org/release/<mbid> -> MBID
    public static string? ExtractMusicBrainzReleaseId(Uri uri);

    // IGDB has no slug-to-id endpoint; always returns null.
    // Games still match via OCR (Path A) and web entities (Path B).
    public static string? ExtractIgdbId(Uri uri) => null;
}
```

Only parses well-known, stable URL patterns. Unknown domains are ignored.
TMDB numeric IDs and MusicBrainz MBIDs are high-confidence. IGDB has no
slug-to-id endpoint, so URL routing for games is intentionally disabled;
games still match via OCR and web entity paths.

### Modified files

#### `MetadataLookupOptions.cs`

Add `Vision` subsection:

```csharp
public sealed class VisionOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://vision.googleapis.com/v1/";
}

// In MetadataLookupOptions:
public VisionOptions Vision { get; set; } = new();
```

#### `LookupEndpoints.cs`

Three new routes inside the existing `/api/lookup` group:

```
POST /api/lookup/movies/by-image
POST /api/lookup/music/by-image
POST /api/lookup/games/by-image
```

Content-Type: `multipart/form-data` with field `file`.

**Handler logic** (per media type, same structure, different provider interface):

1. Validate upload: non-empty, ≤ 5 MiB, JPEG/PNG/WebP, magic-byte check
   (reuse shared upload validation helper — see refactoring note below)
2. If `IVisionClient.IsConfigured == false` → return
   `LookupResponse(provider.Name, configured: false, results: [], hint: null)`
3. Call `visionClient.AnalyseAsync(bytes, ct)`
4. **Collect candidates from all applicable paths:**
   - **Path A — OCR text search:** Filter `DetectedText` (drop < 2 chars,
     > 60 chars). If usable text ≥ 4 chars → concatenate →
     `metadataProvider.SearchAsync(query, ct)` → add to candidates
   - **Path B — Web entity search:** Take top `WebEntities` by score
     (e.g., first 5), join descriptions into query →
     `metadataProvider.SearchAsync(entityQuery, ct)` → add to candidates
   - **Path C — Known-domain URL routing:** Scan `MatchingUrls` with
     `UrlRouter` for trusted domains. For movies: TMDB ID →
     `provider.GetByIdAsync(tmdbId)`. For music: MBID →
     `provider.GetByIdAsync(mbid)`. For games: skipped (IGDB has no
     slug-to-id endpoint; games match via Paths A/B instead).
5. **Dedupe and rank:** Deduplicate candidates by `ProviderKey`. Direct
   ID lookups (Path C) are placed first; search results (Paths A/B)
   follow in provider order. Truncate to top 10.
6. **Return:** If candidates non-empty → return them. If empty → return
   `LookupResponse(provider.Name, configured: true, results: [], hint:
   "No match found from this photo. Try retaking with better lighting,
   or search by title or barcode instead.")`

**Refactoring note:** The upload validation constants and `MagicBytesMatch`
are currently private to `CoversEndpoints`. Extract them into a small
shared helper (e.g., `ImageUploadValidator` in `Api/Endpoints/` or a
common static class) so both `CoversEndpoints` and the new by-image
routes use the same logic.

#### `LookupResponse<T>` in `LookupEndpoints.cs`

Add optional `Hint` field:

```csharp
public record LookupResponse<T>(
    string Provider, bool Configured, IReadOnlyList<T> Results, string? Hint = null);
```

#### `Program.cs`

Add `using Collectify.Infrastructure.Lookup.Vision;` and call
`builder.Services.AddVisionClient(builder.Configuration);` after
`AddMetadataLookup()`.

### `.env.example`

Add section:

```
# ----------------------------------------------------------------------
# Cloud Vision API — photo-snap cover analysis (OCR + reverse image)
# (https://console.cloud.google.com/apis/credentials)
# ----------------------------------------------------------------------
# Required to enable photo-snap lookup.
# Each photo uses one TEXT_DETECTION unit and one WEB_DETECTION unit.
# Each feature has its own first-1,000-units/month free tier.
# Collectify__Metadata__Vision__ApiKey=
# Collectify__Metadata__Vision__BaseUrl=https://vision.googleapis.com/v1/
```

## Frontend

### New file: `client/components/PhotoLookup.tsx`

Props mirror `BarcodeLookup`:

```tsx
interface Props<T extends MediaType> {
  type: T;
  onPick: (item: ResultMap[T]) => void;
  renderItem?: (item: ResultMap[T]) => { primary: string; secondary?: ReactNode; image?: string | null };
}
```

Component flow:

1. **"Snap cover" button** — opens camera modal
2. **Camera modal** — `getUserMedia({ video: { facingMode: 'environment' } })`
   renders live `<video>` preview. "Snap" button captures frame to `<canvas>`.
3. **Confirm step** — shows captured thumbnail. "Retake" returns to preview.
   "Search" proceeds.
4. **Resize** — draw on `<canvas>` at 1200px longest side, export
   `image/jpeg` at quality 0.7
5. **Upload** — `FormData` POST to `/api/lookup/{type}/by-image`
6. **Results** — render candidate list with same `renderItem` / `onPick`
   contract as `BarcodeLookup`. Show `hint` message if present.

Camera permission and modal shell are component-local (no shared hook with
BarcodeScanner needed — the flows diverge after `getUserMedia`).

### Modified files

`MovieForm.tsx`, `AlbumForm.tsx`, `GameForm.tsx` — add:

```tsx
<PhotoLookup type={type} onPick={onPick} renderItem={renderItem} />
```

alongside the existing `BarcodeLookup` component.

## Matching strategy (detailed)

```
Photo uploaded
    │
    ▼
Cloud Vision (TEXT_DETECTION + WEB_DETECTION)
    │
    ├── DetectedText: ["DUNE", "PART", "TWO", "2024"]
    ├── WebEntities: [("Dune (2021 film)", 0.95), ("Denis Villeneuve", 0.82), ...]
    └── MatchingUrls: [(themoviedb.org/movie/634649, "pagesWithMatchingImages"), ...]
    │
    ▼
Collect candidates from all applicable paths (no early return):

  Path A: OCR text search
    "DUNE PART TWO 2024" → TMDB SearchAsync → candidates A

  Path B: Web entity search
    "Dune 2021 film Denis Villeneuve" → TMDB SearchAsync → candidates B

  Path C: Known-domain URL routing (highest confidence)
    themoviedb.org/movie/634649 → GetByIdAsync("634649") → candidate C
    │
    ▼
Dedupe by ProviderKey, rank (Path C first, then A/B), truncate to 10
    │
    ▼
Return ranked candidates (or hint if empty)
```

Key difference from sequential early-return: all applicable paths are
collected before returning (called sequentially for v1 — parallel calls
are a later optimization). A direct ID match from Path C is never hidden
by mediocre OCR results from Path A because deduplication and ranking
happen after all paths have contributed.

## Error handling

| Scenario | Response |
|---|---|
| Vision API key not set | `{ configured: false, results: [] }` |
| Vision API HTTP error | Log warning, return empty result → falls through all paths → hint |
| All three paths return empty | `{ configured: true, results: [], hint: "No match found…" }` |
| Empty upload | 400 Bad Request |
| Upload > 5 MiB | 413 Payload Too Large |
| Wrong content type / magic bytes | 415 Unsupported Media Type |

## Testing

### Server (`tests/Collectify.Tests/`)

**`FakeVisionClient`** — implements `IVisionClient`, returns configurable
`VisionExtractResult`. Init-only properties for `DetectedText`,
`WebEntities` (`WebEntitySignal[]`), `MatchingUrls` (`MatchingUrlSignal[]`).

**Integration tests** via `WebApplicationFactory<Program>`:

1. **OCR path: good text → candidates** — Fake returns `["DUNE", "2024"]`,
   scripted provider returns results. Assert `configured: true`, results
   non-empty.
2. **OCR path: poor text → other paths still run** — Fake returns `["A"]`
   + web entities. Endpoint skips OCR, runs entity search. Assert results
   from entity path.
3. **Web entity path** — Fake returns empty text + scored web entities.
   Scripted provider matches on entity query. Assert results non-empty.
4. **URL routing: TMDB ID extracted, ranked first** — Fake returns TMDB
   URL in `MatchingUrls`. Scripted provider `GetByIdAsync` returns result.
   Assert result is first in list (direct ID ranked above search results).
5. **Deduplication** — Fake returns text + entities + URL that all resolve
   to the same `ProviderKey`. Assert single result (no duplicates).
6. **All paths empty → hint** — Fake returns minimal signals, scripted
   provider returns empty for all queries. Assert hint non-null.
7. **Not configured → configured: false** — Vision client with
   `IsConfigured = false`. Assert `configured: false`, results empty.
8. **Upload validation** — Empty (400), oversized (413), wrong type (415).
9. **All three media types** — Repeat test 1 for movies, music, games.

**`UrlRouter` unit tests:**
- Extract TMDB ID from `themoviedb.org/movie/27205-inception`
- Return null for unknown domains
- Return null for malformed TMDB URLs
- Extract MusicBrainz MBID from release URL

### Client

Unit tests for `PhotoLookup`:
- Renders "Snap cover" button
- Opens camera modal on click
- Shows confirm step after snap
- Calls correct endpoint on search
- Renders candidate list on success
- Shows hint message when present

### Spike: Image-hash caching

Track as a follow-up task: evaluate whether caching the full
vision→candidates chain by image content hash is worthwhile. Approach:
- Hash resized bytes (SHA-256, truncated to 16 hex chars, same as CoverImages)
- Store in `LookupCache` with key `vision:{hash}`
- Measure hit rate in production before committing

### Spike: Local cover-art embedding index

Long-term: evaluate building a self-hosted embedding index of cover images
from TMDB/MusicBrainz/IGDB for fully offline visual matching. Out of scope
for Phase 5; track as future exploration.

## Security

- Image bytes are processed in memory and discarded. Never persisted.
- Vision API key stays server-side (no client exposure).
- Upload validation mirrors `CoversEndpoints`: content-type whitelist,
  magic-byte sniff, 5 MiB cap.
- Secure-context requirement (`getUserMedia`) same as barcode scanning —
  documented in README.

## What is NOT in scope

- Image cropping
- Client-side vision processing or local ML models
- Multi-vision-provider chaining or fallback (single `IVisionClient`, swap via DI)
- Persisting uploaded images
- Non-Latin script OCR (Cloud Vision supports it, but no explicit testing)
- SerpApi / third-party Google Lens wrappers (tracked as future option if Cloud Vision proves insufficient)
- Local cover-art embedding index (tracked as spike for long-term self-hosted visual matching)
