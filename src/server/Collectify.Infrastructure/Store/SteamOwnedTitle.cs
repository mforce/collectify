namespace Collectify.Infrastructure.Store;

/// <summary>Provider import state for a single owned title, used by the preview API.</summary>
public enum SteamTitleImportState
{
    /// <summary>Not in this owner's ledger (or its Game was deleted) — importable.</summary>
    Importable,
    /// <summary>Already in this owner's collection.</summary>
    Imported,
}

/// <summary>
/// A normalized owned-title row presented to the preview API. Derived from the
/// trusted Steam <c>GetOwnedGames</c> fetch joined against the owner's ledger.
/// </summary>
public sealed record SteamOwnedTitle(
    string ExternalGameId,
    string Title,
    long PlaytimeMinutes,
    string? IconUrl,
    SteamTitleImportState State);

/// <summary>Result of an import batch, reported per selected ID.</summary>
public sealed record SteamImportResultItem(string ExternalGameId, bool Imported, bool AlreadyImported);
