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

/// <summary>
/// Subset of /movie/{id}?append_to_response=credits. The append_to_response
/// trick saves us a second round trip while still giving us director +
/// runtime which the search endpoint doesn't carry.
/// </summary>
internal sealed record TmdbMovieDetail(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("original_title")] string? OriginalTitle,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("credits")] TmdbCredits? Credits);

internal sealed record TmdbCredits(
    [property: JsonPropertyName("crew")] IReadOnlyList<TmdbCrewMember>? Crew);

internal sealed record TmdbCrewMember(
    [property: JsonPropertyName("job")] string? Job,
    [property: JsonPropertyName("name")] string? Name);
