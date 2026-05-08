using System.Net.Http.Json;
using Collectify.Infrastructure.Lookup.Upc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.GiantBomb;

/// <summary>
/// IGiantBombGameUpcClient backed by giantbomb.com's
/// /api/releases/?filter=upc: endpoint. GiantBomb is community-curated
/// and indexes UPCs for a far broader catalogue of console / cartridge
/// releases than UPCitemdb's generic retail database, so the IGDB game
/// provider falls back here when UPCitemdb misses.
///
/// GiantBomb requires both a free API key and a contact User-Agent on
/// every request (returns 403 without a UA). Unset = IsConfigured is
/// false and the client short-circuits before ever calling the wire.
///
/// Each lookup is cached under <c>giantbomb / barcode:&lt;upc&gt;</c>; a
/// re-scan or a duplicate fallback for the same code stays in-process.
/// </summary>
public sealed class GiantBombGameUpcClient : IGiantBombGameUpcClient
{
    public const string ProviderName = "giantbomb";
    public const string HttpClientName = "giantbomb";

    private readonly HttpClient _http;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<GiantBombGameUpcClient> _log;

    public GiantBombGameUpcClient(
        HttpClient http,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<GiantBombGameUpcClient> log)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _log = log;
    }

    public string Name => ProviderName;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.GiantBomb.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.GiantBomb.UserAgent);

    public async Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        var trimmed = (barcode ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;

        var cacheKey = "barcode:" + trimmed;
        var cached = await _cache.GetAsync<UpcLookupResult>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        GiantBombReleasesResponse? body;
        try
        {
            // field_list keeps the response small -- GiantBomb defaults
            // include a long list of metadata we don't use. limit=10 in
            // case the same UPC matches multiple regional releases; we
            // pick the first one with a usable name.
            var url = $"releases/?api_key={Uri.EscapeDataString(_options.GiantBomb.ApiKey!)}"
                    + $"&format=json&field_list=id,name,game"
                    + $"&filter=upc:{Uri.EscapeDataString(trimmed)}"
                    + "&limit=10";
            body = await _http.GetFromJsonAsync<GiantBombReleasesResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GiantBomb UPC lookup failed for {Barcode}", trimmed);
            return null;
        }

        // status_code 1 = OK in GiantBomb's API.
        if (body is null || body.StatusCode != 1) return null;

        var first = body.Results?.FirstOrDefault(r =>
            !string.IsNullOrWhiteSpace(r.Game?.Name) || !string.IsNullOrWhiteSpace(r.Name));
        if (first is null) return null;

        // Prefer the canonical game name; fall back to the release's
        // own name (which may be a region-tagged title like
        // "Halo 3 (PAL)").
        var title = !string.IsNullOrWhiteSpace(first.Game?.Name) ? first.Game!.Name! : first.Name!;
        var mapped = new UpcLookupResult(Title: title, Brand: null, Manufacturer: null);

        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }
}
