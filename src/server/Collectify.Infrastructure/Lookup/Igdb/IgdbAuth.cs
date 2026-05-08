using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Default <see cref="IIgdbAuth"/>. Caches the Twitch token in-process and
/// refreshes ahead of expiry. Registered as a singleton so the cache spans
/// every IgdbGameProvider instance (the typed HttpClient is transient).
/// Thread-safety: a SemaphoreSlim guards the refresh path so concurrent
/// callers don't hammer Twitch.
/// </summary>
public sealed class IgdbAuth : IIgdbAuth, IDisposable
{
    public const string HttpClientName = "twitch-oauth";

    private readonly HttpClient _http;
    private readonly MetadataLookupOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<IgdbAuth> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public IgdbAuth(
        HttpClient http,
        IOptions<MetadataLookupOptions> options,
        TimeProvider clock,
        ILogger<IgdbAuth> log)
    {
        _http = http;
        _options = options.Value;
        _clock = clock;
        _log = log;
    }

    public string? ClientId => _options.Igdb.TwitchClientId;

    public async Task<string?> GetTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var clientId = _options.Igdb.TwitchClientId;
        var clientSecret = _options.Igdb.TwitchClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return null;

        if (!forceRefresh && _token is not null && _clock.GetUtcNow() < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            // Re-check inside the lock; another caller may have just refreshed.
            if (!forceRefresh && _token is not null && _clock.GetUtcNow() < _expiresAt)
                return _token;

            var url = "https://id.twitch.tv/oauth2/token"
                + $"?client_id={Uri.EscapeDataString(clientId!)}"
                + $"&client_secret={Uri.EscapeDataString(clientSecret!)}"
                + "&grant_type=client_credentials";

            using var resp = await _http.PostAsync(url, content: null, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Twitch token fetch failed with {Status}", resp.StatusCode);
                return null;
            }

            var body = await resp.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: ct);
            if (body is null || string.IsNullOrEmpty(body.AccessToken)) return null;

            _token = body.AccessToken;
            // Refresh ~1h before the stated expiry so a token isn't handed
            // out moments before it dies. Floor at 60s for paranoid clamps
            // against absurdly short ExpiresIn values.
            var lifetime = Math.Max(60, body.ExpiresIn - 3600);
            _expiresAt = _clock.GetUtcNow().AddSeconds(lifetime);
            return _token;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Twitch token fetch threw");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
