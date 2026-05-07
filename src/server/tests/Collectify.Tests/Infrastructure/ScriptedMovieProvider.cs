using Collectify.Infrastructure.Lookup;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IMovieMetadataProvider"/>. Lets each endpoint
/// test compose a deterministic provider response (configured / not, search
/// result list, by-id result) without hitting the real TMDB provider or its
/// HTTP client.
/// </summary>
public sealed class ScriptedMovieProvider : IMovieMetadataProvider
{
    public string Name { get; init; } = "tmdb";
    public bool IsConfigured { get; init; } = true;
    public IReadOnlyList<MovieLookupResult> SearchResults { get; init; } = [];
    public MovieLookupResult? ById { get; init; }

    public Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(SearchResults);

    public Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult(ById);

    /// <summary>Configured + by-id returns the supplied result.</summary>
    public static ScriptedMovieProvider WithFoundResult(MovieLookupResult result) =>
        new() { ById = result };

    /// <summary>Configured + by-id returns null (TMDB 404 equivalent).</summary>
    public static ScriptedMovieProvider NotFound() =>
        new() { ById = null };
}
