namespace Collectify.Infrastructure.Store;

/// <summary>Steam Web API surface used by the import feature (testable seam).</summary>
public interface ISteamClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, CancellationToken ct = default);
    Task<string?> GetPersonaNameAsync(string steamId64, CancellationToken ct = default);
}
