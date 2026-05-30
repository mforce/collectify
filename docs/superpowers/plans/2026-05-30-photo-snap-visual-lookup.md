# Phase 5: Photo-snap visual lookup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add photo-snap cover lookup — capture a photo of physical media cover art, extract text and visual signals via Google Cloud Vision API, match against metadata providers, return ranked candidates.

**Architecture:** Provider-agnostic `IVisionClient` in Infrastructure. `POST /api/lookup/{type}/by-image` endpoint collects candidates from OCR text, web entities, and known-domain URL routing, dedupes by `ProviderKey`, ranks direct ID matches first. Frontend `PhotoLookup` component with camera preview, snap, confirm/retake, client-side resize.

**Tech Stack:** C# ASP.NET Core 10, Google Cloud Vision API, React 18, TypeScript, TanStack Query, Vitest + RTL

---

## File map

| Action | File | Responsibility |
|---|---|---|
| Create | `src/server/Collectify.Infrastructure/Lookup/Vision/IVisionClient.cs` | Interface + records (`VisionExtractResult`, `WebEntitySignal`, `MatchingUrlSignal`) |
| Create | `src/server/Collectify.Infrastructure/Lookup/Vision/CloudVisionClient.cs` | Google Cloud Vision API implementation (TEXT_DETECTION + WEB_DETECTION) |
| Create | `src/server/Collectify.Infrastructure/Lookup/Vision/VisionServiceCollectionExtensions.cs` | DI registration for `IVisionClient` |
| Create | `src/server/Collectify.Infrastructure/Lookup/Vision/UrlRouter.cs` | Static helper: extract TMDB ID, MusicBrainz MBID from URIs. IGDB routing disabled (no slug-to-id endpoint) |
| Create | `src/server/Collectify.Api/Endpoints/ImageUploadValidator.cs` | Shared upload validation (extracted from `CoversEndpoints`) |
| Create | `src/server/tests/Collectify.Tests/Infrastructure/FakeVisionClient.cs` | Test double for `IVisionClient` |
| Create | `src/server/tests/Collectify.Tests/Api/VisionLookupEndpointsTests.cs` | Integration tests for `POST /api/lookup/{type}/by-image` |
| Create | `src/server/tests/Collectify.Tests/Infrastructure/UrlRouterTests.cs` | Unit tests for `UrlRouter` |
| Create | `src/client/components/PhotoLookup.tsx` | Camera capture, confirm/retake, upload, candidate display |
| Create | `src/client/components/PhotoLookup.test.tsx` | Unit tests for `PhotoLookup` |
| Modify | `src/server/Collectify.Infrastructure/Lookup/MetadataLookupOptions.cs` | Add `VisionOptions` |
| Modify | `src/server/Collectify.Api/Endpoints/LookupEndpoints.cs` | Add `Hint` to `LookupResponse<T>`, add 3 by-image routes |
| Modify | `src/server/Collectify.Api/Endpoints/CoversEndpoints.cs` | Use shared `ImageUploadValidator` |
| Modify | `src/server/Collectify.Api/Program.cs` | Register `AddVisionClient` |
| Modify | `src/client/components/MovieForm.tsx` | Add `<PhotoLookup>` |
| Modify | `src/client/components/AlbumForm.tsx` | Add `<PhotoLookup>` |
| Modify | `src/client/components/GameForm.tsx` | Add `<PhotoLookup>` |
| Modify | `src/client/services/lookup.ts` | Add `lookupByImage` service function |
| Modify | `.env.example` | Add Vision API config section |

---

### Task 1: `IVisionClient` interface and records

**Files:**
- Create: `src/server/Collectify.Infrastructure/Lookup/Vision/IVisionClient.cs`

- [ ] **Step 1: Write the interface file**

```csharp
namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>Scored web entity from WEB_DETECTION.</summary>
public record WebEntitySignal(string Description, float Score);

/// <summary>Categorised matching URL from WEB_DETECTION.</summary>
/// <remarks>Category: "pagesWithMatchingImages" | "fullMatch" | "partialMatch" | "visuallySimilar"</remarks>
public record MatchingUrlSignal(Uri Uri, string Category);

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

- [ ] **Step 2: Verify build**

Run: `cd src/server && dotnet build Collectify.Infrastructure/Collectify.Infrastructure.csproj`
Expected: Clean build, no errors.

- [ ] **Step 3: Commit**

```bash
git add src/server/Collectify.Infrastructure/Lookup/Vision/IVisionClient.cs
git commit -m "feat: add IVisionClient interface with multi-signal result records"
```

---

### Task 2: `UrlRouter` static helper

**Files:**
- Create: `src/server/Collectify.Infrastructure/Lookup/Vision/UrlRouter.cs`
- Create: `src/server/tests/Collectify.Tests/Infrastructure/UrlRouterTests.cs`

- [ ] **Step 1: Write UrlRouter unit tests (red)**

```csharp
using Collectify.Infrastructure.Lookup.Vision;

namespace Collectify.Tests.Infrastructure;

public class UrlRouterTests
{
    [Fact]
    public void ExtractTmdbId_StandardUrl_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NoSlug_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/634649");
        Assert.Equal("634649", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_WithTrailingSlash_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception/");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_WithQuerystring_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception?language=en");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NoWww_ReturnsId()
    {
        var uri = new Uri("https://themoviedb.org/movie/634649-dune-part-two");
        Assert.Equal("634649", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NonMoviePage_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://www.themoviedb.org/person/27205-christopher-nolan")));
    }

    [Fact]
    public void ExtractTmdbId_NonTmdbDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://example.com/movie/27205-inception")));
    }

    [Fact]
    public void ExtractTmdbId_NonNumericId_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://www.themoviedb.org/movie/abc-inception")));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_StandardUrl_ReturnsMbid()
    {
        var uri = new Uri("https://musicbrainz.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf");
        Assert.Equal("f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            UrlRouter.ExtractMusicBrainzReleaseId(uri));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_WithTrailingSlash_ReturnsMbid()
    {
        var uri = new Uri("https://musicbrainz.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf/");
        Assert.Equal("f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            UrlRouter.ExtractMusicBrainzReleaseId(uri));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_NonReleasePage_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractMusicBrainzReleaseId(
            new Uri("https://musicbrainz.org/artist/f4e51c80-99e2-39e1-8062-c9b8e2685bdf")));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_NonMbDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractMusicBrainzReleaseId(
            new Uri("https://example.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf")));
    }

    [Fact]
    public void ExtractIgdbId_AlwaysReturnsNull()
    {
        // IGDB accepts only numeric IDs; slugs can't be resolved.
        Assert.Null(UrlRouter.ExtractIgdbId(
            new Uri("https://www.igdb.com/games/the-witcher-3-wild-hunt")));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/server && dotnet test --filter "FullyQualifiedName~UrlRouterTests" --no-build`
Expected: Build error (UrlRouter doesn't exist yet) or test failures.

- [ ] **Step 3: Write UrlRouter implementation**

```csharp
namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>
/// Extracts provider IDs from trusted-domain URLs returned by
/// WEB_DETECTION. Parses uri.Host and uri.AbsolutePath segments
/// instead of regex-matching the full URL — tolerates trailing slashes,
/// query strings, and URLs without slugs.
/// </summary>
public static class UrlRouter
{
    public static string? ExtractTmdbId(Uri uri)
    {
        if (!IsHost(uri, "themoviedb.org") && !IsHost(uri, "www.themoviedb.org"))
            return null;

        // /movie/27205-inception or /movie/27205
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "movie")
            return null;

        // First path segment after /movie/ — strip trailing slug if present
        var idPart = segments[1].Split('-')[0];
        return long.TryParse(idPart, out _) ? idPart : null;
    }

    public static string? ExtractMusicBrainzReleaseId(Uri uri)
    {
        if (!IsHost(uri, "musicbrainz.org"))
            return null;

        // /release/<mbid>
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "release")
            return null;

        var mbid = segments[1];
        // Validate MBID shape: 8-4-4-4-12 hex
        if (mbid.Length != 36)
            return null;
        for (var i = 0; i < mbid.Length; i++)
        {
            var c = mbid[i];
            if (i == 8 || i == 13 || i == 18 || i == 23)
            {
                if (c != '-') return null;
            }
            else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return null;
            }
        }
        return mbid;
    }

    /// <summary>
    /// IGDB has no slug-to-id endpoint; always returns null.
    /// Games still match via OCR (Path A) and web entities (Path B).
    /// </summary>
    public static string? ExtractIgdbId(Uri uri) => null;
    {
        if (!IsHost(uri, "igdb.com") && !IsHost(uri, "www.igdb.com"))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "games")
            return null;

        return segments[1];
    }

    private static bool IsHost(Uri uri, string expected)
        => uri.Host.Equals(expected, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/server && dotnet test --filter "FullyQualifiedName~UrlRouterTests" -v normal`
Expected: All 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/server/Collectify.Infrastructure/Lookup/Vision/UrlRouter.cs src/server/tests/Collectify.Tests/Infrastructure/UrlRouterTests.cs
git commit -m "feat: add UrlRouter for TMDB/MusicBrainz/IGDB ID extraction from URLs"
```

---

### Task 3: Extract `ImageUploadValidator` from `CoversEndpoints`

**Files:**
- Create: `src/server/Collectify.Api/Endpoints/ImageUploadValidator.cs`
- Modify: `src/server/Collectify.Api/Endpoints/CoversEndpoints.cs`

- [ ] **Step 1: Write ImageUploadValidator**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Collectify.Api.Endpoints;

/// <summary>Result of image upload validation: either bytes or an error.</summary>
public readonly struct ImageUploadResult
{
    public byte[]? Bytes { get; }
    public IResult? Error { get; }

    public static ImageUploadResult Success(byte[] bytes) => new() { Bytes = bytes };
    public static implicit operator ImageUploadResult(IResult error) => new() { Error = error };

    private ImageUploadResult() { }
}

/// <summary>
/// Shared image upload validation used by both CoversEndpoints and the
/// by-image vision lookup routes. Validates content-type, file size, and
/// magic bytes.
/// </summary>
public static class ImageUploadValidator
{
    // 5 MiB upload cap.
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    /// <summary>
    /// Validates the form file and returns the raw bytes on success,
    /// or an IResult error on failure.
    /// </summary>
    public static async Task<ImageUploadResult> ValidateAndReadAsync(
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "A non-empty file is required." });
        if (file.Length > MaxUploadBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (file.ContentType is null || !AllowedContentTypes.Contains(file.ContentType))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        if (!MagicBytesMatch(bytes, file.ContentType))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        return ImageUploadResult.Success(bytes);
    }

    /// <summary>
    /// Confirms the leading bytes of an upload actually look like the
    /// declared Content-Type. Returns true for content-types we don't
    /// sniff (defensive default for whitelist entries without a stable
    /// header signature).
    /// </summary>
    public static bool MagicBytesMatch(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4) return false;
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" when bytes.Length >= 8 =>
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            "image/webp" when bytes.Length >= 12 =>
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => true,
        };
    }
}
```

- [ ] **Step 2: Update CoversEndpoints to use ImageUploadValidator**

Replace the private `MaxUploadBytes`, `AllowedContentTypes`, and `MagicBytesMatch` in `CoversEndpoints.cs` with references to `ImageUploadValidator`. The POST handler's validation block becomes:

```csharp
// In CoversEndpoints.MapPost handler, replace the inline validation:
var result = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
if (result.Error is not null) return result.Error;
byte[] bytes = result.Bytes!;
```

Remove the now-unused private constants and `MagicBytesMatch` method from `CoversEndpoints`.

- [ ] **Step 3: Verify build and existing tests still pass**

Run: `cd src/server && dotnet test --filter "FullyQualifiedName~CoversEndpointsTests" -v normal`
Expected: All existing cover upload tests pass (same behaviour, just refactored).

- [ ] **Step 4: Commit**

```bash
git add src/server/Collectify.Api/Endpoints/ImageUploadValidator.cs src/server/Collectify.Api/Endpoints/CoversEndpoints.cs
git commit -m "refactor: extract ImageUploadValidator from CoversEndpoints for reuse"
```

---

### Task 4: Add `VisionOptions` to `MetadataLookupOptions`

**Files:**
- Modify: `src/server/Collectify.Infrastructure/Lookup/MetadataLookupOptions.cs`

- [ ] **Step 1: Add VisionOptions**

Add to `MetadataLookupOptions.cs`:

```csharp
public sealed class VisionOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://vision.googleapis.com/v1/";
}
```

And add property to `MetadataLookupOptions`:
```csharp
public VisionOptions Vision { get; set; } = new();
```

- [ ] **Step 2: Verify build**

Run: `cd src/server && dotnet build Collectify.Infrastructure/Collectify.Infrastructure.csproj`
Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add src/server/Collectify.Infrastructure/Lookup/MetadataLookupOptions.cs
git commit -m "feat: add VisionOptions to MetadataLookupOptions"
```

---

### Task 5: `CloudVisionClient` implementation

**Files:**
- Create: `src/server/Collectify.Infrastructure/Lookup/Vision/CloudVisionClient.cs`

- [ ] **Step 1: Write CloudVisionClient**

The client sends image bytes to `images:annotate` with features `TEXT_DETECTION` and `WEB_DETECTION`. It uses `IHttpClientFactory` named client `"vision"` and reads config from `IOptions<MetadataLookupOptions>`.

Key implementation details:
- POST to `{BaseUrl}images:annotate?key={ApiKey}` with JSON body:
  ```json
  {
    "requests": [{
      "image": { "content": "<base64 bytes>" },
      "features": [
        { "type": "TEXT_DETECTION" },
        { "type": "WEB_DETECTION" }
      ]
    }]
  }
  ```
- Map response `textAnnotations` → `DetectedText` (skip index 0 which is the full text blob; filter entries < 2 chars or > 60 chars)
- Map `webDetection.webEntities` → `WebEntitySignal[]` (description + score)
- Map `webDetection.pagesWithMatchingImages` → `MatchingUrlSignal[]` with category `"pagesWithMatchingImages"`
- Map `webDetection.fullMatchingImageUrls` → category `"fullMatch"`
- Map `webDetection.partialMatchingImageUrls` → category `"partialMatch"`
- Map `webDetection.visuallySimilarImages` → category `"visuallySimilar"`
- `TextConfidence` = average of individual text annotation confidence values (0 if none)
- On HTTP error: log warning, return empty `VisionExtractResult`
- `IsConfigured` = `!string.IsNullOrWhiteSpace(ApiKey)`

- [ ] **Step 2: Verify build**

Run: `cd src/server && dotnet build Collectify.Infrastructure/Collectify.Infrastructure.csproj`
Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add src/server/Collectify.Infrastructure/Lookup/Vision/CloudVisionClient.cs
git commit -m "feat: implement CloudVisionClient with TEXT_DETECTION + WEB_DETECTION"
```

---

### Task 6: `VisionServiceCollectionExtensions` DI registration

**Files:**
- Create: `src/server/Collectify.Infrastructure/Lookup/Vision/VisionServiceCollectionExtensions.cs`
- Modify: `src/server/Collectify.Api/Program.cs`

- [ ] **Step 1: Write DI extension**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collectify.Infrastructure.Lookup.Vision;

public static class VisionServiceCollectionExtensions
{
    public static IServiceCollection AddVisionClient(
        this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<CloudVisionClient>("vision")
            .SetHandlerLifetime(TimeSpan.FromMinutes(2));

        services.AddScoped<IVisionClient, CloudVisionClient>();
        return services;
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add `using Collectify.Infrastructure.Lookup.Vision;` and call
`builder.Services.AddVisionClient(builder.Configuration);` after `AddMetadataLookup()`.

- [ ] **Step 3: Verify build**

Run: `cd src/server && dotnet build Collectify.Api/Collectify.Api.csproj`
Expected: Clean build.

- [ ] **Step 4: Commit**

```bash
git add src/server/Collectify.Infrastructure/Lookup/Vision/VisionServiceCollectionExtensions.cs src/server/Collectify.Api/Program.cs
git commit -m "feat: wire up IVisionClient DI registration in Program.cs"
```

---

### Task 7: `FakeVisionClient` test double

**Files:**
- Create: `src/server/tests/Collectify.Tests/Infrastructure/FakeVisionClient.cs`

- [ ] **Step 1: Write FakeVisionClient**

```csharp
using Collectify.Infrastructure.Lookup.Vision;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for IVisionClient. Returns whatever the test scripted
/// via init-only properties so endpoint tests can compose deterministic
/// vision responses without hitting Google Cloud.
/// </summary>
public sealed class FakeVisionClient : IVisionClient
{
    public string Name { get; init; } = "fake-vision";
    public bool IsConfigured { get; init; } = true;
    public string[] DetectedText { get; init; } = [];
    public float TextConfidence { get; init; } = 1f;
    public WebEntitySignal[] WebEntities { get; init; } = [];
    public MatchingUrlSignal[] MatchingUrls { get; init; } = [];

    public Task<VisionExtractResult> AnalyseAsync(byte[] imageBytes, CancellationToken ct = default)
        => Task.FromResult(new VisionExtractResult(
            DetectedText, TextConfidence, WebEntities, MatchingUrls));

    public static FakeVisionClient WithText(params string[] words) =>
        new() { DetectedText = words };

    public static FakeVisionClient WithEntities(params WebEntitySignal[] entities) =>
        new() { WebEntities = entities };

    public static FakeVisionClient WithUrls(params MatchingUrlSignal[] urls) =>
        new() { MatchingUrls = urls };

    public static FakeVisionClient NotConfigured() =>
        new() { IsConfigured = false };
}
```

- [ ] **Step 2: Verify build**

Run: `cd src/server && dotnet build tests/Collectify.Tests/Collectify.Tests.csproj`
Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add src/server/tests/Collectify.Tests/Infrastructure/FakeVisionClient.cs
git commit -m "test: add FakeVisionClient test double"
```

---

### Task 8: Add `VisionClient` override to `CollectifyApiFactory`

**Files:**
- Modify: `src/server/tests/Collectify.Tests/Infrastructure/CollectifyApiFactory.cs`

- [ ] **Step 1: Add VisionClient property and swap**

Add to `CollectifyApiFactory`:

```csharp
public IVisionClient? VisionClient { get; init; }
```

In `ConfigureServices`, after the game provider swap:

```csharp
if (VisionClient is not null)
{
    services.RemoveAll<IVisionClient>();
    services.AddSingleton(VisionClient);
}
```

- [ ] **Step 2: Verify build**

Run: `cd src/server && dotnet build tests/Collectify.Tests/Collectify.Tests.csproj`
Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add src/server/tests/Collectify.Tests/Infrastructure/CollectifyApiFactory.cs
git commit -m "test: add VisionClient override to CollectifyApiFactory"
```

---

### Task 9: Add `Hint` to `LookupResponse<T>` and by-image endpoints

**Files:**
- Modify: `src/server/Collectify.Api/Endpoints/LookupEndpoints.cs`

- [ ] **Step 1: Add Hint to LookupResponse**

Change the record in `LookupEndpoints.cs`:

```csharp
public record LookupResponse<T>(string Provider, bool Configured, IReadOnlyList<T> Results, string? Hint = null);
```

- [ ] **Step 2: Add by-image routes**

Add three new routes inside the existing `/api/lookup` group (one per media type). Each follows this pattern:

```csharp
// POST /api/lookup/movies/by-image
group.MapPost("/movies/by-image", async (
    [FromForm(Name = "file")] IFormFile? file,
    IMovieMetadataProvider metadataProvider,
    IVisionClient visionClient,
    CancellationToken ct) =>
{
    // 1. Validate upload
    var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
    if (validation is IResult error) return error;
    byte[] bytes = (byte[])validation;

    // 2. Check configured — either Vision or the metadata provider being
    //    unconfigured means the full chain can't run.
    if (!visionClient.IsConfigured || !metadataProvider.IsConfigured)
        return Results.Ok(new LookupResponse<MovieLookupResult>(metadataProvider.Name, false, []));

    // 3. Analyse image
    var vision = await visionClient.AnalyseAsync(bytes, ct);

    // 4. Collect candidates from all paths, tagged by source priority.
    //    Priority: 0 = direct ID (highest), 1 = search result.
    var scoredCandidates = new List<(int Priority, MovieLookupResult Result)>();
    var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Path A: OCR text search
    var filteredText = vision.DetectedText
        .Where(t => t.Length >= 2 && t.Length <= 60)
        .ToArray();
    var usableTextLength = filteredText.Sum(t => t.Length);
    if (usableTextLength >= 4)
    {
        var query = string.Join(" ", filteredText);
        var ocrResults = await metadataProvider.SearchAsync(query, ct);
        foreach (var r in ocrResults)
            if (seenKeys.Add(r.ProviderKey))
                scoredCandidates.Add((1, r));
    }

    // Path B: Web entity search
    if (vision.WebEntities.Length > 0)
    {
        var entityQuery = string.Join(" ", vision.WebEntities
            .OrderByDescending(e => e.Score)
            .Take(5)
            .Select(e => e.Description));
        if (!string.IsNullOrWhiteSpace(entityQuery))
        {
            var entityResults = await metadataProvider.SearchAsync(entityQuery, ct);
            foreach (var r in entityResults)
                if (seenKeys.Add(r.ProviderKey))
                    scoredCandidates.Add((1, r));
        }
    }

    // Path C: Known-domain URL routing (priority 0 = ranked first)
    foreach (var urlSignal in vision.MatchingUrls)
    {
        var tmdbId = UrlRouter.ExtractTmdbId(urlSignal.Uri);
        if (tmdbId != null)
        {
            var hit = await metadataProvider.GetByIdAsync(tmdbId, ct);
            if (hit != null)
            {
                // If this ProviderKey was already added by search, promote
                // it by replacing the entry with priority 0.
                if (seenKeys.Contains(hit.ProviderKey))
                {
                    var idx = scoredCandidates.FindIndex(c =>
                        c.Result.ProviderKey.Equals(hit.ProviderKey, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) scoredCandidates[idx] = (0, hit);
                }
                else
                {
                    seenKeys.Add(hit.ProviderKey);
                    scoredCandidates.Add((0, hit));
                }
            }
            break; // One TMDB ID is enough
        }
    }

    // 5. Sort by priority (direct ID first), then truncate to top 10
    var candidates = scoredCandidates
        .OrderBy(c => c.Priority)
        .Select(c => c.Result)
        .Take(10)
        .ToList();

    // 6. Return
    if (candidates.Count > 0)
        return Results.Ok(new LookupResponse<MovieLookupResult>(metadataProvider.Name, true, candidates));

    return Results.Ok(new LookupResponse<MovieLookupResult>(
        metadataProvider.Name, true, [],
        "No match found from this photo. Try retaking with better lighting, or search by title or barcode instead."));
})
.RequireAuthorization()
.DisableAntiforgery();
```

Repeat for `/music/by-image` (using `IMusicMetadataProvider`, `MusicLookupResult`, and `UrlRouter.ExtractMusicBrainzReleaseId`) and `/games/by-image` (using `IGameMetadataProvider`, `GameLookupResult`, and `UrlRouter.ExtractIgdbId` — always null, so Path C is skipped for games; they match via OCR and web entities instead).

- [ ] **Step 3: Verify build**

Run: `cd src/server && dotnet build Collectify.Api/Collectify.Api.csproj`
Expected: Clean build.

- [ ] **Step 4: Commit**

```bash
git add src/server/Collectify.Api/Endpoints/LookupEndpoints.cs
git commit -m "feat: add POST /api/lookup/{type}/by-image endpoints with multi-path matching"
```

---

### Task 10: Integration tests for by-image endpoints

**Files:**
- Create: `src/server/tests/Collectify.Tests/Api/VisionLookupEndpointsTests.cs`

- [ ] **Step 1: Write integration tests**

Follow the pattern from `LookupEndpointsTests.cs` and `CoversEndpointsTests.cs`. Use `FakeVisionClient` + `ScriptedMovieProvider`/`ScriptedMusicProvider`/`ScriptedGameProvider` via `CollectifyApiFactory` overrides. Use `MultipartFormDataContent` with tiny JPEG/PNG byte arrays (reuse the `TinyJpeg` pattern from `CoversEndpointsTests`).

Tests:

```csharp
public class VisionLookupEndpointsTests
{
    private record LookupResponse<T>(string Provider, bool Configured, T[] Results, string? Hint);
    private static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

    private static MultipartFormDataContent FilePart(byte[] bytes, string contentType = "image/jpeg", string filename = "cover.jpg")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", filename);
        return form;
    }

    // --- Auth ---
    [Theory]
    [InlineData("/api/lookup/movies/by-image")]
    [InlineData("/api/lookup/music/by-image")]
    [InlineData("/api/lookup/games/by-image")]
    public async Task ByImage_Unauthenticated_Returns401(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var response = await factory.CreateClient().PostAsync(url, FilePart(TinyJpeg));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Not configured ---
    [Fact]
    public async Task ByImage_VisionNotConfigured_ReturnsConfiguredFalse()
    {
        // Either Vision or metadata provider being unconfigured returns
        // configured: false.
        await using var factory = new CollectifyApiFactory
        {
            VisionClient = FakeVisionClient.NotConfigured()
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<object>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    // --- OCR path ---
    [Fact]
    public async Task ByImage_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", "Inception", 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("INCEPTION", "2010")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
        Assert.Equal("Inception", body.Results[0].Title);
    }

    // --- Web entity path ---
    [Fact]
    public async Task ByImage_WebEntityPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [seeded] },
            VisionClient = new FakeVisionClient
            {
                WebEntities = [new WebEntitySignal("Inception (2010 film)", 0.95f)]
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }

    // --- URL routing ranked first ---
    [Fact]
    public async Task ByImage_UrlRouting_RankedAboveSearchResults()
    {
        var directHit = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, "Christopher Nolan", 148, null, null, null);
        var searchHit = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "634649", "Dune Part Two", null, 2024, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider
            {
                SearchResults = [searchHit],
                ById = directHit
            },
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["SOME", "RANDOM", "TEXT"],
                MatchingUrls = [new MatchingUrlSignal(
                    new Uri("https://www.themoviedb.org/movie/27205-inception"),
                    "pagesWithMatchingImages")]
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Results);
        // Direct ID match should be first
        Assert.Equal("27205", body.Results[0].ProviderKey);
    }

    // --- Deduplication ---
    [Fact]
    public async Task ByImage_DeduplicatesByProviderKey()
    {
        var sameResult = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [sameResult], ById = sameResult },
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["INCEPTION", "2010"],
                WebEntities = [new WebEntitySignal("Inception", 0.9f)],
                MatchingUrls = [new MatchingUrlSignal(
                    new Uri("https://www.themoviedb.org/movie/27205-inception"),
                    "pagesWithMatchingImages")]
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        // All three paths resolve to same ProviderKey; should appear once
        Assert.Single(body!.Results);
    }

    // --- All paths empty -> hint ---
    [Fact]
    public async Task ByImage_AllPathsEmpty_ReturnsHint()
    {
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = ScriptedMovieProvider.NotFound(),
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["X"],
                WebEntities = [],
                MatchingUrls = []
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<object>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
        Assert.NotNull(body.Hint);
    }

    // --- Upload validation ---
    [Fact]
    public async Task ByImage_EmptyFile_Returns400()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var response = await alice.Client.PostAsync(
            "/api/lookup/movies/by-image", FilePart(Array.Empty<byte>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ByImage_WrongContentType_Returns415()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var response = await alice.Client.PostAsync(
            "/api/lookup/movies/by-image",
            FilePart(System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // --- All three media types ---
    [Fact]
    public async Task ByImage_Music_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MusicLookupResult(
            "musicbrainz", "f4e51c80", "OK Computer", "Radiohead", 1997, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MusicProvider = new ScriptedMusicProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("OK", "COMPUTER", "RADIOHEAD")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MusicLookupResult>>(
            "/api/lookup/music/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }

    [Fact]
    public async Task ByImage_Games_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.GameLookupResult(
            "igdb", "1942", "The Witcher 3", null, 2015, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            GameProvider = new ScriptedGameProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("WITCHER", "THREE")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.GameLookupResult>>(
            "/api/lookup/games/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }
}
```

Add a helper extension for posting multipart and reading JSON:
```csharp
// In TestExtensions.cs:
public static async Task<T?> PostMultipartAndReadJsonAsync<T>(
    this HttpClient client, string url, HttpContent content)
{
    var response = await client.PostAsync(url, content);
    return await response.Content.ReadFromJsonAsync<T>(TestExtensions.JsonOptions);
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `cd src/server && dotnet test --filter "FullyQualifiedName~VisionLookupEndpointsTests" -v normal`
Expected: All tests pass.

- [ ] **Step 3: Run full test suite**

Run: `cd src/server && dotnet test`
Expected: All existing tests still pass + new tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/server/tests/Collectify.Tests/Api/VisionLookupEndpointsTests.cs
git commit -m "test: add integration tests for by-image vision lookup endpoints"
```

---

### Task 11: `PhotoLookup` frontend component

**Files:**
- Create: `src/client/components/PhotoLookup.tsx`

- [ ] **Step 1: Add `lookupByImage` service function to `src/client/services/lookup.ts`**

Follow the existing pattern from `lookupByBarcode`. Add:

```ts
/**
 * Photo-snap lookup. Uploads a resized image and returns candidates from
 * OCR + web entity + URL routing paths. Same LookupResponse shape as
 * barcode/title search so the frontend reuses the candidate list UI.
 *
 * Uses fetch directly (not the api() helper) because FormData requires
 * the browser to set the multipart Content-Type boundary. The api() helper
 * would override it with application/json.
 */
export async function lookupByImage<T extends MediaType>(
  type: T,
  file: Blob,
): Promise<LookupResponse<ResultMap[T]>> {
  const form = new FormData();
  form.append('file', file, 'cover.jpg');

  const res = await fetch(`/api/lookup/${type}/by-image`, {
    method: 'POST',
    credentials: 'include',
    body: form,
  });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const data = await res.json();
      if (data?.error) message = data.error;
    } catch {}
    throw new ApiError(res.status, message);
  }

  return res.json() as Promise<LookupResponse<ResultMap[T]>>;
}
```

Import `ApiError` from `./client` at the top of `lookup.ts`.

- [ ] **Step 2: Write PhotoLookup component**

Component with camera preview, snap, confirm/retake, client-side resize, and upload. Follows the same pattern as `BarcodeLookup` (same props contract: `type`, `onPick`, `renderItem`). Use `lookupByImage` from the service layer for the upload.

Key implementation details:
- State machine: `idle` → `preview` (camera streaming) → `confirm` (thumbnail + retake/search) → `results` (candidate list)
- `getUserMedia({ video: { facingMode: { ideal: 'environment' } } })` for rear camera
- Snap: draw `<video>` frame to hidden `<canvas>`, convert to data URL
- Confirm: show thumbnail from data URL. Retake returns to preview. Search proceeds to resize + upload.
- Resize: draw to `<canvas>` at 1200px longest side, `canvas.toBlob(blob => ..., 'image/jpeg', 0.7)`
- Upload: `FormData` with field `file`, POST to `/api/lookup/{type}/by-image`
- Results: render candidate list with same `renderItem` / `onPick` contract. Show `hint` if present.
- Modal shell mirrors `BarcodeScanner` (fixed inset-0, z-50, h-dvh, bg-black/95)
- Escape key closes modal. Stream cleanup on unmount (same pattern as BarcodeScanner).
- HTTPS check: if `navigator.mediaDevices?.getUserMedia` unavailable, show "secure context" message.

- [ ] **Step 2: Verify build**

Run: `cd src/client && npx tsc --noEmit`
Expected: Clean type check.

- [ ] **Step 3: Commit**

```bash
git add src/client/components/PhotoLookup.tsx
git commit -m "feat: add PhotoLookup component with camera preview and image upload"
```

---

### Task 12: `PhotoLookup` unit tests

**Files:**
- Create: `src/client/components/PhotoLookup.test.tsx`

- [ ] **Step 1: Write unit tests**

Follow the pattern from `BarcodeLookup.test.tsx`. Mock `navigator.mediaDevices.getUserMedia` and the fetch API.

Tests:
1. Renders "Snap cover" button
2. Opens camera modal on click (dialog with aria-label)
3. Shows confirm step after snap (thumbnail + Retake/Search buttons)
4. Retake returns to camera preview
5. Search uploads to correct endpoint (`/api/lookup/{type}/by-image`)
6. Renders candidate list on success and fires `onPick` on selection
7. Shows hint message when server returns hint
8. Shows "not configured" hint when `configured: false`
9. Shows HTTPS hint when `getUserMedia` unavailable

- [ ] **Step 2: Run tests**

Run: `cd src/client && npm test -- --run src/client/components/PhotoLookup.test.tsx`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/client/components/PhotoLookup.test.tsx
git commit -m "test: add unit tests for PhotoLookup component"
```

---

### Task 13: Add `<PhotoLookup>` to all three forms

**Files:**
- Modify: `src/client/components/MovieForm.tsx`
- Modify: `src/client/components/AlbumForm.tsx`
- Modify: `src/client/components/GameForm.tsx`

- [ ] **Step 1: Add import and component to MovieForm**

Add `import PhotoLookup from './PhotoLookup';` after the `BarcodeLookup` import.

Add `<PhotoLookup>` after `<BarcodeLookup>`:

```tsx
<PhotoLookup
  type="movies"
  onPick={importLookup}
  renderItem={(r) => ({
    primary: r.title + (r.year ? ` (${r.year})` : ''),
    secondary: r.description?.slice(0, 120),
    image: r.imageUrl,
  })}
/>
```

- [ ] **Step 2: Add to AlbumForm**

Same pattern. Import `PhotoLookup`, add after `BarcodeLookup` with `type="music"` and the album-specific `renderItem`.

- [ ] **Step 3: Add to GameForm**

Same pattern. Import `PhotoLookup`, add after `BarcodeLookup` with `type="games"` and the game-specific `renderItem`.

- [ ] **Step 4: Verify build**

Run: `cd src/client && npm run build`
Expected: Clean build (tsc + vite).

- [ ] **Step 5: Run client tests**

Run: `cd src/client && npm test -- --run`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/client/components/MovieForm.tsx src/client/components/AlbumForm.tsx src/client/components/GameForm.tsx
git commit -m "feat: add PhotoLookup button to movie, album, and game forms"
```

---

### Task 14: Update `.env.example`

**Files:**
- Modify: `.env.example`

- [ ] **Step 1: Add Vision API section**

Add after the UPCitemdb section:

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

- [ ] **Step 2: Commit**

```bash
git add .env.example
git commit -m "docs: add Cloud Vision API config to .env.example"
```

---

### Task 15: Final verification

- [ ] **Step 1: Full server build and test**

Run: `cd src/server && dotnet build Collectify.slnx && dotnet test`
Expected: Clean build, all tests pass.

- [ ] **Step 2: Full client build and test**

Run: `cd src/client && npm run build && npm test -- --run`
Expected: Clean build, all tests pass.

- [ ] **Step 3: Commit any remaining changes**

```bash
git status
# If clean, we're done. If not, commit remaining changes.
```

---

## Self-review checklist

**Spec coverage:**
- [x] `IVisionClient` interface with multi-signal result (Task 1)
- [x] `CloudVisionClient` implementation (Task 5)
- [x] `VisionServiceCollectionExtensions` DI (Task 6)
- [x] `UrlRouter` with TMDB/MusicBrainz/IGDB extraction (Task 2)
- [x] `VisionOptions` in config (Task 4)
- [x] `Hint` on `LookupResponse<T>` (Task 9)
- [x] Three by-image POST routes with collect-and-dedupe (Task 9)
- [x] Shared `ImageUploadValidator` extracted (Task 3)
- [x] `Program.cs` registration (Task 6)
- [x] `FakeVisionClient` test double (Task 7)
- [x] `CollectifyApiFactory` vision override (Task 8)
- [x] Integration tests: OCR, entity, URL routing, dedup, hint, validation, all 3 types (Task 10)
- [x] `UrlRouter` unit tests (Task 2)
- [x] `lookupByImage` service function in `lookup.ts` (Task 11)
- [x] `PhotoLookup` component (Task 11)
- [x] `PhotoLookup` tests (Task 12)
- [x] All three forms updated (Task 13)
- [x] `.env.example` updated (Task 14)

**Codex review fixes (applied):**
- [x] `ImageUploadValidator` returns `ImageUploadResult` struct (not `IResult?` cast to `byte[]`)
- [x] `UrlRouter` uses path segment parsing (not brittle regexes)
- [x] Dedup/ranking: direct ID hits promote existing entries (not discarded)
- [x] Endpoint checks both `visionClient.IsConfigured` AND `metadataProvider.IsConfigured`
- [x] Multipart test helper renamed to `PostMultipartAndReadJsonAsync`
- [x] `lookupByImage` added to `services/lookup.ts` (follows service-layer pattern)

**Placeholder scan:** No TBDs, no "add validation later", no "similar to Task N" without code. Every step has code or explicit instructions.

**Type consistency:** `VisionExtractResult`, `WebEntitySignal`, `MatchingUrlSignal` names match across interface (Task 1), implementation (Task 5), fake (Task 7), and tests (Task 10). `LookupResponse<T>` with `Hint` added once in Task 9, used consistently in endpoint and tests. `ImageUploadValidator` static class shared between `CoversEndpoints` and `LookupEndpoints`.

**Scope check:** Focused on Phase 5 only. No unrelated refactoring. Spikes (image-hash cache, local embedding index) tracked as future tasks, not implemented.
