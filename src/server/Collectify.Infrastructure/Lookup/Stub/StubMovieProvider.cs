using Collectify.Infrastructure.Lookup.GiantBomb;
using Collectify.Infrastructure.Lookup.Upc;

namespace Collectify.Infrastructure.Lookup.Stub;

/// <summary>
/// Default <see cref="IGiantBombGameUpcClient"/> registered when the
/// real one isn't wired up. Always reports unconfigured + null so the
/// game provider's UPC fallback chain skips it gracefully.
/// </summary>
internal sealed class StubGiantBombGameUpcClient : IGiantBombGameUpcClient
{
    public string Name => "stub-giantbomb";
    public bool IsConfigured => false;

    public Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<UpcLookupResult?>(null);
}

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

    public Task<IReadOnlyList<MovieLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovieLookupResult>>(Array.Empty<MovieLookupResult>());
}

internal sealed class StubMusicProvider : IMusicMetadataProvider
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MusicLookupResult>>(Array.Empty<MusicLookupResult>());

    public Task<MusicLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult<MusicLookupResult?>(null);

    public Task<IReadOnlyList<MusicLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MusicLookupResult>>(Array.Empty<MusicLookupResult>());
}

internal sealed class StubGameProvider : IGameMetadataProvider
{
    public string Name => "stub";
    public bool IsConfigured => false;

    public Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GameLookupResult>>(Array.Empty<GameLookupResult>());

    public Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
        => Task.FromResult<GameLookupResult?>(null);

    public Task<IReadOnlyList<GameLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GameLookupResult>>(Array.Empty<GameLookupResult>());
}
