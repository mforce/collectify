using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Vision;
using Microsoft.AspNetCore.Mvc;

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
            IGameMetadataProvider provider,
            CancellationToken ct) =>
        {
            if (Validate(q) is { } error) return error;
            if (!provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));
            var results = await provider.SearchAsync(q!.Trim(), ct);
            return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, true, results));
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
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<MovieLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var candidates = await CollectCandidates(
                provider, vision, url => UrlRouter.ExtractTmdbId(url), ct);

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
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<MusicLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var candidates = await CollectCandidates(
                provider, vision, url => UrlRouter.ExtractMusicBrainzReleaseId(url), ct);

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
            CancellationToken ct) =>
        {
            var validation = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (validation.Error is not null) return validation.Error;
            byte[] bytes = validation.Bytes!;

            if (!visionClient.IsConfigured || !provider.IsConfigured)
                return Results.Ok(new LookupResponse<GameLookupResult>(provider.Name, false, []));

            var vision = await visionClient.AnalyseAsync(bytes, ct);
            var candidates = await CollectCandidates(
                provider, vision, url => UrlRouter.ExtractIgdbId(url), ct);

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
        Func<Uri, string?> extractId, CancellationToken ct)
    {
        return await CollectCandidatesCore<MovieLookupResult>(new MovieAdapter(provider), vision, extractId, ct);
    }

    private static async Task<List<MusicLookupResult>> CollectCandidates(
        IMusicMetadataProvider provider, VisionExtractResult vision,
        Func<Uri, string?> extractId, CancellationToken ct)
    {
        return await CollectCandidatesCore<MusicLookupResult>(new MusicAdapter(provider), vision, extractId, ct);
    }

    private static async Task<List<GameLookupResult>> CollectCandidates(
        IGameMetadataProvider provider, VisionExtractResult vision,
        Func<Uri, string?> extractId, CancellationToken ct)
    {
        return await CollectCandidatesCore<GameLookupResult>(new GameAdapter(provider), vision, extractId, ct);
    }

    private static async Task<List<T>> CollectCandidatesCore<T>(
        IMetadataProviderBase<T> provider, VisionExtractResult vision,
        Func<Uri, string?> extractId, CancellationToken ct)
        where T : Collectify.Infrastructure.Lookup.ILookupResult
    {
        var scoredCandidates = new List<(int Priority, T Result)>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Path A: OCR text search
        var filteredText = vision.DetectedText
            .Where(t => t.Length >= 2 && t.Length <= 60)
            .ToArray();
        if (filteredText.Sum(t => t.Length) >= 4)
        {
            var query = string.Join(" ", filteredText);
            var ocrResults = await provider.SearchAsync(query, ct);
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
                var entityResults = await provider.SearchAsync(entityQuery, ct);
                foreach (var r in entityResults)
                    if (seenKeys.Add(r.ProviderKey))
                        scoredCandidates.Add((1, r));
            }
        }

        // Path C: Known-domain URL routing (priority 0 = ranked first)
        foreach (var urlSignal in vision.MatchingUrls)
        {
            var id = extractId(urlSignal.Uri);
            if (id != null)
            {
                var hit = await provider.GetByIdAsync(id, ct);
                if (hit != null)
                {
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
                break; // One provider ID is enough
            }
        }

        return scoredCandidates
            .OrderBy(c => c.Priority)
            .Select(c => c.Result)
            .Take(10)
            .ToList();
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
