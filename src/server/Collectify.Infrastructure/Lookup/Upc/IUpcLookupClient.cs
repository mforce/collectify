namespace Collectify.Infrastructure.Lookup.Upc;

/// <summary>
/// Generic UPC/EAN barcode → product summary lookup. Implementations
/// resolve a 12-13 digit barcode to a free-form product title (and brand
/// where available) so the per-type metadata providers can run a regular
/// title search to enrich the result. Returning null is the "barcode not
/// recognised" signal -- the provider should not call its title search
/// with an empty string in that case.
/// </summary>
public interface IUpcLookupClient
{
    string Name { get; }

    Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default);
}

/// <summary>
/// Subset of a UPC database hit that's useful for downstream title
/// searches. Brand and manufacturer aren't always populated; the title is
/// the only field the rest of the lookup pipeline relies on.
/// </summary>
public sealed record UpcLookupResult(string Title, string? Brand, string? Manufacturer);
