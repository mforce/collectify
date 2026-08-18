namespace Collectify.Infrastructure.Lookup;

using Collectify.Domain.Enums;

/// <summary>
/// Per-domain provider contracts. Each concrete provider (TMDB,
/// MusicBrainz, IGDB, etc.) implements one of these; the lookup endpoint
/// asks the registered provider for that media type for suggestions.
/// IsConfigured lets a provider report "no API key set, skip me" so the
/// host can degrade gracefully without throwing.
/// </summary>
public interface IMovieMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Direct lookup by the provider's identifier (e.g. a TMDB id). Returns
    /// null when the provider doesn't recognise the id; tests may also rely
    /// on null when <see cref="IsConfigured"/> is false.
    /// </summary>
    Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Lookup by an IMDB id (the <c>tt…</c> shape). Implementations are
    /// expected to resolve the IMDB id to their own provider key under the
    /// hood and return the same shape as <see cref="GetByIdAsync"/>.
    /// </summary>
    Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default);

    /// <summary>
    /// Lookup by a UPC/EAN barcode. Implementations may delegate to a UPC
    /// database (e.g. UPCitemdb) to resolve the barcode to a product title
    /// and then run their own title search; returns up to a handful of
    /// candidates so the user can disambiguate when the UPC is shared
    /// across editions / boxsets.
    /// </summary>
    Task<IReadOnlyList<MovieLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default);
}

public interface IMusicMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Direct lookup by the provider's identifier (e.g. a MusicBrainz
    /// release MBID). Returns null when the provider doesn't recognise
    /// the id.
    /// </summary>
    Task<MusicLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Lookup by a UPC/EAN barcode. MusicBrainz indexes barcodes natively;
    /// other providers may dispatch via UPCitemdb first and then run a
    /// title search on the resolved name.
    /// </summary>
    Task<IReadOnlyList<MusicLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default);
}

public interface IGameMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Search restricted to results that map to <paramref name="platform"/>.
    /// The default implementation filters <see cref="SearchAsync"/> in memory;
    /// providers that fetch platform data natively (IGDB returns several
    /// platforms per release) may override to filter at the source and/or use a
    /// platform-scoped cache key. Empty when no result matches the platform.
    /// </summary>
    async Task<IReadOnlyList<GameLookupResult>> SearchByPlatformAsync(
        string query,
        GamePlatform platform,
        CancellationToken ct = default)
    {
        var results = await SearchAsync(query, ct).ConfigureAwait(false);
        return results.Where(r => r.IsOn(platform)).ToList();
    }

    /// <summary>
    /// Direct lookup by the provider's identifier (e.g. an IGDB game id).
    /// Returns null when the provider doesn't recognise the id.
    /// </summary>
    Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Lookup by a UPC/EAN barcode. IGDB doesn't index barcodes, so
    /// implementations are expected to dispatch via UPCitemdb and then run
    /// their own title search.
    /// </summary>
    Task<IReadOnlyList<GameLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default);
}
