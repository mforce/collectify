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
    public string? PersonaName { get; set; } = "TestPersona";

    public Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, CancellationToken ct = default)
        => Task.FromResult(OwnedGames);

    public Task<string?> GetPersonaNameAsync(string steamId64, CancellationToken ct = default)
        => Task.FromResult(PersonaName);
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
