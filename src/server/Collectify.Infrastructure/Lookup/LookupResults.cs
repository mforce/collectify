namespace Collectify.Infrastructure.Lookup;

/// <summary>
/// Provider-neutral suggestion the API returns to the client. ProviderKey is
/// opaque to the frontend; subsequent providers can use it to fetch full
/// details if/when we need a "get one" path beyond the searchable fields.
/// </summary>
public record MovieLookupResult(
    string Provider,
    string ProviderKey,
    string Title,
    string? OriginalTitle,
    int? Year,
    string? Director,
    int? RuntimeMinutes,
    string? Description,
    string? ImageUrl,
    string? Genres);

public record MusicLookupResult(
    string Provider,
    string ProviderKey,
    string Title,
    string ArtistName,
    int? Year,
    string? Label,
    string? Description,
    string? ImageUrl,
    string? Genres);

public record GameLookupResult(
    string Provider,
    string ProviderKey,
    string Title,
    string? Platform,
    int? Year,
    string? Publisher,
    string? Developer,
    string? Description,
    string? ImageUrl,
    string? Genres);
