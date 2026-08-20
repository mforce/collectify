using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.MusicBrainz;

/// <summary>
/// <see cref="IMetadataProvider{T}"/> (music) backed by musicbrainz.org's web
/// service. MB has
/// no API key; instead it requires a contact-bearing User-Agent on every
/// request -- the User-Agent is the rate-limit identity. If the option
/// isn't configured, every entry point short-circuits and the lookup
/// endpoint reports configured=false.
///
/// Cover art comes from the Cover Art Archive at
/// <c>coverartarchive.org/release/{mbid}/front-500</c>. Not every release
/// has art there; the URL is returned regardless and the browser falls
/// back to the placeholder when the image 404s -- no extra round trip.
/// </summary>
public sealed class MusicBrainzMusicProvider : IMetadataProvider<MusicLookupResult>
{
    public const string ProviderName = "musicbrainz";
    public const string HttpClientName = "musicbrainz";
    private const string CoverArtBase = "https://coverartarchive.org/release";

    private readonly HttpClient _http;
    private readonly ILookupCache _cache;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<MusicBrainzMusicProvider> _log;

    public MusicBrainzMusicProvider(
        HttpClient http,
        ILookupCache cache,
        IOptions<MetadataLookupOptions> options,
        ILogger<MusicBrainzMusicProvider> log)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _log = log;
    }

    public string Name => ProviderName;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.MusicBrainz.UserAgent);

    public async Task<IReadOnlyList<MusicLookupResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];

        var cacheKey = "search:" + trimmed.ToLowerInvariant();
        var cached = await _cache.GetAsync<List<MusicLookupResult>>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        MbReleaseSearchResponse? body;
        try
        {
            // limit=10 keeps the dropdown manageable; fmt=json picks the
            // structured response over the default XML.
            var url = $"release?query={Uri.EscapeDataString(trimmed)}&fmt=json&limit=10";
            body = await _http.GetFromJsonAsync<MbReleaseSearchResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MusicBrainz search failed for {Query}", trimmed);
            return [];
        }

        var mapped = (body?.Releases ?? []).Select(Map).ToList();
        await _cache.SetAsync(ProviderName, cacheKey, mapped, _options.CacheTtl, ct);
        return mapped;
    }

    public async Task<MusicLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (string.IsNullOrWhiteSpace(providerKey)) return null;

        var cacheKey = "id:" + providerKey;
        var cached = await _cache.GetAsync<MusicLookupResult>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        MbRelease? release;
        try
        {
            // inc=artist-credits+labels gives us artist + label inline so we
            // don't need a second call per result.
            var url = $"release/{Uri.EscapeDataString(providerKey)}?inc=artist-credits+labels&fmt=json";
            release = await _http.GetFromJsonAsync<MbRelease>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MusicBrainz lookup-by-id failed for {Mbid}", providerKey);
            return null;
        }

        if (release is null) return null;
        var mapped = Map(release);
        await _cache.SetAsync(ProviderName, cacheKey, mapped, _options.CacheTtl, ct);
        return mapped;
    }

    public async Task<IReadOnlyList<MusicLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];
        var trimmed = (barcode ?? string.Empty).Trim();
        if (trimmed.Length == 0) return [];

        // MB indexes barcodes natively via the "barcode:" Lucene field on
        // the /release search index, so we can skip UPCitemdb entirely.
        // Cache key is namespaced "barcode:" so it can't collide with
        // free-text searches whose content happens to be a 12-digit number.
        var cacheKey = "barcode:" + trimmed;
        var cached = await _cache.GetAsync<List<MusicLookupResult>>(ProviderName, cacheKey, ct);
        if (cached is not null) return cached;

        MbReleaseSearchResponse? body;
        try
        {
            var url = $"release?query=barcode:{Uri.EscapeDataString(trimmed)}&fmt=json&limit=10";
            body = await _http.GetFromJsonAsync<MbReleaseSearchResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MusicBrainz barcode search failed for {Barcode}", trimmed);
            return [];
        }

        var mapped = (body?.Releases ?? []).Select(Map).ToList();
        await _cache.SetAsync(ProviderName, cacheKey, mapped, _options.CacheTtl, ct);
        return mapped;
    }

    private MusicLookupResult Map(MbRelease r) => new(
        Provider: ProviderName,
        ProviderKey: r.Id,
        Title: r.Title ?? string.Empty,
        ArtistName: JoinArtistCredits(r.ArtistCredit),
        Year: ParseYear(r.Date),
        Label: r.LabelInfo?.FirstOrDefault()?.Label?.Name,
        Description: null,
        ImageUrl: $"{CoverArtBase}/{r.Id}/front-500",
        Genres: null);

    private static string JoinArtistCredits(IReadOnlyList<MbArtistCredit>? credits)
    {
        if (credits is null || credits.Count == 0) return string.Empty;
        // MB encodes "X feat. Y" / "X & Y" by emitting Name + JoinPhrase
        // pairs. Concatenating in order gives the canonical display string.
        return string.Concat(credits.Select(c => (c.Name ?? string.Empty) + (c.JoinPhrase ?? string.Empty))).Trim();
    }

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4) return null;
        return int.TryParse(date.AsSpan(0, 4), out var year) ? year : null;
    }
}
