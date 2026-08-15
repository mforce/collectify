using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

/// <summary>
/// The import ledger / provenance record: "which store titles are already in
/// this owner's collection". The unique (OwnerId, Store, ExternalGameId) key
/// makes re-imports idempotent and survives disconnect; <see cref="GameId"/>
/// is the link to the created <see cref="Game"/>, if any.
///
/// State is linear:
///   * row absent            -> importable (never seen)
///   * row present, GameId!=null -> imported
///   * row present, GameId==null -> the linked Game was deleted; importable again
///
/// <see cref="ExternalAccountId"/> is informational only and deliberately NOT
/// part of the idempotency key, so a user who relinks a different account
/// still cannot create duplicates.
/// </summary>
public class GameStoreOwnedTitle
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DigitalStore Store { get; set; } = DigitalStore.Steam;
    /// <summary>Provider external game id (Steam appid), canonical decimal string.</summary>
    public string ExternalGameId { get; set; } = string.Empty;
    /// <summary>Which linked account this title came from (informational).</summary>
    public string? ExternalAccountId { get; set; }
    /// <summary>Provider-supplied title, used for display/audit.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>FK to <see cref="Game"/> when imported; null until imported or after its Game is deleted.</summary>
    public int? GameId { get; set; }
    public DateTime? ImportedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
