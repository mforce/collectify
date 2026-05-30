using Collectify.Domain.Enums;

namespace Collectify.Infrastructure.Lookup;

/// <summary>Common contract for all lookup result types.</summary>
public interface ILookupResult
{
    string ProviderKey { get; }
}

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
    string? Genres) : ILookupResult;

public record MusicLookupResult(
    string Provider,
    string ProviderKey,
    string Title,
    string ArtistName,
    int? Year,
    string? Label,
    string? Description,
    string? ImageUrl,
    string? Genres) : ILookupResult;

public record GameLookupResult(
    string Provider,
    string ProviderKey,
    string Title,
    /// <summary>
    /// Canonical platform if the provider's first-listed platform name
    /// resolved to a <see cref="GamePlatform"/>; null when we can't tell.
    /// Falling back to null instead of <c>Other</c> keeps the form's
    /// dropdown unselected so the user notices and picks one.
    /// </summary>
    GamePlatform? Platform,
    int? Year,
    string? Publisher,
    string? Developer,
    string? Description,
    string? ImageUrl,
    string? Genres) : ILookupResult;
