using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

/// <summary>
/// A connected/authorised digital store account (one row per owner + store).
/// Represents "this Collectify user has linked their Steam (or later Xbox /
/// PSN) account". The connection is bounded to a single store per owner via
/// the unique (OwnerId, Store) index.
/// </summary>
public class GameStoreConnection
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DigitalStore Store { get; set; } = DigitalStore.Steam;
    /// <summary>Provider-neutral external account id (SteamID64 for Steam).</summary>
    public string ExternalAccountId { get; set; } = string.Empty;
    public string? ExternalDisplayName { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set on successful owned-games fetch; underpins future sync.</summary>
    public DateTime? LastSyncedAt { get; set; }
}
