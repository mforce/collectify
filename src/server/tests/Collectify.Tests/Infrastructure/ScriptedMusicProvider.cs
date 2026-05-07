using Collectify.Infrastructure.Lookup;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IMusicMetadataProvider"/>. Lets each endpoint
/// test compose a deterministic provider response (configured / not, search
/// result list, by-id result) without hitting the real MusicBrainz provider
/// or its HTTP client.
/// </summary>
public sealed class ScriptedMusicProvider : IMusicMetadataProvider
{
    public string Name { get; init; } = "musicbrainz";
    public bool IsConfigured { get; init; } = true;
    public IReadOnlyList<MusicLookupResult> SearchResults { get; init; } = [];
    public MusicLookupResult? ById { get; init; }

    public Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(SearchResults);

    public Task<MusicLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult(ById);

    public static ScriptedMusicProvider WithFoundResult(MusicLookupResult result) =>
        new() { ById = result };

    public static ScriptedMusicProvider NotFound() =>
        new() { ById = null };
}
