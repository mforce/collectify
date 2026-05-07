namespace Collectify.Infrastructure.Lookup;

/// <summary>
/// Per-domain provider contracts. Each concrete provider (TMDB,
/// MusicBrainz, IGDB, etc.) implements one of these; the lookup endpoint
/// asks the registered provider for that media type for suggestions.
/// IsConfigured lets a provider report "no API key set, skip me" so the
/// host can degrade gracefully without throwing.
/// </summary>
public interface IMovieMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Direct lookup by the provider's identifier (e.g. a TMDB id). Returns
    /// null when the provider doesn't recognise the id; tests may also rely
    /// on null when <see cref="IsConfigured"/> is false.
    /// </summary>
    Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default);
}

public interface IMusicMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default);
}

public interface IGameMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default);
}
