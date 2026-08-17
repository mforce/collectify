using System.Net;
using System.Net.Http;
using System.Text;
using Collectify.Infrastructure.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Direct tests for SteamOpenIdVerifier.VerifyAsync with a stubbed
/// HttpMessageHandler for the check_authentication round-trip, so the real
/// verifier (not a double) is exercised — this is what catches regressions in
/// the connection security checks.
/// </summary>
public class SteamOpenIdVerifierTests
{
    private const string Endpoint = "https://steam.example/openid/login";
    private const string SteamNs = "http://specs.openid.net/auth/2.0";
    private const string Known = "76561198000000000";

    private static SteamOpenIdVerifier Build(bool isValid, Func<string, bool>? postCheck = null)
    {
        var handler = new StubHandler(isValid, postCheck);
        var http = new HttpClient(handler) { BaseAddress = new Uri(Endpoint) };
        var options = Options.Create(new SteamOptions
        {
            Steam = new SteamOptions.SteamSubOptions
            {
                ApiKey = "k",
                OpenIdBaseUrl = Endpoint,
            },
        });
        return new SteamOpenIdVerifier(http, options, NullLogger<SteamOpenIdVerifier>.Instance);
    }
    private static Dictionary<string, string> ValidParams(string? claimed = null, string? returnTo = null)
    {
        var id = claimed ?? $"https://steamcommunity.com/openid/id/{Known}";
        return new Dictionary<string, string>
        {
            ["openid.mode"] = "id_res",
            ["openid.ns"] = SteamNs,
            ["openid.op_endpoint"] = Endpoint,
            ["openid.return_to"] = returnTo ?? "https://app.example/api/accounts/steam/callback?state=abc",
            ["openid.identity"] = id,
            ["openid.claimed_id"] = id,
            ["openid.signed"] = "op_endpoint,return_to,response_nonce,assoc_handle,claimed_id,identity",
            ["openid.response_nonce"] = "2024-01-01T00:00:00Zabc",
            ["openid.assoc_handle"] = "h",
        };
    }

    [Fact]
    public async Task ValidAssertion_ReturnsSteamId()
    {
        var v = Build(isValid: true);
        var result = await v.VerifyAsync(ValidParams(), "https://app.example/api/accounts/steam/callback?state=abc");
        Assert.Equal(Known, result);
    }

    [Fact]
    public async Task ValidAssertion_SendsCheckAuthenticationMode()
    {
        // The round-trip must be a check_authentication POST, not a replay of
        // the id_res. Make the stub only report is_valid:true when the body
        // contains mode=check_authentication.
        var verifier = Build(isValid: false, postCheck: body => body.Contains("mode=check_authentication"));
        var result = await verifier.VerifyAsync(ValidParams(), "https://app.example/api/accounts/steam/callback?state=abc");
        Assert.Equal(Known, result);
    }

    [Fact]
    public async Task WrongMode_ReturnsNull()
    {
        var p = ValidParams();
        p["openid.mode"] = "cancel";
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Theory]
    [InlineData("nottheendpoint")]
    [InlineData("https://evil.example/openid/login")]
    public async Task BadEndpoint_ReturnsNull(string endpoint)
    {
        var p = ValidParams();
        p["openid.op_endpoint"] = endpoint;
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task ReturnToMismatch_ReturnsNull()
    {
        var p = ValidParams(returnTo: "https://app.example/api/accounts/steam/callback?state=other");
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task ClaimedIdDoesNotEqualIdentity_ReturnsNull()
    {
        var p = ValidParams(claimed: $"https://steamcommunity.com/openid/id/{Known}");
        p["openid.identity"] = $"https://steamcommunity.com/openid/id/99999999999999999";
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task TrailingSlashClaimedId_ReturnsNullInsteadOfThrowing()
    {
        // Regression for the IndexOutOfRange 500: /openid/id/ with no id.
        var p = ValidParams(claimed: "https://steamcommunity.com/openid/id/");
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task MissingSignedCoverage_ReturnsNull()
    {
        var p = ValidParams();
        p["openid.signed"] = "op_endpoint,return_to";
        Assert.Null(await Build(true).VerifyAsync(p, "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task CheckAuthReturnsNotValid_ReturnsNull()
    {
        Assert.Null(await Build(isValid: false).VerifyAsync(ValidParams(), "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    [Fact]
    public async Task CheckAuthNetworkFailure_ReturnsNull()
    {
        Assert.Null(await Build(isValid: true, postCheck: _ => throw new HttpRequestException("boom"))
            .VerifyAsync(ValidParams(), "https://app.example/api/accounts/steam/callback?state=abc"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly bool _valid;
        private readonly Func<string, bool>? _postCheck;
        public StubHandler(bool valid, Func<string, bool>? postCheck) { _valid = valid; _postCheck = postCheck; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            // _postCheck either decides validity from the body or throws (network
            // failure simulation — the verifier swallows the exception -> null).
            var onlyValidWhen = _postCheck;
            var isValid = onlyValidWhen is null ? _valid : onlyValidWhen(body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(isValid ? "ns:" + SteamNs + "\nis_valid:true\n" : "is_valid:false\n", Encoding.UTF8),
            };
        }
    }
}
