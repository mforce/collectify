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
    public IReadOnlyList<MovieLookupResult> ByBarcode { get; init; } = [];
    public MovieLookupResult? ById { get; init; }
    public MovieLookupResult? ByImdbId { get; init; }

    public Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(SearchResults);

    public Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult(ById);

    public Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default)
        => Task.FromResult(ByImdbId);

    public Task<IReadOnlyList<MovieLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult(ByBarcode);

    /// <summary>Configured + by-id returns the supplied result.</summary>
    public static ScriptedMovieProvider WithFoundResult(MovieLookupResult result) =>
        new() { ById = result };

    /// <summary>Configured + by-id returns null (TMDB 404 equivalent).</summary>
    public static ScriptedMovieProvider NotFound() =>
        new() { ById = null };

    /// <summary>Configured + by-imdb-id returns the supplied result.</summary>
    public static ScriptedMovieProvider WithImdbResult(MovieLookupResult result) =>
        new() { ByImdbId = result };

    /// <summary>Configured + by-barcode returns the supplied results.</summary>
    public static ScriptedMovieProvider WithBarcodeResults(params MovieLookupResult[] results) =>
        new() { ByBarcode = results };
}
