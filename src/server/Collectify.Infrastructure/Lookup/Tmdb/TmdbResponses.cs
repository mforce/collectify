using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup.Tmdb;

/// <summary>
/// Subset of the TMDB v3 /search/movie response we actually consume.
/// Fields we don't need (vote_average, popularity, backdrop_path, …) are
/// intentionally absent so the JsonResponse cached on disk stays compact.
/// </summary>
internal sealed record TmdbSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<TmdbMovieSummary> Results);

internal sealed record TmdbMovieSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("original_title")] string? OriginalTitle,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath);
