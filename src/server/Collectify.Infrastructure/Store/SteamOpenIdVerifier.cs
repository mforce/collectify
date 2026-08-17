using System.Net.Http;
using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Store;

/// <summary>
/// Steam OpenID 2.0 assertion verifier. The callback endpoint hands us the raw
/// <c>openid.*</c> query parameters; we validate every field and then perform
/// the authoritative <c>check_authentication</c> round-trip back to the
/// hard-coded Steam provider (never trusting the response's op_endpoint for
/// where we verify).
///
/// Replay protection is delegated to the one-time <c>state</c> request (the
/// consuming call passes the state through the return_to we require to match),
/// so this verifier concerns itself only with authentication of the assertion.
/// </summary>
public sealed class SteamOpenIdVerifier : ISteamOpenIdVerifier
{
    public const string HttpClientName = "steam-openid";

    private const string SteamNamespace = "http://specs.openid.net/auth/2.0";

    private readonly HttpClient _http;
    private readonly SteamOptions.SteamSubOptions _options;
    private readonly ILogger<SteamOpenIdVerifier> _log;

    public SteamOpenIdVerifier(
        HttpClient http,
        IOptions<SteamOptions> options,
        ILogger<SteamOpenIdVerifier> log)
    {
        _http = http;
        _options = options.Value.Steam;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>
    /// Validates the assertion and returns the verified SteamID64, or null if
    /// any check fails. Throws only on unexpected infrastructure errors; a
    /// forged/invalid assertion returns null.
    /// </summary>
    public async Task<string?> VerifyAsync(
        IReadOnlyDictionary<string, string> openIdParams,
        string expectedReturnTo,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var get = (string k) => openIdParams.TryGetValue(k, out var v) ? v : null;

        // 1. mode + namespace
        if (get("openid.mode") != "id_res") { LogReject("mode"); return null; }
        if (get("openid.ns") != SteamNamespace) { LogReject("namespace"); return null; }

        // 2. exact known provider endpoint
        if (get("openid.op_endpoint") != _options.OpenIdBaseUrl) { LogReject("op_endpoint"); return null; }

        // 3. return_to must byte-match the expected callback (incl. state)
        var returnTo = get("openid.return_to");
        if (returnTo is null || !string.Equals(returnTo, expectedReturnTo, StringComparison.Ordinal))
        {
            LogReject("return_to");
            return null;
        }

        // 6. identity == claimed_id (OpenID 2.0)
        var identity = get("openid.identity");
        var claimed = get("openid.claimed_id");
        if (identity is null || claimed is null || !string.Equals(identity, claimed, StringComparison.Ordinal))
        {
            LogReject("identity!=claimed_id");
            return null;
        }

        // 7. strict SteamID64 from the claimed-id URI
        var steamId = SteamId64.FromClaimedId(claimed);
        if (steamId is null) { LogReject("claimed_id"); return null; }

        // 4/6? authoritative round-trip: echo every openid.* param back,
        // only mode changes to check_authentication.
        var valid = await CheckAuthenticationAsync(openIdParams, ct);
        if (!valid) { LogReject("check_authentication"); return null; }

        // 8. signed must actually cover the fields we trust
        var signed = get("openid.signed") ?? string.Empty;
        var signedSet = signed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .ToHashSet(StringComparer.Ordinal);

        foreach (var field in new[] { "op_endpoint", "return_to", "response_nonce", "assoc_handle", "claimed_id", "identity" })
        {
            if (!signedSet.Contains(field)) { LogReject("signed:" + field); return null; }
        }

        return steamId;
    }

    private async Task<bool> CheckAuthenticationAsync(IReadOnlyDictionary<string, string> openIdParams, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>();
        foreach (var (k, v) in openIdParams.Where(p => p.Key.StartsWith("openid.", StringComparison.Ordinal)))
        {
            if (k == "openid.mode") form.Add(new(k, "check_authentication"));
            else form.Add(new(k, v));
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var resp = await _http.PostAsync(_options.OpenIdBaseUrl, content, ct);
            if (!resp.IsSuccessStatusCode) return false;
            var text = await resp.Content.ReadAsStringAsync(ct);
            // Steam returns an application/x-www-form-urlencoded-ish body like
            // is_valid:true\n... — parse the is_valid key loosely.
            var lookup = ParseBody(text);
            return lookup.TryGetValue("is_valid", out var v) && v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Steam OpenID check_authentication failed");
            return false;
        }
    }

    private static Dictionary<string, string> ParseBody(string text)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf(':');
            if (idx > 0) d[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return d;
    }

    private void LogReject(string reason)
    {
        if (_log.IsEnabled(LogLevel.Information))
            _log.LogInformation("Steam OpenID assertion rejected: {Reason}", reason);
    }
}
