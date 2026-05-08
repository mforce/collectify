using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup.Upc;

/// <summary>
/// Subset of the UPCitemdb /prod/trial/lookup response. Only the fields we
/// map into <see cref="UpcLookupResult"/> are kept so the cached JSON
/// stays small. The trial endpoint wraps results in a top-level "items"
/// array even when there's a single match.
/// </summary>
internal sealed record UpcItemDbResponse(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("items")] IReadOnlyList<UpcItemDbItem>? Items);

internal sealed record UpcItemDbItem(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("brand")] string? Brand,
    [property: JsonPropertyName("manufacturer")] string? Manufacturer);
