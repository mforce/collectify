using Collectify.Infrastructure.Store;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// In-memory ISteamClient for tests: returns the configured owned games and
/// persona name without hitting Steam.
/// </summary>
public sealed class ScriptedSteamClient : ISteamClient
{
    public bool IsConfigured { get; set; } = true;
    public IReadOnlyList<SteamOwnedGame> OwnedGames { get; set; } = [];
    public SteamFetchStatus FetchStatus { get; set; } = SteamFetchStatus.Ok;
    public string? PersonaName { get; set; } = "TestPersona";
    /// <summary>Rich metadata keyed by appid for GetItemsAsync. Empty = none.</summary>
    public Dictionary<uint, SteamStoreBrowseItem> StoreItems { get; set; } = new();

    public Task<SteamGamesResult> GetOwnedGamesAsync(string steamId64, CancellationToken ct = default)
        => Task.FromResult(new SteamGamesResult(FetchStatus, OwnedGames));

    public Task<string?> GetPersonaNameAsync(string steamId64, CancellationToken ct = default)
        => Task.FromResult(PersonaName);

    public Task<IReadOnlyList<SteamStoreBrowseItem>> GetItemsAsync(IReadOnlyCollection<uint> appIds, CancellationToken ct = default)
    {
        var matched = StoreItems.Where(kv => appIds.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        return Task.FromResult<IReadOnlyList<SteamStoreBrowseItem>>(matched);
    }
}

/// <summary>
/// In-memory ISteamOpenIdVerifier for tests. Returns the configured verified
/// steamId, or null to simulate a rejected assertion.
/// </summary>
public sealed class ScriptedSteamOpenIdVerifier : ISteamOpenIdVerifier
{
    public bool IsConfigured { get; set; } = true;
    public string? VerifiedSteamId { get; set; } = "76561198000000000";

    public Task<string?> VerifyAsync(
        IReadOnlyDictionary<string, string> openIdParams,
        string expectedReturnTo,
        CancellationToken ct = default)
        => Task.FromResult(VerifiedSteamId);
}
