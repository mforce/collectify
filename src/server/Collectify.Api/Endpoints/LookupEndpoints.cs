using Collectify.Infrastructure.Lookup;
using Microsoft.AspNetCore.Mvc;

namespace Collectify.Api.Endpoints;

public static class LookupEndpoints
{
    public record LookupResponse<T>(string Provider, bool Configured, IReadOnlyList<T> Results);

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

        return app;
    }

    private static IResult? Validate(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Results.BadRequest(new { error = "Query must be at least 2 characters." });
        return null;
    }
}
