namespace Collectify.Infrastructure.Store;

/// <summary>Whether a Steam owned-games fetch succeeded or was unavailable.</summary>
public enum SteamFetchStatus
{
    /// <summary>Provider returned a usable response (may have been empty).</summary>
    Ok,
    /// <summary>Provider error / outage / config issue — an empty list here must
    /// NOT be shown as "you own nothing" (private/offline/unavailable).</summary>
    Unavailable,
}

/// <summary>Result of a Steam owned-games fetch so callers can tell a real
/// "empty/private library" apart from a provider failure.</summary>
public sealed record SteamGamesResult(SteamFetchStatus Status, IReadOnlyList<SteamOwnedGame> Games);
