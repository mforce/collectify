namespace Collectify.Domain.Metadata;

/// <summary>
/// A metadata provider for one media type. Concrete providers (TMDB, MusicBrainz,
/// IGDB) implement <see cref="IMetadataProvider{T}"/> with their lookup-result type.
/// <see cref="IsConfigured"/> lets a provider report "no API key set, skip me" so
/// the host can degrade gracefully without throwing.
/// </summary>
/// <remarks>
/// The generic is deliberately <b>unconstrained</b>: the lookup-result records
/// (<c>MovieLookupResult</c>, <c>MusicLookupResult</c>, <c>GameLookupResult</c>)
/// live in <c>Collectify.Infrastructure.Lookup</c>, and Domain must have zero
/// non-BCL dependencies. Call sites that need the shared result contract apply
/// their own <c>where T : ILookupResult</c> constraint at use.
/// </remarks>
public interface IMetadataProvider<T>
{
    string Name { get; }

    bool IsConfigured { get; }

    /// <summary>
    /// Free-text search for candidates. Returns up to a handful of results the
    /// user can disambiguate.
    /// </summary>
    Task<IReadOnlyList<T>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Direct lookup by the provider's identifier (e.g. a TMDB movie id, a
    /// MusicBrainz release MBID, an IGDB game id). Returns null when the provider
    /// doesn't recognise the id; tests may also rely on null when
    /// <see cref="IsConfigured"/> is false.
    /// </summary>
    Task<T?> GetByIdAsync(string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Lookup by a UPC/EAN barcode. Implementations may delegate to a UPC database
    /// (e.g. UPCitemdb) to resolve the barcode to a product title and then run
    /// their own title search; returns up to a handful of candidates so the user
    /// can disambiguate when the UPC is shared across editions / boxsets.
    /// </summary>
    Task<IReadOnlyList<T>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default);
}
