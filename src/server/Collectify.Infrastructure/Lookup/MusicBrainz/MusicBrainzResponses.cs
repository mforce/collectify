using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup.MusicBrainz;

/// <summary>
/// Subset of the MusicBrainz /ws/2/release search response. The API
/// returns a lot more (annotations, packaging, status, etc.) but only
/// the fields we map into <see cref="MusicLookupResult"/> are kept so
/// the JSON we cache stays compact.
///
/// MB returns artist + label data inline on search results when you
/// request <c>?fmt=json</c>, so a single /release call covers the form's
/// needs -- no need for the search-then-detail enrichment dance the
/// TMDB flow requires.
/// </summary>
internal sealed record MbReleaseSearchResponse(
    [property: JsonPropertyName("releases")] IReadOnlyList<MbRelease>? Releases);

internal sealed record MbRelease(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("artist-credit")] IReadOnlyList<MbArtistCredit>? ArtistCredit,
    [property: JsonPropertyName("label-info")] IReadOnlyList<MbLabelInfo>? LabelInfo);

internal sealed record MbArtistCredit(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("joinphrase")] string? JoinPhrase);

internal sealed record MbLabelInfo(
    [property: JsonPropertyName("label")] MbLabel? Label);

internal sealed record MbLabel(
    [property: JsonPropertyName("name")] string? Name);
