using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup.Upc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// IGameMetadataProvider backed by IGDB's /v4/games endpoint. Auth flows
/// through Twitch OAuth client-credentials -- both a Twitch client id and
/// secret must be configured or every entry point short-circuits and the
/// lookup endpoint reports configured=false.
///
/// The wire protocol is "Apicalypse": a domain-specific text body posted
/// (not GET'd) to /v4/games. Search uses <c>search "x"; fields …; limit 10;</c>;
/// by-id swaps the search clause for <c>where id = N;</c>. We always ask
/// for the same field set so the cached JSON is the same shape regardless
/// of entry point.
///
/// Cover art is served from <c>images.igdb.com</c> using the cover's
/// <c>image_id</c> at the <c>t_cover_big</c> preset (~264x352). When a game
/// has no cover the URL is null; the browser shows the fallback placeholder.
/// </summary>
public sealed class IgdbGameProvider : IGameMetadataProvider
{
    public const string ProviderName = "igdb";
    public const string HttpClientName = "igdb";

    // IGDB image URLs are "https://images.igdb.com/igdb/image/upload/{size}/{image_id}.jpg".
    // t_cover_big is the canonical "box art at form thumbnail size" preset.
    private const string CoverImageBase = "https://images.igdb.com/igdb/image/upload/t_cover_big";

    // Apicalypse field list. Kept in one place so search and by-id stay in
    // sync; mismatched fields would split the cache and bloat storage.
    private const string Fields =
        "name,first_release_date,cover.image_id,summary," +
        "involved_companies.company.name,involved_companies.developer,involved_companies.publisher," +
        "platforms.name,genres.name";

    private readonly HttpClient _http;
    private readonly IIgdbAuth _auth;
    private readonly IUpcLookupClient _upc;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<IgdbGameProvider> _log;

    public IgdbGameProvider(
        HttpClient http,
        IIgdbAuth auth,
        IUpcLookupClient upc,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<IgdbGameProvider> log)
    {
        _http = http;
        _auth = auth;
        _upc = upc;
        _cache = cache;
        _options = options.Value;
        _log = log;
    }

    public string Name => ProviderName;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Igdb.TwitchClientId) &&
        !string.IsNullOrWhiteSpace(_options.Igdb.TwitchClientSecret);

    public async Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];

        var cacheKey = "search:" + trimmed.ToLowerInvariant();
        var cached = await _cache.GetAsync<List<GameLookupResult>>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        // Apicalypse "search" is a fuzzy match. Quotes are required around
        // the query and any embedded quotes have to be escaped or IGDB
        // returns 400.
        var body = $"search \"{Escape(trimmed)}\"; fields {Fields}; limit 10;";
        var games = await PostGamesAsync(body, ct);
        if (games is null) return [];

        var mapped = games.Select(Map).ToList();
        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }

    public async Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (string.IsNullOrWhiteSpace(providerKey)) return null;
        if (!long.TryParse(providerKey.Trim(), out var id) || id <= 0) return null;

        var cacheKey = "id:" + id.ToString();
        var cached = await _cache.GetAsync<GameLookupResult>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        var body = $"where id = {id}; fields {Fields}; limit 1;";
        var games = await PostGamesAsync(body, ct);
        if (games is null || games.Count == 0) return null;

        var mapped = Map(games[0]);
        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }

    public async Task<IReadOnlyList<GameLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = (barcode ?? string.Empty).Trim();
        if (trimmed.Length == 0) return [];

        var cacheKey = "barcode:" + trimmed;
        var cached = await _cache.GetAsync<List<GameLookupResult>>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        // IGDB has no barcode field; resolve via UPCitemdb to a product
        // title, then run our regular Apicalypse search. UPC lookups are
        // independently cached so a second scan is free even before this
        // wrapping cache layer kicks in.
        var upcHit = await _upc.LookupAsync(trimmed, ct);
        if (upcHit is null) return [];

        var hits = await SearchAsync(upcHit.Title, ct);
        await _cache.SetAsync(ProviderName, cacheKey, hits.ToList(), ct);
        return hits;
    }

    private async Task<IReadOnlyList<IgdbGame>?> PostGamesAsync(string apicalypseBody, CancellationToken ct)
    {
        var token = await _auth.GetTokenAsync(forceRefresh: false, ct);
        if (string.IsNullOrEmpty(token))
        {
            _log.LogWarning("IGDB request skipped: no Twitch token available");
            return null;
        }

        var resp = await SendAsync(apicalypseBody, token!, ct);

        // Single retry on 401: assume the cached token expired and grab a
        // fresh one. A second 401 means credentials are bad -- give up so
        // we don't loop.
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            resp.Dispose();
            var refreshed = await _auth.GetTokenAsync(forceRefresh: true, ct);
            if (string.IsNullOrEmpty(refreshed)) return null;
            resp = await SendAsync(apicalypseBody, refreshed!, ct);
        }

        try
        {
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("IGDB games endpoint returned {Status}", resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<IReadOnlyList<IgdbGame>>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "IGDB response parse failed");
            return null;
        }
        finally
        {
            resp.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string body, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "games")
        {
            // IGDB expects the Apicalypse query as a raw text/plain body.
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        req.Headers.Add("Client-ID", _auth.ClientId);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.ParseAdd("application/json");
        return await _http.SendAsync(req, ct);
    }

    private static string Escape(string raw) =>
        raw.Replace("\\", "\\\\", StringComparison.Ordinal)
           .Replace("\"", "\\\"", StringComparison.Ordinal);

    private GameLookupResult Map(IgdbGame g)
    {
        var devs = g.InvolvedCompanies?.Where(c => c.Developer && c.Company?.Name is not null)
                                       .Select(c => c.Company!.Name!).Distinct().ToList();
        var pubs = g.InvolvedCompanies?.Where(c => c.Publisher && c.Company?.Name is not null)
                                       .Select(c => c.Company!.Name!).Distinct().ToList();

        return new GameLookupResult(
            Provider: ProviderName,
            ProviderKey: g.Id.ToString(),
            Title: g.Name ?? string.Empty,
            // IGDB returns N platforms per release. Walk them in order
            // and surface the first one that maps cleanly to our enum;
            // if none does, leave it null so the form's dropdown stays
            // unselected (better than auto-defaulting to Other). The
            // mapping resolver is normalisation-tolerant -- "PlayStation
            // 5", "playstation-5", " PS_5 " all hit the same value.
            Platform: g.Platforms?
                .Select(p => GamePlatformMapping.TryParse(p.Name))
                .FirstOrDefault(v => v.HasValue),
            Year: ToYear(g.FirstReleaseDate),
            Publisher: pubs is { Count: > 0 } ? string.Join(", ", pubs) : null,
            Developer: devs is { Count: > 0 } ? string.Join(", ", devs) : null,
            Description: g.Summary,
            ImageUrl: g.Cover?.ImageId is { Length: > 0 } imageId
                ? $"{CoverImageBase}/{imageId}.jpg"
                : null,
            Genres: g.Genres is { Count: > 0 }
                ? string.Join(", ", g.Genres.Where(x => x.Name is not null).Select(x => x.Name!))
                : null);
    }

    private static int? ToYear(long? unixSeconds)
    {
        if (unixSeconds is null) return null;
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).Year;
    }
}
