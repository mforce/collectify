using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Tmdb;

/// <summary>
/// IMovieMetadataProvider backed by themoviedb.org v3 /search/movie. Empty
/// query strings and unconfigured installs short-circuit to an empty result.
/// Every successful upstream call is cached in the LookupCacheEntry table
/// keyed by the lowercased query string, so repeat searches don't burn rate
/// limits or wait on the network.
/// </summary>
public sealed class TmdbMovieProvider : IMovieMetadataProvider
{
    public const string ProviderName = "tmdb";

    private readonly HttpClient _http;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<TmdbMovieProvider> _log;

    public TmdbMovieProvider(
        HttpClient http,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<TmdbMovieProvider> log)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _log = log;
    }

    public string Name => ProviderName;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Tmdb.ApiKey);

    public async Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];

        var cacheKey = "search:" + trimmed.ToLowerInvariant();

        var cached = await _cache.GetAsync<List<MovieLookupResult>>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        TmdbSearchResponse? body;
        try
        {
            var url = $"search/movie?query={Uri.EscapeDataString(trimmed)}&include_adult=false&api_key={Uri.EscapeDataString(_options.Tmdb.ApiKey!)}";
            body = await _http.GetFromJsonAsync<TmdbSearchResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "TMDB search failed for {Query}", trimmed);
            return [];
        }

        var mapped = (body?.Results ?? []).Select(Map).ToList();
        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }

    private MovieLookupResult Map(TmdbMovieSummary s) => new(
        Provider: ProviderName,
        ProviderKey: s.Id.ToString(),
        Title: s.Title ?? s.OriginalTitle ?? string.Empty,
        OriginalTitle: s.OriginalTitle,
        Year: ParseYear(s.ReleaseDate),
        Director: null,
        RuntimeMinutes: null,
        Description: s.Overview,
        ImageUrl: BuildImageUrl(s.PosterPath),
        Genres: null);

    private static int? ParseYear(string? releaseDate)
    {
        if (releaseDate is null || releaseDate.Length < 4) return null;
        return int.TryParse(releaseDate[..4], out var year) ? year : null;
    }

    private string? BuildImageUrl(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath)) return null;
        var baseUrl = _options.Tmdb.ImageBaseUrl.TrimEnd('/');
        return $"{baseUrl}{posterPath}";
    }
}
