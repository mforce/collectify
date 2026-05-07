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

        return app;
    }

    private static IResult? Validate(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Results.BadRequest(new { error = "Query must be at least 2 characters." });
        return null;
    }
}
