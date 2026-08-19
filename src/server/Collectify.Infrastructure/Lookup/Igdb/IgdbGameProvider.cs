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

    /// <summary>
    /// Bumped when the cached <see cref="GameLookupResult"/> shape changes OR
    /// when a cached query's filtering semantics change for the same key. The
    /// lookup cache is keyed by (Provider, Key) with no schema guard, so a
    /// DTO field added/removed/renamed would otherwise silently serve stale,
    /// wrongly-shaped rows (e.g. a pre-<c>Platforms</c> JSON deserializes that
    /// field to its default empty set), and a semantics change (e.g. PC-scoped
    /// searches now include the Linux IGDB id, so old entries fetched with
    /// Windows-only filtering would keep excluding Linux titles for the TTL)
    /// would serve stale results. Versioning the key forces a refresh once
    /// and prevents ever serving an out-of-date cached result again.
    /// </summary>
    private const int CacheSchemaVersion = 3;

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
        => await SearchCoreAsync(query, filter: null, ct);

    public async Task<IReadOnlyList<GameLookupResult>> SearchByPlatformAsync(
        string query,
        GamePlatform platform,
        CancellationToken ct = default)
        => await SearchCoreAsync(query, platform, ct);

    /// <summary>
    /// Shared search. When <paramref name="filter"/> is set the cache key is
    /// platform-scoped (so a PC-scoped search never reuses — or gets reused by —
    /// a result set cached for an unscoped query) AND the query is filtered to
    /// that platform AT THE SOURCE via Apicalypse <c>where platforms = (ids)</c>.
    ///
    /// Filtering at the source matters: IGDB's fuzzy <c>search</c> ranks the
    /// top N results across ALL platforms, and re-releases crowd out the exact
    /// platform's release (a PC "The Witcher 3" gets buried under console and
    /// bundled SKUs). A <c>where platforms = (6)</c> clause (for Pc, <c>(6,3)</c>
    /// — Windows + Linux) makes IGDB run the fuzzy search within just that
    /// platform, so the right SKU is included.
    /// The in-memory <see cref="GameLookupResult.IsOn"/> filter is kept as a
    /// safety net for platforms we can't map to an IGDB id (and for
    /// source-platforms that are multi-id, e.g. Android/iOS under Mobile).
    /// </summary>
    private async Task<IReadOnlyList<GameLookupResult>> SearchCoreAsync(
        string query,
        GamePlatform? filter,
        CancellationToken ct)
    {
        if (!IsConfigured) return [];
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];

        // Platform-scoped searches must not share a cache entry with an
        // unscoped (or other-platform) one, or the filter is silently wrong.
        // Version prefix busts stale pre-Platforms rows (see CacheSchemaVersion).
        var cacheKey = filter is { } p
            ? $"v{CacheSchemaVersion}:search:{trimmed.ToLowerInvariant()}|{p}"
            : $"v{CacheSchemaVersion}:search:" + trimmed.ToLowerInvariant();
        var cached = await _cache.GetAsync<List<GameLookupResult>>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        // Apicalypse "search" is a fuzzy match. Quotes are required around
        // the query and any embedded quotes have to be escaped or IGDB
        // returns 400. When we know the platform's IGDB id(s) we append a
        // `where platforms = (id,...)` clause so IGDB filters the search to
        // that platform rather than relying purely on post-hoc in-memory
        // filtering of an all-platform top-N (which starves the exact SKU).
        var ids = filter is { } f ? IgdbPlatformIds(f) : [];
        var platformClause = ids.Count > 0
            ? $" where platforms = ({string.Join(",", ids)});"
            : ";";
        var body = $"search \"{Escape(trimmed)}\"; fields {Fields}; limit 10;{platformClause}";
        var games = await PostGamesAsync(body, ct);
        if (games is null) return [];

        var mapped = games.Select(Map).ToList();
        if (filter is { } f2)
            mapped = mapped.Where(r => r.IsOn(f2)).ToList();
        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }

    /// <summary>
    /// Maps our <see cref="GamePlatform"/> enum to IGDB's numeric platform
    /// id(s) (stable, from IGDB's /platforms endpoint). Used to build the
    /// <c>where platforms = (...)</c> source filter for platform-scoped
    /// searches. Returns empty for platforms with no canonical id
    /// (Mobile splits Android/iOS; Other is "unknown"). Note Steam Deck is no
    /// longer a platform (#103) and Switch 2 maps back to id 508. See
    /// https://api-docs.igdb.com and the public platform id lists.
    ///
    /// PC is a family: IGDB has separate ids for PC / Microsoft Windows (6),
    /// Linux (3) and Mac (14). We model Mac as its own platform, but Linux
    /// folds into <see cref="GamePlatform.Pc"/> (#102) — and IGDB releases a
    /// game on Linux (id 3) may have NO Windows id (6), so a PC-scoped search
    /// must include BOTH 6 and 3 or Linux-only titles are excluded upstream
    /// before the in-memory <see cref="GameLookupResult.IsOn"/> filter runs.
    ///
    /// Apicalypse <c>where platforms = (6,3)</c> means "contains ANY of"
    /// (the OR form); <c>[6,3]</c> would mean "contains ALL of" (inclusive-AND)
    /// and exclude Windows-only titles with no Linux port. Do NOT swap the
    /// bracket styles — the multi-id form relies on OR semantics.
    /// </summary>
    private static IReadOnlyList<int> IgdbPlatformIds(GamePlatform platform)
    {
        return platform switch
        {
            // PC = Windows (6) + Linux (3); Mac (14) is its own platform.
            GamePlatform.Pc => [6, 3],
            GamePlatform.Mac => [14],
            GamePlatform.XboxOriginal => [11],
            GamePlatform.Xbox360 => [12],
            GamePlatform.XboxOne => [49],
            GamePlatform.XboxSeriesXS => [169],
            GamePlatform.Ps1 => [7],
            GamePlatform.Ps2 => [8],
            GamePlatform.Ps3 => [9],
            GamePlatform.Ps4 => [48],
            GamePlatform.Ps5 => [167],
            GamePlatform.Psp => [38],
            GamePlatform.PsVita => [46],
            GamePlatform.Nes => [18],
            GamePlatform.Snes => [19],
            GamePlatform.N64 => [4],
            GamePlatform.GameCube => [21],
            GamePlatform.Wii => [5],
            GamePlatform.WiiU => [41],
            GamePlatform.Switch => [130],
            GamePlatform.Switch2 => [508],
            GamePlatform.GameBoy => [33],
            GamePlatform.GameBoyColor => [22],
            GamePlatform.GameBoyAdvance => [24],
            GamePlatform.NintendoDs => [20],
            GamePlatform.Nintendo3Ds => [37],
            GamePlatform.SegaGenesis => [29],
            GamePlatform.SegaSaturn => [32],
            GamePlatform.SegaDreamcast => [23],
            _ => [], // Other (unknown) and Mobile (Android/iOS split) — no single canonical id
        };
    }

    public async Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (string.IsNullOrWhiteSpace(providerKey)) return null;
        if (!long.TryParse(providerKey.Trim(), out var id) || id <= 0) return null;

        var cacheKey = $"v{CacheSchemaVersion}:id:" + id.ToString();
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

        var cacheKey = $"v{CacheSchemaVersion}:barcode:" + trimmed;
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

        var mappedPlatforms = g.Platforms?
            .Select(p => GamePlatformMapping.TryParse(p.Name))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList() ?? [];

        return new GameLookupResult(
            Provider: ProviderName,
            ProviderKey: g.Id.ToString(),
            Title: g.Name ?? string.Empty,
            // IGDB returns N platforms per release. Surface the first one that
            // maps cleanly to our enum as `Platform` (for form dropdown compat);
            // null when none map, so the dropdown stays unset rather than
            // defaulting to Other. The mapping resolver is normalisation-tolerant
            // -- "PlayStation 5", "playstation-5", " PS_5 " all hit one value.
            Platform: mappedPlatforms.Count > 0 ? mappedPlatforms[0] : null,
            Year: ToYear(g.FirstReleaseDate),
            Publisher: pubs is { Count: > 0 } ? string.Join(", ", pubs) : null,
            Developer: devs is { Count: > 0 } ? string.Join(", ", devs) : null,
            Description: g.Summary,
            ImageUrl: g.Cover?.ImageId is { Length: > 0 } imageId
                ? $"{CoverImageBase}/{imageId}.jpg"
                : null,
            Genres: g.Genres is { Count: > 0 }
                ? string.Join(", ", g.Genres.Where(x => x.Name is not null).Select(x => x.Name!))
                : null)
        {
            // The full mapped platform set, not just the first — used for
            // platform-scoped matching and edit-page prioritisation.
            Platforms = mappedPlatforms,
        };
    }

    private static int? ToYear(long? unixSeconds)
    {
        if (unixSeconds is null) return null;
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).Year;
    }
}
