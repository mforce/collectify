using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup.GiantBomb;

/// <summary>
/// Subset of the GiantBomb /api/releases/ response. Only the fields
/// we map into <see cref="Upc.UpcLookupResult"/> are kept. The release
/// "name" can differ from the underlying game's name (e.g. regional
/// box variants), so we prefer the game's name when present and fall
/// back to the release's.
/// </summary>
internal sealed record GiantBombReleasesResponse(
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("results")] IReadOnlyList<GiantBombRelease>? Results);

internal sealed record GiantBombRelease(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("game")] GiantBombGameRef? Game);

internal sealed record GiantBombGameRef(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name);
