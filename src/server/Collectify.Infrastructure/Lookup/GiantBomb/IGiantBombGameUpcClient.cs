using Collectify.Infrastructure.Lookup.Upc;

namespace Collectify.Infrastructure.Lookup.GiantBomb;

/// <summary>
/// GiantBomb-backed UPC → game-title resolver. Distinct from
/// <see cref="IUpcLookupClient"/> on purpose: the IGDB game provider
/// uses GiantBomb only as a fallback when UPCitemdb misses, so the
/// two paths shouldn't share a registration slot. Returns null when
/// not configured or when GiantBomb has no release for the code.
/// </summary>
public interface IGiantBombGameUpcClient
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default);
}
