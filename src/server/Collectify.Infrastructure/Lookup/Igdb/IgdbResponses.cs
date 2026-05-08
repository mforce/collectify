using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Subset of IGDB's /v4/games response. The API returns many more fields
/// (rating, themes, age_ratings, …); only what maps into
/// <see cref="GameLookupResult"/> is kept so the cached JSON stays small.
/// </summary>
internal sealed record IgdbGame(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("first_release_date")] long? FirstReleaseDate,
    [property: JsonPropertyName("cover")] IgdbCover? Cover,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("involved_companies")] IReadOnlyList<IgdbInvolvedCompany>? InvolvedCompanies,
    [property: JsonPropertyName("platforms")] IReadOnlyList<IgdbPlatform>? Platforms,
    [property: JsonPropertyName("genres")] IReadOnlyList<IgdbGenre>? Genres);

internal sealed record IgdbCover(
    [property: JsonPropertyName("image_id")] string? ImageId);

internal sealed record IgdbInvolvedCompany(
    [property: JsonPropertyName("company")] IgdbCompany? Company,
    [property: JsonPropertyName("developer")] bool Developer,
    [property: JsonPropertyName("publisher")] bool Publisher);

internal sealed record IgdbCompany(
    [property: JsonPropertyName("name")] string? Name);

internal sealed record IgdbPlatform(
    [property: JsonPropertyName("name")] string? Name);

internal sealed record IgdbGenre(
    [property: JsonPropertyName("name")] string? Name);

/// <summary>
/// Twitch /oauth2/token client-credentials response. IGDB sits behind
/// Twitch's OAuth, so the token here is what gets passed as
/// <c>Authorization: Bearer …</c> to api.igdb.com.
/// </summary>
internal sealed record TwitchTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
