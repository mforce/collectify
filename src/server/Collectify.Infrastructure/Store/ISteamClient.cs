namespace Collectify.Infrastructure.Store;

/// <summary>Steam Web API surface used by the import feature (testable seam).</summary>
public interface ISteamClient
{
    bool IsConfigured { get; }

    Task<SteamGamesResult> GetOwnedGamesAsync(string steamId64, CancellationToken ct = default);
    Task<string?> GetPersonaNameAsync(string steamId64, CancellationToken ct = default);

    /// <summary>
    /// Bulk rich-metadata lookup via the keyless storefront endpoint
    /// <c>IStoreBrowseService/GetItems</c> (developer/publisher/release date/
    /// description for up to <paramref name="appIds"/> in one request).
    /// Returns an empty list on any failure — callers must fail soft.
    /// </summary>
    Task<IReadOnlyList<SteamStoreBrowseItem>> GetItemsAsync(IReadOnlyCollection<uint> appIds, CancellationToken ct = default);
}
