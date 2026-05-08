using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Upc;

/// <summary>
/// IUpcLookupClient backed by upcitemdb.com's free trial endpoint
/// (/prod/trial/lookup). No API key, but rate-limited at ~100 lookups/day,
/// so every call is cached in LookupCache under the "upcitemdb" provider
/// name with a "barcode:" namespace -- a re-scan of the same code is then
/// free and a cold scan stays well under the daily quota.
///
/// On any non-200 / parse error / "no items" we return null so the caller
/// can decide whether to fall back to a per-type title search.
/// </summary>
public sealed class UpcItemDbClient : IUpcLookupClient
{
    public const string ProviderName = "upcitemdb";
    public const string HttpClientName = "upcitemdb";

    private readonly HttpClient _http;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<UpcItemDbClient> _log;

    public UpcItemDbClient(
        HttpClient http,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<UpcItemDbClient> log)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _log = log;
    }

    public string Name => ProviderName;

    public async Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        var trimmed = (barcode ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;

        var cacheKey = "barcode:" + trimmed;
        var cached = await _cache.GetAsync<UpcLookupResult>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        UpcItemDbResponse? body;
        try
        {
            var url = $"prod/trial/lookup?upc={Uri.EscapeDataString(trimmed)}";
            body = await _http.GetFromJsonAsync<UpcItemDbResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "UPCitemdb lookup failed for {Barcode}", trimmed);
            return null;
        }

        var first = body?.Items?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Title));
        if (first is null) return null;

        var mapped = new UpcLookupResult(
            Title: first.Title!,
            Brand: string.IsNullOrWhiteSpace(first.Brand) ? null : first.Brand,
            Manufacturer: string.IsNullOrWhiteSpace(first.Manufacturer) ? null : first.Manufacturer);

        await _cache.SetAsync(ProviderName, cacheKey, mapped, ct);
        return mapped;
    }
}
