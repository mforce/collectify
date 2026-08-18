using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Vision;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Collectify.Api.Endpoints;

public static class LookupEndpoints
{
    public record LookupResponse<T>(string Provider, bool Configured, IReadOnlyList<T> Results, string? Hint = null);

    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lookup").RequireAuthorization();

        group.MapGet("/movies", async (
            [FromQuery] string? q,
            IMovieMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (Validate(q) is { } error) return error;
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));
            var results = await provider.SearchAsync(q!.Trim(), ct);
            return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, true, results));
        });

        // Direct lookup by provider id (e.g. a TMDB movie id). Reuses
        // LookupResponse so the frontend handles unconfigured / not-found /
        // found with one shape: 0 results = not-found, 1 result = found,
        // configured=false signals "set the provider key" instead of
        // "your id was wrong".
        group.MapGet("/movies/by-id/{providerKey}", async (
            string providerKey,
            IMovieMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(providerKey))
                return Results.BadRequest(new { error = "Provider key is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));

            var hit = await provider.GetByIdAsync(providerKey.Trim(), ct);
            IReadOnlyList<MovieLookupResult> results = hit is null
                ? []
                : new[] { hit };
            return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, true, results));
        });

        // Lookup via an external IMDB id (the "tt..." shape). The provider
        // resolves it to its own provider key under the hood; the response
        // shape matches /by-id so the frontend uses the same code path.
        group.MapGet("/movies/by-imdb-id/{imdbId}", async (
            string imdbId,
            IMovieMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(imdbId))
                return Results.BadRequest(new { error = "IMDB id is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));

            var hit = await provider.GetByImdbIdAsync(imdbId.Trim(), ct);
            IReadOnlyList<MovieLookupResult> results = hit is null
                ? []
                : new[] { hit };
            return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, true, results));
        });

        // Barcode lookup. Movies don't have a native UPC index, so the
        // provider falls back to UPCitemdb -> product title -> its own
        // title search. Returns up to 10 candidates so the user can pick
        // the right edition (the UPC may be shared across box-sets).
        group.MapGet("/movies/by-barcode/{code}", async (
            string code,
            IMovieMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "Barcode is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));

            var results = await provider.SearchByBarcodeAsync(code.Trim(), ct);
            return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, true, results));
        });

        group.MapGet("/music", async (
            [FromQuery] string? q,
            IMusicMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (Validate(q) is { } error) return error;
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, false, []));
            var results = await provider.SearchAsync(q!.Trim(), ct);
            return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, true, results));
        });

        // Direct lookup by provider id (e.g. a MusicBrainz release MBID).
        // Same response shape as /movies/by-id so the frontend can use
        // one code path.
        group.MapGet("/music/by-id/{providerKey}", async (
            string providerKey,
            IMusicMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(providerKey))
                return Results.BadRequest(new { error = "Provider key is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, false, []));

            var hit = await provider.GetByIdAsync(providerKey.Trim(), ct);
            IReadOnlyList<MusicLookupResult> results = hit is null
                ? []
                : new[] { hit };
            return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, true, results));
        });

        // Barcode lookup. MusicBrainz indexes barcodes natively (no UPC
        // round-trip); the response shape stays parallel to the by-id and
        // search routes so the frontend can reuse the same decoder.
        group.MapGet("/music/by-barcode/{code}", async (
            string code,
            IMusicMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "Barcode is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, false, []));

            var results = await provider.SearchByBarcodeAsync(code.Trim(), ct);
            return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, true, results));
        });

        group.MapGet("/games", async (
            [FromQuery] string? q,
            [FromQuery] GamePlatform? platform,
            IGameMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (Validate(q) is { } error) return error;
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));

            // Edit-page prefill: when the caller passes the game's already-set
            // platform, search AT THE SOURCE for that platform (IGDB appends
            // `where platforms = (id)`), so console re-releases are excluded
            // from the window and the right SKU surfaces instead of being
            // buried by IGDB's all-platform fuzzy ranking. Fall back to an
            // unscoped search when the scoped query comes back empty (e.g.
            // IGDB has no entry for that platform) or the caller's platform is
            // unset/Other, which has no filtering meaning.
            if (platform is { } p && p != GamePlatform.Other)
            {
                var scoped = await provider.SearchByPlatformAsync(q!.Trim(), p, ct);
                var results = scoped.Count > 0
                    ? scoped
                    : await provider.SearchAsync(q!.Trim(), ct);
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, results));
            }

            var all = await provider.SearchAsync(q!.Trim(), ct);
            return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, all));
        });

        // Barcode lookup for games. IGDB doesn't index barcodes; the
        // provider dispatches to UPCitemdb first, then runs its own
        // Apicalypse title search.
        group.MapGet("/games/by-barcode/{code}", async (
            string code,
            IGameMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "Barcode is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));

            var results = await provider.SearchByBarcodeAsync(code.Trim(), ct);
            return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, results));
        });

        // Direct lookup by provider id (e.g. an IGDB game id). Same response
        // shape as /movies/by-id and /music/by-id so the frontend reuses
        // the LookupByIdOutcome decoder.
        group.MapGet("/games/by-id/{providerKey}", async (
            string providerKey,
            IGameMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(providerKey))
                return Results.BadRequest(new { error = "Provider key is required." });
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));

            var hit = await provider.GetByIdAsync(providerKey.Trim(), ct);
            IReadOnlyList<GameLookupResult> results = hit is null
                ? []
                : new[] { hit };
            return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, results));
        });

        // ---- Image-based lookup routes ----

        group.MapPost("/movies/by-image", async (
            [FromForm(Name = "file")] IFormFile? file,
            IMovieMetadataProvider provider,
            IVisionClient visionClient,
            IOptions<MetadataLookupOptions> lookupOptions,
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var noiseFilter = lookupOptions.Value.GetNoiseWordsFor(MetadataLookupOptions.Category.Movies);
            var candidates = await CollectCandidates(
                provider, vision, url => new UrlRouter.UrlResolution(UrlRouter.ExtractTmdbId(url), null), ct, noiseFilter, lookupOptions.Value.VisionResultLimit);

            if (candidates.Count > 0)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, true, candidates));

            return Results.Ok(new LookupResponse<MovieLookupResult>(
                provider.Name, true, [],
                "No match found from this photo. Try retaking with better lighting, or search by title or barcode instead."));
        })
        .DisableAntiforgery();

        group.MapPost("/music/by-image", async (
            [FromForm(Name = "file")] IFormFile? file,
            IMusicMetadataProvider provider,
            IVisionClient visionClient,
            IOptions<MetadataLookupOptions> lookupOptions,
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var noiseFilter = lookupOptions.Value.GetNoiseWordsFor(MetadataLookupOptions.Category.Music);
            var candidates = await CollectCandidates(
                provider, vision, url => new UrlRouter.UrlResolution(UrlRouter.ExtractMusicBrainzReleaseId(url), null), ct, noiseFilter, lookupOptions.Value.VisionResultLimit);

            if (candidates.Count > 0)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, true, candidates));

            return Results.Ok(new LookupResponse<MusicLookupResult>(
                provider.Name, true, [],
                "No match found from this photo. Try retaking with better lighting, or search by title or barcode instead."));
        })
        .DisableAntiforgery();

        group.MapPost("/games/by-image", async (
            [FromForm(Name = "file")] IFormFile? file,
            IGameMetadataProvider provider,
            IVisionClient visionClient,
            IOptions<MetadataLookupOptions> lookupOptions,
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var noiseFilter = lookupOptions.Value.GetNoiseWordsFor(MetadataLookupOptions.Category.Games);
            var candidates = await CollectCandidates(
                provider, vision, ResolveGameUrl, ct, noiseFilter, lookupOptions.Value.VisionResultLimit);

            if (candidates.Count > 0)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, candidates));

            return Results.Ok(new LookupResponse<GameLookupResult>(
                provider.Name, true, [],
                "No match found from this photo. Try retaking with better lighting, or search by title or barcode instead."));
        })
        .DisableAntiforgery();

        return app;
    }

    private static IResult? Validate(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Results.BadRequest(new { error = "Query must be at least 2 characters." });
        return null;
    }

    // ---- Multi-path candidate collection ----

    private static async Task<List<MovieLookupResult>> CollectCandidates(
        IMovieMetadataProvider provider, VisionExtractResult vision,
        Func<Uri, UrlRouter.UrlResolution?> resolveUrl, CancellationToken ct,
        HashSet<string>? noiseFilter = null, int limit = 100)
    {
        return await CollectCandidatesCore<MovieLookupResult>(new MovieAdapter(provider), vision, resolveUrl, ct, noiseFilter, limit);
    }

    private static async Task<List<MusicLookupResult>> CollectCandidates(
        IMusicMetadataProvider provider, VisionExtractResult vision,
        Func<Uri, UrlRouter.UrlResolution?> resolveUrl, CancellationToken ct,
        HashSet<string>? noiseFilter = null, int limit = 100)
    {
        return await CollectCandidatesCore<MusicLookupResult>(new MusicAdapter(provider), vision, resolveUrl, ct, noiseFilter, limit);
    }

    private static async Task<List<GameLookupResult>> CollectCandidates(
        IGameMetadataProvider provider, VisionExtractResult vision,
        Func<Uri, UrlRouter.UrlResolution?> resolveUrl, CancellationToken ct,
        HashSet<string>? noiseFilter = null, int limit = 100)
    {
        return await CollectCandidatesCore<GameLookupResult>(new GameAdapter(provider), vision, resolveUrl, ct, noiseFilter, limit);
    }

    private static async Task<List<T>> CollectCandidatesCore<T>(
        IMetadataProviderBase<T> provider, VisionExtractResult vision,
        Func<Uri, UrlRouter.UrlResolution?> resolveUrl, CancellationToken ct,
        HashSet<string>? noiseFilter = null, int limit = 100)
        where T : Collectify.Infrastructure.Lookup.ILookupResult
    {
        var scoredCandidates = new List<(int Priority, T Result)>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Path A: OCR text search — try combined query first, then fall
        // back to individual tokens if the combined search returns nothing.
        var filteredText = vision.DetectedText
            .Where(t => t.Length >= 2 && t.Length <= 60)
            .ToArray();

        // Strip platform/brand noise words (games only).
        var cleanTokens = noiseFilter is not null
            ? filteredText.Where(t => !noiseFilter.Contains(t)).ToArray()
            : filteredText;

        // Use clean tokens for combined query; fall back to all tokens
        // only if cleaning stripped everything.
        var queryTokens = cleanTokens.Length > 0 ? cleanTokens : filteredText;

        if (queryTokens.Sum(t => t.Length) >= 4)
        {
            var query = string.Join(" ", queryTokens);
            var ocrResults = await provider.SearchAsync(query, ct);
            foreach (var r in ocrResults)
                if (seenKeys.Add(r.ProviderKey))
                    scoredCandidates.Add((1, r));

            // Fallback: try the longest non-noise token on its own when the
            // combined query returned no candidates.
            if (ocrResults.Count == 0)
            {
                var bestToken = queryTokens
                    .OrderByDescending(t => t.Length)
                    .FirstOrDefault();
                if (bestToken is not null)
                {
                    var singleResults = await provider.SearchAsync(bestToken, ct);
                    foreach (var r in singleResults)
                        if (seenKeys.Add(r.ProviderKey))
                            scoredCandidates.Add((1, r));
                }
            }
        }

        // Path B: Web entity search — try the top entity alone first, then
        // a combined query of the top-5.
        if (vision.WebEntities.Length > 0)
        {
            var topEntity = vision.WebEntities.OrderByDescending(e => e.Score).First();
            var entityResults = await provider.SearchAsync(topEntity.Description, ct);
            foreach (var r in entityResults)
                if (seenKeys.Add(r.ProviderKey))
                    scoredCandidates.Add((1, r));

            // Broader fallback: combine top-5 entities when single entity
            // returned nothing.
            if (entityResults.Count == 0 && vision.WebEntities.Length > 1)
            {
                var entityQuery = string.Join(" ", vision.WebEntities
                    .OrderByDescending(e => e.Score)
                    .Take(5)
                    .Select(e => e.Description));
                if (!string.IsNullOrWhiteSpace(entityQuery))
                {
                    var broaderResults = await provider.SearchAsync(entityQuery, ct);
                    foreach (var r in broaderResults)
                        if (seenKeys.Add(r.ProviderKey))
                            scoredCandidates.Add((1, r));
                }
            }
        }

        // Path C: Known-domain URL routing (priority 0 = ranked first).
        // Try direct ID lookup first; fall back to slug-based search for
        // providers without a slug-to-ID endpoint (e.g. IGDB).
        foreach (var urlSignal in vision.MatchingUrls)
        {
            var resolution = resolveUrl(urlSignal.Uri);
            if (resolution == null) continue;

            // Direct ID lookup — highest confidence.
            if (resolution.Id is not null)
            {
                var hit = await provider.GetByIdAsync(resolution.Id, ct);
                if (hit != null)
                {
                    DeduplicateOrAdd(hit, 0);
                    break;
                }
            }

            // Slug-based search fallback — still high confidence because
            // the image matched a known provider page.
            if (resolution.SearchSlug is not null)
            {
                var slugResults = await provider.SearchAsync(resolution.SearchSlug, ct);
                foreach (var r in slugResults)
                    if (seenKeys.Add(r.ProviderKey))
                        scoredCandidates.Add((0, r));
                break; // One provider page is enough
            }
        }

        return scoredCandidates
            .OrderBy(c => c.Priority)
            .Select(c => c.Result)
            .Take(limit)
            .ToList();

        // --- Helpers ---

        void DeduplicateOrAdd(T result, int priority)
        {
            if (seenKeys.Contains(result.ProviderKey))
            {
                var idx = scoredCandidates.FindIndex(c =>
                    c.Result.ProviderKey.Equals(result.ProviderKey, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) scoredCandidates[idx] = (priority, result);
            }
            else
            {
                seenKeys.Add(result.ProviderKey);
                scoredCandidates.Add((priority, result));
            }
        }
    }

    /// <summary>Composite resolver: tries IGDB, then retail/info sites.</summary>
    private static UrlRouter.UrlResolution? ResolveGameUrl(Uri uri)
    {
        var resolution = UrlRouter.ResolveIgdbUrl(uri);
        if (resolution is not null) return resolution;

        resolution = UrlRouter.ResolveWikipediaGameUrl(uri);
        if (resolution is not null) return resolution;

        resolution = UrlRouter.ResolvePlayStationStoreUrl(uri);
        if (resolution is not null) return resolution;

        resolution = UrlRouter.ResolveTargetUrl(uri);
        if (resolution is not null) return resolution;

        resolution = UrlRouter.ResolveWalmartUrl(uri);
        if (resolution is not null) return resolution;

        resolution = UrlRouter.ResolveAmazonUrl(uri);
        if (resolution is not null) return resolution;

        return null;
    }

    /// <summary>Generic base for the three metadata provider interfaces.</summary>
    private interface IMetadataProviderBase<T>
    {
        Task<IReadOnlyList<T>> SearchAsync(string query, CancellationToken ct);
        Task<T?> GetByIdAsync(string providerKey, CancellationToken ct);
    }

    // Adapter wrappers so the three concrete providers satisfy IMetadataProviderBase<T>.
    private sealed class MovieAdapter : IMetadataProviderBase<MovieLookupResult>
    {
        private readonly IMovieMetadataProvider _inner;
        public MovieAdapter(IMovieMetadataProvider inner) => _inner = inner;
        public Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string q, CancellationToken ct) => _inner.SearchAsync(q, ct);
        public Task<MovieLookupResult?> GetByIdAsync(string pk, CancellationToken ct) => _inner.GetByIdAsync(pk, ct);
    }

    private sealed class MusicAdapter : IMetadataProviderBase<MusicLookupResult>
    {
        private readonly IMusicMetadataProvider _inner;
        public MusicAdapter(IMusicMetadataProvider inner) => _inner = inner;
        public Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string q, CancellationToken ct) => _inner.SearchAsync(q, ct);
        public Task<MusicLookupResult?> GetByIdAsync(string pk, CancellationToken ct) => _inner.GetByIdAsync(pk, ct);
    }

    private sealed class GameAdapter : IMetadataProviderBase<GameLookupResult>
    {
        private readonly IGameMetadataProvider _inner;
        public GameAdapter(IGameMetadataProvider inner) => _inner = inner;
        public Task<IReadOnlyList<GameLookupResult>> SearchAsync(string q, CancellationToken ct) => _inner.SearchAsync(q, ct);
        public Task<GameLookupResult?> GetByIdAsync(string pk, CancellationToken ct) => _inner.GetByIdAsync(pk, ct);
    }
}
