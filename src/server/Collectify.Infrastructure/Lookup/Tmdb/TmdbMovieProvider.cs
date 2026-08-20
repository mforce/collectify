using System.Net;
using System.Net.Http.Json;
using Collectify.Infrastructure.Lookup.Upc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Tmdb;

/// <summary>
/// IMovieMetadataProvider backed by themoviedb.org v3 /search/movie. Empty
/// query strings and unconfigured installs short-circuit to an empty result.
/// Every successful upstream call is cached through <see cref="ILookupCache"/>
/// keyed by the lowercased query string, so repeat searches don't burn rate
/// limits or wait on the network.
/// </summary>
public sealed class TmdbMovieProvider : IMovieMetadataProvider
{
    public const string ProviderName = "tmdb";

    private readonly HttpClient _http;
    private readonly IUpcLookupClient _upc;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<TmdbMovieProvider> _log;

    public TmdbMovieProvider(
        HttpClient http,
        IUpcLookupClient upc,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<TmdbMovieProvider> log)
    {
        _http = http;
        _upc = upc;
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

        var cached = await _cache.GetAsync<List<MovieLookupResult>>(ProviderName, cacheKey, ct);
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
        await _cache.SetAsync(ProviderName, cacheKey, mapped, _options.CacheTtl, ct);
        return mapped;
    }

    public async Task<MovieLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (string.IsNullOrWhiteSpace(providerKey)) return null;

        // Separate cache namespace from search so the two flows can't poison
        // each other's results.
        var cacheKey = "id:" + providerKey;
        var cached = await _cache.GetAsync<MovieLookupResult>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        TmdbMovieDetail? detail;
        try
        {
            // append_to_response=credits saves us a /credits round trip and
            // gives us director + runtime in the same payload.
            var url = $"movie/{Uri.EscapeDataString(providerKey)}?api_key={Uri.EscapeDataString(_options.Tmdb.ApiKey!)}&append_to_response=credits";
            detail = await _http.GetFromJsonAsync<TmdbMovieDetail>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // TMDB doesn't recognise the id; not an error worth shouting about.
            return null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "TMDB lookup-by-id failed for {Id}", providerKey);
            return null;
        }

        if (detail is null) return null;
        var mapped = MapDetail(detail);
        await _cache.SetAsync(ProviderName, cacheKey, mapped, _options.CacheTtl, ct);
        return mapped;
    }

    public async Task<MovieLookupResult?> GetByImdbIdAsync(string imdbId, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        var trimmed = (imdbId ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;

        // Separate cache namespace from id-lookup and search so an unrelated
        // string that happens to match (e.g. "tt27205") can't satisfy a
        // different lookup.
        var cacheKey = "imdb:" + trimmed;
        var cached = await _cache.GetAsync<MovieLookupResult>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        TmdbFindResponse? body;
        try
        {
            // /find returns matches across categories (movie / tv / person);
            // we only consume movie_results.
            var url = $"find/{Uri.EscapeDataString(trimmed)}?external_source=imdb_id&api_key={Uri.EscapeDataString(_options.Tmdb.ApiKey!)}";
            body = await _http.GetFromJsonAsync<TmdbFindResponse>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "TMDB find-by-imdb failed for {Id}", trimmed);
            return null;
        }

        var first = body?.MovieResults?.FirstOrDefault();
        if (first is null) return null;

        // Resolve the TMDB id into the full detail. Reuses GetByIdAsync's
        // cache, so a follow-up by-tmdb-id call for the same movie is free.
        var detail = await GetByIdAsync(first.Id.ToString(), ct);
        if (detail is null) return null;

        await _cache.SetAsync(ProviderName, cacheKey, detail, _options.CacheTtl, ct);
        return detail;
    }

    public async Task<IReadOnlyList<MovieLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = (barcode ?? string.Empty).Trim();
        if (trimmed.Length == 0) return [];

        // Cache the final list per barcode so a second scan of the same
        // disc is a single DB hit -- no UPC lookup, no TMDB title search.
        var cacheKey = "barcode:" + trimmed;
        var cached = await _cache.GetAsync<List<MovieLookupResult>>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        // TMDB doesn't index barcodes. Resolve the UPC via UPCitemdb to a
        // product title, then run our regular title search. UPC client
        // already caches its own response, so a re-scan stays under the
        // 100/day trial quota even before this barcode-cache layer.
        var upcHit = await _upc.LookupAsync(trimmed, ct);
        if (upcHit is null) return [];

        var hits = await SearchAsync(upcHit.Title, ct);
        await _cache.SetAsync(ProviderName, cacheKey, hits.ToList(), _options.CacheTtl, ct);
        return hits;
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
        Genres: null,
        ReleaseDate: ParseDateOnly(s.ReleaseDate),
        Cast: null,
        ProviderRating: null);

    private MovieLookupResult MapDetail(TmdbMovieDetail d) => new(
        Provider: ProviderName,
        ProviderKey: d.Id.ToString(),
        Title: d.Title ?? d.OriginalTitle ?? string.Empty,
        OriginalTitle: d.OriginalTitle,
        Year: ParseYear(d.ReleaseDate),
        Director: ExtractDirector(d.Credits),
        RuntimeMinutes: d.Runtime,
        Description: d.Overview,
        ImageUrl: BuildImageUrl(d.PosterPath),
        Genres: null,
        ReleaseDate: ParseDateOnly(d.ReleaseDate),
        Cast: ExtractTopCast(d.Credits),
        ProviderRating: d.VoteAverage);

    private static DateOnly? ParseDateOnly(string? releaseDate)
    {
        if (releaseDate is null) return null;
        return DateOnly.TryParseExact(releaseDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    private static string? ExtractTopCast(TmdbCredits? credits)
    {
        if (credits?.Cast is null) return null;
        var names = credits.Cast.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().Take(5).ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static string? ExtractDirector(TmdbCredits? credits)
    {
        if (credits?.Crew is null) return null;
        // Co-directed films get joined by " & " for clarity. Order is
        // whatever TMDB returns -- usually the primary director first.
        var directors = credits.Crew
            .Where(c => string.Equals(c.Job, "Director", StringComparison.Ordinal))
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList();
        return directors.Count == 0 ? null : string.Join(" & ", directors);
    }

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
