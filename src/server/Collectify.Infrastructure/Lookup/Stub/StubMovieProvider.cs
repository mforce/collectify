namespace Collectify.Infrastructure.Lookup.Stub;

/// <summary>
/// Fallback provider used when no concrete movie provider has been registered.
/// Always returns empty so the lookup endpoint can degrade gracefully -- the
/// frontend just sees "no suggestions" instead of a 500.
/// </summary>
internal sealed class StubMovieProvider : IMovieMetadataProvider
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovieLookupResult>>(Array.Empty<MovieLookupResult>());

    public Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult<MovieLookupResult?>(null);

    public Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default)
        => Task.FromResult<MovieLookupResult?>(null);
}

internal sealed class StubMusicProvider : IMusicMetadataProvider
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MusicLookupResult>>(Array.Empty<MusicLookupResult>());

    public Task<MusicLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult<MusicLookupResult?>(null);
}

internal sealed class StubGameProvider : IGameMetadataProvider
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GameLookupResult>>(Array.Empty<GameLookupResult>());
}
