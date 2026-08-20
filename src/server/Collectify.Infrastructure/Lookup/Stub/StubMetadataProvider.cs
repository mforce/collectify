namespace Collectify.Infrastructure.Lookup.Stub;

using Collectify.Domain.Metadata;

/// <summary>
/// Fallback provider used when no concrete provider of a media type has been
/// registered. Always reports unconfigured and returns empty/null so the lookup
/// endpoint can degrade gracefully -- the frontend just sees "no suggestions"
/// instead of a 500.
/// </summary>
internal class StubMetadataProvider<T> : IMetadataProvider<T>
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<T>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

    public Task<T?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult<T?>(default);

    public Task<IReadOnlyList<T>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
}

/// <summary>
/// Movie-capability stub: the shared empty stub plus a null IMDB lookup.
/// </summary>
internal sealed class StubMovieMetadataProvider : StubMetadataProvider<MovieLookupResult>, IMovieMetadataProvider
{
    public Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default)
        => Task.FromResult<MovieLookupResult?>(null);
}

/// <summary>
/// Game-capability stub: the shared empty stub. <see cref="IGameMetadataProvider.SearchByPlatformAsync"/>
/// needs no body here -- its default filters the (empty) shared search to empty.
/// </summary>
internal sealed class StubGameMetadataProvider : StubMetadataProvider<GameLookupResult>, IGameMetadataProvider
{
}
