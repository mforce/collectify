namespace Collectify.Domain.Entities;

/// <summary>
/// A one-time, expiring Steam OpenID auth request. The server stores only a
/// hash of the random <c>state</c> token that travels in the OpenID
/// <c>return_to</c> query string, binding that token to a single owner. The
/// row is consumed atomically on a successful callback (replay-safe) and
/// swept by the startup garbage-collector once expired.
/// </summary>
public class SteamAuthRequest
{
    /// <summary>Hex of the SHA-256 of the plaintext state token.</summary>
    public string StateHash { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
}
