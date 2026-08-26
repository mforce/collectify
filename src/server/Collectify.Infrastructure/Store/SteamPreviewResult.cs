namespace Collectify.Infrastructure.Store;

/// <summary>Outcome of a Steam owned-games preview fetch.</summary>
public enum SteamPreviewStatus
{
    /// <summary>No Steam connection for this owner.</summary>
    NotConnected,
    /// <summary>Provider returned a usable response (possibly empty).</summary>
    Ok,
    /// <summary>Provider error / outage — show an "unavailable, try again" state,
    /// NOT "you own nothing".</summary>
    Unavailable,
}

/// <summary>Preview payload: a status plus (for Ok) the owned titles.</summary>
public sealed record SteamPreviewResult(
    SteamPreviewStatus Status,
    IReadOnlyList<SteamOwnedTitle> Titles,
    bool Truncated,
    int Total);
