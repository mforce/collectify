namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Hands out Twitch OAuth bearer tokens for IGDB. Pulled out of the game
/// provider so tests can fake the token (no live Twitch round-trip) and so
/// the in-memory cache survives the typed HttpClient's transient lifetime.
/// </summary>
public interface IIgdbAuth
{
    /// <summary>The Twitch client id passed alongside the bearer token.</summary>
    string? ClientId { get; }

    /// <summary>
    /// Returns a cached token if available, otherwise fetches a fresh one.
    /// Returns null if either credential isn't configured or the Twitch
    /// endpoint refuses the request. Pass <c>forceRefresh</c> to invalidate
    /// the current token (used after a 401 from IGDB).
    /// </summary>
    Task<string?> GetTokenAsync(bool forceRefresh = false, CancellationToken ct = default);
}
