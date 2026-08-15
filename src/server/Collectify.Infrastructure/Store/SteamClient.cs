using System.Net.Http.Json;
using Collectify.Infrastructure.Lookup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Store;

/// <summary>
/// Typed Steam Web API client for the owned-games import feature. Talks to
/// Steam's official API (IPlayerService/GetOwnedGames, ISteamUser/
/// GetPlayerSummaries). Fail-soft: if <see cref="SteamOptions.SteamSubOptions.IsConfigured"/>
/// is false (no API key), every call returns an empty result and the UI shows
/// a "set the Steam API key" hint.
///
/// Owned-games fetching is cached through <see cref="ILookupCache"/> keyed on
/// the SteamID64 (never OwnerId — the cache table is global), with the short
/// TTL from SteamOptions because a user's private library must not sit in the
/// shared cache for days.
/// </summary>
public sealed class SteamClient : ISteamClient
{
    public const string ProviderName = "steam-owned";
    public const string HttpClientName = "steam";

    private readonly HttpClient _http;
    private readonly SteamOptions.SteamSubOptions _options;
    private readonly ILookupCache _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<SteamClient> _log;

    public SteamClient(
        HttpClient http,
        IOptions<SteamOptions> options,
        ILookupCache cache,
        TimeProvider clock,
        ILogger<SteamClient> log)
    {
        _http = http;
        _options = options.Value.Steam;
        _cache = cache;
        _clock = clock;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>
    /// Returns the app ids + metadata the account owns. Cached per SteamID64
    /// for <see cref="SteamOptions.SteamSubOptions.CacheTtl"/>. Distinguishes a
    /// genuine empty/private library (Ok with no games) from a provider
    /// failure (Unavailable) so the caller can show the right message.
    /// </summary>
    public async Task<SteamGamesResult> GetOwnedGamesAsync(string steamId, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(steamId))
            return new SteamGamesResult(SteamFetchStatus.Unavailable, []);

        var cacheKey = "owned:" + steamId;
        var cached = await _cache.GetAsync<SteamGamesResult>(ProviderName, cacheKey, _options.CacheTtl, ct);
        if (cached is not null) return cached;

        var result = await FetchOwnedGamesAsync(steamId, ct);
        // Only cache successful responses (Ok, even if empty — a private/empty
        // library is still a valid state and shouldn't be re-fetched every 5s).
        if (result.Status == SteamFetchStatus.Ok)
            await _cache.SetAsync(ProviderName, cacheKey, result, ct);

        return result;
    }

    private async Task<SteamGamesResult> FetchOwnedGamesAsync(string steamId, CancellationToken ct)
    {
        var url = "IPlayerService/GetOwnedGames/v1/"
            + $"?key={Uri.EscapeDataString(_options.ApiKey!)}"
            + $"&steamid={Uri.EscapeDataString(steamId)}"
            + "&include_appinfo=true&include_played_free_games=true&format=json";

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Static key: a 401 has no refresh path, so fail soft and log
                // the status WITHOUT the key (the URL carries the key).
                _log.LogWarning("Steam GetOwnedGames returned {Status}", resp.StatusCode);
                return new SteamGamesResult(SteamFetchStatus.Unavailable, []);
            }
            var body = await resp.Content.ReadFromJsonAsync<SteamOwnedGamesResponse>(cancellationToken: ct);
            return new SteamGamesResult(SteamFetchStatus.Ok, body?.Response?.Games ?? []);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Steam GetOwnedGames call failed");
            return new SteamGamesResult(SteamFetchStatus.Unavailable, []);
        }
    }

    /// <summary>Best-effort persona name lookup. Returns null on any failure.</summary>
    public async Task<string?> GetPersonaNameAsync(string steamId, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(steamId)) return null;

        var url = "ISteamUser/GetPlayerSummaries/v2/"
            + $"?key={Uri.EscapeDataString(_options.ApiKey!)}"
            + $"&steamids={Uri.EscapeDataString(steamId)}&format=json";

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadFromJsonAsync<SteamPlayerSummariesResponse>(cancellationToken: ct);
            return body?.Body?.Players?.FirstOrDefault(p =>
                string.Equals(p.SteamId, steamId, StringComparison.Ordinal))?.PersonaName;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Steam GetPlayerSummaries failed");
            return null;
        }
    }
}
