using Collectify.Infrastructure.Lookup;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IGameMetadataProvider"/>. Mirrors the
/// scripted movie/music providers so endpoint tests can compose
/// deterministic responses without standing up the IGDB stack.
/// </summary>
public sealed class ScriptedGameProvider : IGameMetadataProvider
{
    public string Name { get; init; } = "igdb";
    public bool IsConfigured { get; init; } = true;
    public IReadOnlyList<GameLookupResult> SearchResults { get; init; } = [];
    public GameLookupResult? ById { get; init; }

    public Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(SearchResults);

    public Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult(ById);

    public static ScriptedGameProvider WithFoundResult(GameLookupResult result) =>
        new() { ById = result };

    public static ScriptedGameProvider NotFound() =>
        new() { ById = null };
}
