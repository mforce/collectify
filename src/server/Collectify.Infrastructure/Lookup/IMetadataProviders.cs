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
