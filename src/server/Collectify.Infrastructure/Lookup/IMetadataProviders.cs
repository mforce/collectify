namespace Collectify.Infrastructure.Lookup;

using Collectify.Domain.Enums;
using Collectify.Domain.Metadata;

/// <summary>
/// Movie-specific metadata provider: the generic provider contract plus the
/// IMDB-id capability (<see cref="GetByImdbIdAsync"/>). TMDB and the movie stub
/// implement this; the lookup endpoints inject it.
/// </summary>
public interface IMovieMetadataProvider : IMetadataProvider<MovieLookupResult>
{
    /// <summary>
    /// Lookup by an IMDB id (the <c>tt…</c> shape). Implementations resolve the
    /// IMDB id to their own provider key under the hood and return the same shape
    /// as <see cref="IMetadataProvider{T}.GetByIdAsync"/>. Returns null when the
    /// provider can't resolve it; tests may also rely on null when
    /// <see cref="IMetadataProvider{T}.IsConfigured"/> is false.
    /// </summary>
    Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default);
}

/// <summary>
/// Game-specific metadata provider: the generic provider contract plus the
/// platform-scoped search capability (<see cref="SearchByPlatformAsync"/>). IGDB
/// and the game stub implement this; the lookup endpoints and the IGDB backfill
/// runner inject it.
/// </summary>
public interface IGameMetadataProvider : IMetadataProvider<GameLookupResult>
{
    /// <summary>
    /// Search restricted to results that map to <paramref name="platform"/>.
    /// The default implementation filters <see cref="IMetadataProvider{T}.SearchAsync"/>
    /// in memory; providers that fetch platform data natively (IGDB returns several
    /// platforms per release) may override to filter at the source and/or use a
    /// platform-scoped cache key. Empty when no result matches the platform.
    /// </summary>
    async Task<IReadOnlyList<GameLookupResult>> SearchByPlatformAsync(
        string query,
        GamePlatform platform,
        CancellationToken ct = default)
    {
        var results = await SearchAsync(query, ct).ConfigureAwait(false);
        return results.Where(r => r.IsOn(platform)).ToList();
    }
}
