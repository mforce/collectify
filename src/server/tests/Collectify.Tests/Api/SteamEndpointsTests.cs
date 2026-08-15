using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Store;
using Collectify.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class SteamEndpointsTests
{
    private record SteamConnectDto(bool Configured, string? RedirectUrl);
    private record SteamConnectionDto(bool Connected, string? SteamId, string? PersonaName);
    private record SteamOwnedTitleDto(string ExternalGameId, string Title, long PlaytimeMinutes, string? IconUrl, string State);
    private record SteamPreviewDto(string Status, SteamOwnedTitleDto[] Titles, bool Truncated);
    private record SteamImportResultDto(int Imported, int AlreadyImported, SteamImportItemDto[] Items);
    private record SteamImportItemDto(string ExternalGameId, bool Imported, bool AlreadyImported);

    // -------- Not-configured ----------

    [Fact]
    public async Task Connect_Unconfigured_ReturnsConfiguredFalse()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient { IsConfigured = false },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier { IsConfigured = false },
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var dto = await alice.Client.GetJsonAsync<SteamConnectDto>("/api/accounts/steam/connect");

        Assert.NotNull(dto);
        Assert.False(dto.Configured);
        Assert.Null(dto.RedirectUrl);
    }

    [Fact]
    public async Task Connection_NotLinked_ReportsDisconnected()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var dto = await alice.Client.GetJsonAsync<SteamConnectionDto>("/api/accounts/steam/");

        Assert.False(dto!.Connected);
    }

    // -------- Connect happy path ----------

    [Fact]
    public async Task Connect_WhenConfigured_ReturnsSteamRedirectAndSetsStateCookie()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.GetAsync("/api/accounts/steam/connect");
        var dto = await response.ReadJsonAsync<SteamConnectDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(dto!.Configured);
        Assert.Contains("https://steamcommunity.com/openid/login?", dto.RedirectUrl);
        Assert.Contains("openid.return_to=", dto.RedirectUrl);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                    cookies.Any(c => c.StartsWith("collectify.steam.state=")));
    }

    // -------- Full connect callback flow ----------

    [Fact]
    public async Task Callback_WithValidAssertionAndCookie_ConnectsAccount()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient { PersonaName = "AlicePersona" },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier { VerifiedSteamId = "76561198000000000" },
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        // 1. Begin connect, capture cookie + the return_to (which carries state).
        var connectResponse = await alice.Client.GetAsync("/api/accounts/steam/connect");
        var connect = await connectResponse.ReadJsonAsync<SteamConnectDto>();
        var cookie = connectResponse.Headers.GetValues("Set-Cookie")
            .Select(s => s.Split(';')[0]).FirstOrDefault(c => c.StartsWith("collectify.steam.state="));
        Assert.NotNull(cookie);

        var redirectUri = new Uri(connect!.RedirectUrl!);
        var q = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        var returnTo = q["openid.return_to"]!;

        // 2. Simulate Steam posting us back with a full assertion.
        var (status, location) = await CallbackClientAsync(factory, alice, returnTo, cookie);

        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.Contains("/import/steam?steam=connected", location!.ToString());

        // 3. Connection persisted with verified steam id + persona.
        var connection = await factory.WithDbAsync(db =>
            db.GameStoreConnections.AsNoTracking().FirstOrDefaultAsync(c => c.OwnerId == alice.Id));
        Assert.NotNull(connection);
        Assert.Equal("76561198000000000", connection!.ExternalAccountId);
        Assert.Equal("AlicePersona", connection.ExternalDisplayName);
    }

    [Fact]
    public async Task Callback_WithRejectedAssertion_DoesNotConnect()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier { VerifiedSteamId = null },
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var connectResponse = await alice.Client.GetAsync("/api/accounts/steam/connect");
        var connect = await connectResponse.ReadJsonAsync<SteamConnectDto>();
        var cookie = connectResponse.Headers.GetValues("Set-Cookie")
            .Select(s => s.Split(';')[0]).FirstOrDefault(c => c.StartsWith("collectify.steam.state="));
        var redirectUri = new Uri(connect!.RedirectUrl!);
        var q = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        var returnTo = q["openid.return_to"]!;

        var (_, location) = await CallbackClientAsync(factory, alice, returnTo, cookie);

        Assert.Contains("/import/steam?steam=error", location!.ToString());
        var connection = await factory.WithDbAsync(db =>
            db.GameStoreConnections.AsNoTracking().AnyAsync(c => c.OwnerId == alice.Id));
        Assert.False(connection);
    }

    // -------- Import ----------

    [Fact]
    public async Task Import_CreatesGameAndLedgerRow()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames =
                [
                    new SteamOwnedGame { AppId = 1, Name = "Hades", PlaytimeForever = 3600 },
                    new SteamOwnedGame { AppId = 2, Name = "Hollow Knight" },
                ],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        var body = await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1", "2" } })).ReadJsonAsync<SteamImportResultDto>();

        Assert.Equal(2, body!.Imported);
        Assert.Equal(0, body.AlreadyImported);

        var game = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        Assert.Equal(GamePlatform.Pc, game.Platform);
        Assert.Equal(DigitalStore.Steam, game.DigitalStore);
        Assert.True(game.IsDigital);
        Assert.Equal(60, game.HoursPlayed); // 3600s/60 = 60h

        var ledger = await factory.WithDbAsync(db =>
            db.GameStoreOwnedTitles.AsNoTracking().CountAsync(t => t.OwnerId == alice.Id && t.Store == DigitalStore.Steam));
        Assert.Equal(2, ledger);
    }

    [Fact]
    public async Task Import_IsIdempotent()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });
        var second = await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        Assert.Equal(0, second!.Imported);
        Assert.Equal(1, second.AlreadyImported);

        var gameCount = await factory.WithDbAsync(db => db.Games.AsNoTracking().CountAsync(g => g.OwnerId == alice.Id));
        Assert.Equal(1, gameCount);
    }

    [Fact]
    public async Task Import_CannotImportUnownedApp()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        // AppId 999 is not in the trusted owned list, so it must not import.
        var body = await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "999", "1" } })).ReadJsonAsync<SteamImportResultDto>();

        Assert.Equal(1, body!.Imported);
        Assert.False(body.Items.Single(i => i.ExternalGameId == "999").Imported);
    }

    // -------- Games preview + disconnect ----------

    [Fact]
    public async Task Games_MarksImportedTitles()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }, new SteamOwnedGame { AppId = 2, Name = "Celeste" }],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);
        await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });

        var preview = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");

        var hades = preview!.Titles.Single(t => t.ExternalGameId == "1");
        var celeste = preview.Titles.Single(t => t.ExternalGameId == "2");
        Assert.Equal("ok", preview.Status);
        Assert.Equal("imported", hades.State);
        Assert.Equal("importable", celeste.State);
    }

    [Fact]
    public async Task Games_NotLinked_ReturnsEmpty()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var preview = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");

        // Not connected: status is "notconnected", empty titles (client shows
        // the connect prompt, not a blank list).
        Assert.Equal("notconnected", preview!.Status);
        Assert.Empty(preview.Titles);
    }

    [Fact]
    public async Task Games_SteamUnavailable_ReportsUnavailableStatus()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient { FetchStatus = SteamFetchStatus.Unavailable },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        var preview = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");

        // Provider failure must be surfaced as "unavailable" (client shows the
        // qualified private/offline message), NOT a blank "you own nothing".
        Assert.Equal("unavailable", preview!.Status);
        Assert.Empty(preview.Titles);
    }

    [Fact]
    public async Task Disconnect_RemovesConnectionButKeepsGames()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);
        await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });

        var response = await alice.Client.DeleteAsync("/api/accounts/steam/");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var connection = await factory.WithDbAsync(db =>
            db.GameStoreConnections.AsNoTracking().AnyAsync(c => c.OwnerId == alice.Id));
        Assert.False(connection);

        // Imported game survives the disconnect.
        var game = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().AnyAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        Assert.True(game);
    }

    [Fact]
    public async Task Delete_ImportedGame_NullsLedgerAndSucceeds()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);
        await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });

        var gameId = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().Where(g => g.OwnerId == alice.Id && g.Title == "Hades")
                .Select(g => (int?)g.Id).FirstAsync());

        // Deleting the imported game must NOT trip the composite FK (Restrict)
        // and must leave an importable ledger row behind.
        var delete = await alice.Client.DeleteAsync($"/api/games/{gameId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var ledgerStillThere = await factory.WithDbAsync(db =>
            db.GameStoreOwnedTitles.AsNoTracking()
                .AnyAsync(t => t.OwnerId == alice.Id && t.Store == DigitalStore.Steam && t.ExternalGameId == "1"));
        Assert.True(ledgerStillThere, "ledger row should persist after game delete");

        var gameGone = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId));
        Assert.False(gameGone);

        // A second re-import is idempotent and succeeds (relinks the ledger row).
        var reimport = await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });
        Assert.Equal(HttpStatusCode.OK, reimport.StatusCode);
        var restored = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().AnyAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        Assert.True(restored);
    }

    // -------- Helpers ----------

    private static async Task LinkSteamAsync(CollectifyApiFactory factory, TestExtensions.TestUser user)
    {
        var connectResponse = await user.Client.GetAsync("/api/accounts/steam/connect");
        var connect = await connectResponse.ReadJsonAsync<SteamConnectDto>();
        var cookie = connectResponse.Headers.GetValues("Set-Cookie")
            .Select(s => s.Split(';')[0]).FirstOrDefault(c => c.StartsWith("collectify.steam.state="));
        var redirectUri = new Uri(connect!.RedirectUrl!);
        var q = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        var returnTo = q["openid.return_to"]!;

        var client = await CallbackClientAsync(factory, user, returnTo, cookie);
        if (client.Location?.ToString().Contains("error", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("Callback redirected to error: " + client.Location);
    }

    /// <summary>
    /// Sends the OpenID callback with the steam-state cookie on a
    /// no-auto-redirect client so we observe the callback's 302 instead of it
    /// being followed to /import/steam (which 404s in the test host because
    /// the SPA is served by Kestrel only in production).
    /// </summary>
    private static async Task<(System.Net.HttpStatusCode Status, Uri? Location)> CallbackClientAsync(
        CollectifyApiFactory factory, TestExtensions.TestUser user, string returnTo, string? cookie)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildCallbackUrl(returnTo, "76561198000000000"));
        if (cookie is not null) req.Headers.TryAddWithoutValidation("Cookie", cookie);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var cb = await client.SendAsync(req);
        return (cb.StatusCode, cb.Headers.Location);
    }

    private static string BuildCallbackUrl(string returnTo, string steamId)
    {
        var enc = (string s) => Uri.EscapeDataString(s);
        var claimed = $"http://steamcommunity.com/openid/id/{steamId}";
        var identity = $"http://steamcommunity.com/openid/id/{steamId}";
        return "/api/accounts/steam/callback"
            + $"?openid.ns={enc("http://specs.openid.net/auth/2.0")}"
            + "&openid.mode=id_res"
            + $"&openid.op_endpoint={enc("http://steamcommunity.com/openid/login")}"
            + $"&openid.return_to={enc(returnTo)}"
            + $"&openid.claimed_id={enc(claimed)}"
            + $"&openid.identity={enc(identity)}"
            + "&openid.assoc_handle=assoc"
            + "&openid.response_nonce=nonce"
            + "&openid.signed=op_endpoint,return_to,response_nonce,assoc_handle,claimed_id,identity"
            + "&openid.sig=sig";
    }
}
