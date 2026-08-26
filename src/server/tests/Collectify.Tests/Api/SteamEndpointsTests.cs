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
    private record SteamOwnedTitleDto(string ExternalGameId, string Title, long PlaytimeMinutes, string? IconUrl, string? LogoUrl, string State);
    private record SteamPreviewDto(string Status, SteamOwnedTitleDto[] Titles, bool Truncated, int Total);
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

        var connectResponse = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
        var dto = await connectResponse.ReadJsonAsync<SteamConnectDto>();

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

        var response = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
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
        var connectResponse = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
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

        var connectResponse = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
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
        Assert.Equal(DigitalStore.Steam, game.DigitalStores);
        Assert.NotEqual(DigitalStore.None, game.DigitalStores); // derived "digital"
        Assert.Equal(60, game.HoursPlayed); // 3600s/60 = 60h

        var ledger = await factory.WithDbAsync(db =>
            db.GameStoreOwnedTitles.AsNoTracking().CountAsync(t => t.OwnerId == alice.Id && t.Store == DigitalStore.Steam));
        Assert.Equal(2, ledger);
    }

    [Fact]
    public async Task Import_CapturesSteamCoverAndLastPlayedOnGame()
    {
        var lastPlayedUnix = 1735689600; // 2025-01-01T00:00:00Z
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames =
                [
                    new SteamOwnedGame
                    {
                        AppId = 1,
                        Name = "Hades",
                        ImgIconUrl = "iconhash",
                        ImgLogoUrl = "logohash",
                        RtimeLastPlayed = lastPlayedUnix,
                    },
                    new SteamOwnedGame
                    {
                        AppId = 2,
                        Name = "Hollow Knight",
                        // No cover, never played -> ImagePath/LastPlayedOn stay null.
                    },
                ],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1", "2" } })).ReadJsonAsync<SteamImportResultDto>();

        var withCover = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        // Library cover (600x900 portrait) preferred over logo/icon; localized
        // through the (fake) cover store into /covers/<hash> — never the raw
        // remote URL.
        Assert.StartsWith("/covers/", withCover.ImagePath);
        Assert.DoesNotContain("steampowered.com", withCover.ImagePath);
        Assert.Equal(new DateOnly(2025, 1, 1), withCover.LastPlayedOn);

        // A real owned Steam app always has library art, so Hollow Knight (appid
        // 2) also gets a localized cover — but it was never played, so
        // LastPlayedOn stays null.
        var noPlay = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id && g.Title == "Hollow Knight"));
        Assert.StartsWith("/covers/", noPlay.ImagePath);
        Assert.Null(noPlay.LastPlayedOn);
    }

    [Fact]
    public async Task Import_CapturesRichMetadataFromStoreBrowse()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades", PlaytimeForever = 3600 }],
                StoreItems = new Dictionary<uint, SteamStoreBrowseItem>
                {
                    [1] = new()
                    {
                        AppId = 1,
                        Name = "Hades",
                        BasicInfo = new SteamStoreBasicInfo
                        {
                            ShortDescription = "Defy the god of the dead.",
                            Developers = [new SteamStoreOwner { Name = "Supergiant Games" }],
                            Publishers = [new SteamStoreOwner { Name = "Supergiant Games" }],
                        },
                        Release = new SteamStoreRelease { SteamReleaseDate = 1609459200 }, // 2021-01-01
                        // Hashed asset dir (like newer apps) — the import MUST use
                        // this to build the cover URL, not a hardcoded appid URL.
                        Assets = new SteamStoreAssets
                        {
                            AssetUrlFormat = "steam/apps/1/${FILENAME}?t=1",
                            LibraryCapsule2x = "aabbcc/library_600x900_2x.jpg",
                        },
                    },
                },
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        var game = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id));
        Assert.Equal("Hades", game.Title);
        Assert.Equal("Supergiant Games", game.Developer);
        Assert.Equal("Supergiant Games", game.Publisher);
        Assert.Equal(2021, game.Year);
        Assert.Equal(new DateOnly(2021, 1, 1), game.ReleaseDate);
        Assert.Equal("Defy the god of the dead.", game.Description);
    }

    [Fact]
    public async Task Import_ProceedsWhenStoreBrowseMetadataUnavailable()
    {
        // No metadata configured + no StoreItems -> the import must still
        // succeed with the basics (cover/title/playtime) and null rich fields.
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades", PlaytimeForever = 3600 }],
                StoreItems = new Dictionary<uint, SteamStoreBrowseItem>(),
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        var body = await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        Assert.Equal(1, body!.Imported);
        var game = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id));
        Assert.Equal("Hades", game.Title);
        Assert.Null(game.Developer);
        Assert.Null(game.Publisher);
        Assert.Null(game.Year);
        // No browse metadata -> ReleaseDate stays null too, not just Year (#156).
        Assert.Null(game.ReleaseDate);
        Assert.Null(game.Description);
    }

    [Fact]
    public async Task Import_HealsStaleRemoteCover_OnReImport()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades", PlaytimeForever = 3600 }],
                StoreItems = new Dictionary<uint, SteamStoreBrowseItem>
                {
                    [1] = new()
                    {
                        AppId = 1,
                        Name = "Hades",
                        Assets = new SteamStoreAssets
                        {
                            AssetUrlFormat = "steam/apps/1/${FILENAME}?t=1",
                            LibraryCapsule2x = "aabbcc/library_600x900_2x.jpg",
                        },
                    },
                },
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        // First import creates the game (cover becomes a local /covers/<hash>).
        await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        // Simulate a game imported before the cover fix: stale raw remote URL.
        await factory.WithDbAsync(db =>
        {
            var g = db.Games.First(g => g.OwnerId == alice.Id && g.Title == "Hades");
            g.ImagePath = "https://cdn.akamai.steamstatic.com/steam/apps/1/library_600x900_2x.jpg";
            return db.SaveChangesAsync();
        });

        // Re-import: same id, already in ledger -> skips create but heals cover.
        var body = await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();
        Assert.Equal(0, body!.Imported);

        var game = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        Assert.StartsWith("/covers/", game.ImagePath);
        Assert.DoesNotContain("steampowered.com", game.ImagePath);
    }

    [Fact]
    public async Task Import_DoesNotOverwriteGoodLocalCover_OnReImport()
    {
        // A user-set local cover (e.g. via IGDB) must survive a re-import.
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades", PlaytimeForever = 3600 }],
                StoreItems = new Dictionary<uint, SteamStoreBrowseItem>
                {
                    [1] = new()
                    {
                        AppId = 1,
                        Name = "Hades",
                        Assets = new SteamStoreAssets
                        {
                            AssetUrlFormat = "steam/apps/1/${FILENAME}?t=1",
                            LibraryCapsule2x = "aabbcc/library_600x900_2x.jpg",
                        },
                    },
                },
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        // User picks a different (manual) local cover.
        await factory.WithDbAsync(db =>
        {
            var g = db.Games.First(g => g.OwnerId == alice.Id && g.Title == "Hades");
            g.ImagePath = "/covers/manual-pick.jpg";
            return db.SaveChangesAsync();
        });

        await (await alice.Client.PostAsJsonAsync("/api/accounts/steam/import",
            new { ExternalGameIds = new[] { "1" } })).ReadJsonAsync<SteamImportResultDto>();

        var game = await factory.WithDbAsync(db => db.Games.AsNoTracking().FirstAsync(g => g.OwnerId == alice.Id && g.Title == "Hades"));
        Assert.Equal("/covers/manual-pick.jpg", game.ImagePath); // untouched
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
    public async Task Games_SearchFiltersAcrossFullLibrary()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames =
                [
                    new SteamOwnedGame { AppId = 1, Name = "Hades" },
                    new SteamOwnedGame { AppId = 2, Name = "Celeste" },
                    new SteamOwnedGame { AppId = 3, Name = "Hollow Knight" },
                ],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        // No search -> whole library (2 owned, one of them the searched title).
        var all = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");
        Assert.Equal(3, all!.Titles.Length);

        // Case-insensitive title search filters server-side across the full
        // library, so a user with more than PreviewCap games can still reach a
        // specific lower-playtime title no matter where it sorts.
        var celeste = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games?q=celeste");
        Assert.Single(celeste!.Titles);
        Assert.Equal("2", celeste.Titles[0].ExternalGameId);

        var hollow = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games?q=HOLLOW");
        Assert.Single(hollow!.Titles);
        Assert.Equal("3", hollow.Titles[0].ExternalGameId);
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
    public async Task Games_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts/steam/games");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("-1", null)]
    [InlineData(null, "0")]
    [InlineData(null, "1001")]
    public async Task Games_InvalidPagination_Returns400(string? offset, string? limit)
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient { OwnedGames = [new SteamOwnedGame { AppId = 1, Name = "Hades" }] },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        var query = string.Join("&", new[]
        {
            offset is null ? null : $"offset={offset}",
            limit is null ? null : $"limit={limit}",
        }.Where(s => s is not null));
        var res = await alice.Client.GetAsync($"/api/accounts/steam/games{(query.Length > 0 ? "?" + query : "")}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Games_PaginatesWithinSearchedLibrary_WithTotal()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient
            {
                OwnedGames =
                [
                    new SteamOwnedGame { AppId = 1, Name = "Game Alpha" },
                    new SteamOwnedGame { AppId = 2, Name = "Game Beta" },
                    new SteamOwnedGame { AppId = 3, Name = "Game Gamma" },
                    new SteamOwnedGame { AppId = 4, Name = "Game Delta" },
                ],
            },
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        // Page 0 of size 2: first 2 of the full, searched library.
        var page1 = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games?offset=0&limit=2");
        Assert.Equal(4, page1!.Total);
        Assert.Equal(2, page1.Titles.Length);
        Assert.Equal("1", page1.Titles[0].ExternalGameId);
        Assert.Equal("2", page1.Titles[1].ExternalGameId);
        Assert.True(page1.Truncated); // more after this page

        // Page 1 of size 2: the remaining slice.
        var page2 = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games?offset=2&limit=2");
        Assert.Equal(4, page2!.Total);
        Assert.Equal(2, page2.Titles.Length);
        Assert.Equal("3", page2.Titles[0].ExternalGameId);
        Assert.Equal("4", page2.Titles[1].ExternalGameId);
        Assert.False(page2.Truncated); // end of set

        // Search composes with paging across the FULL library BEFORE paging.
        // "delta" is the 4th title — outside page 0 of size 2 — so this proves
        // search is applied to the whole library first: a search-after-paging
        // bug would find nothing here because "delta" is not on this page.
        var searchPage = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games?q=delta&offset=0&limit=2");
        Assert.Equal(1, searchPage!.Total); // only 1 matches the search
        Assert.Single(searchPage.Titles);
        Assert.Equal("4", searchPage.Titles[0].ExternalGameId);
        Assert.False(searchPage.Truncated);

        // Default (no offset/limit): unchanged, full preview set, Total = library size.
        var def = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");
        Assert.Equal(4, def!.Total);
        Assert.Equal(4, def.Titles.Length);
        Assert.False(def.Truncated);
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

    [Fact]
    public async Task Import_EmptySelection_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        var response = await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_OverCap_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier(),
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await LinkSteamAsync(factory, alice);

        // ImportCap defaults to 500; build a 501-element selection.
        var ids = Enumerable.Range(0, 501).Select(i => i.ToString()).ToArray();
        var response = await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = ids });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Callback_MissingCookieHalf_DoesNotConnect()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier { VerifiedSteamId = "76561198000000000" },
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        // Begin connect to mint an auth request, capture the return_to (state).
        var connectResponse = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
        var connect = await connectResponse.ReadJsonAsync<SteamConnectDto>();
        var redirectUri = new Uri(connect!.RedirectUrl!);
        var returnTo = System.Web.HttpUtility.ParseQueryString(redirectUri.Query)["openid.return_to"]!;

        // Callback WITHOUT the cookie half -> rejected, no connection.
        var (status, location) = await CallbackClientAsync(factory, alice, returnTo, null);
        Assert.Equal(HttpStatusCode.Found, status);
        Assert.Contains("steam=error", location!.ToString());

        var connected = await factory.WithDbAsync(db =>
            db.GameStoreConnections.AsNoTracking().AnyAsync(c => c.OwnerId == alice.Id));
        Assert.False(connected);
    }

    [Fact]
    public async Task Callback_Replay_SameStateAndCookie_SecondAttemptRejected()
    {
        await using var factory = new CollectifyApiFactory
        {
            SteamClient = new ScriptedSteamClient(),
            SteamOpenIdVerifier = new ScriptedSteamOpenIdVerifier { VerifiedSteamId = "76561198000000000" },
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var connectResponse = await alice.Client.PostAsync("/api/accounts/steam/connect", null);
        var connect = await connectResponse.ReadJsonAsync<SteamConnectDto>();
        var redirectUri = new Uri(connect!.RedirectUrl!);
        var returnTo = System.Web.HttpUtility.ParseQueryString(redirectUri.Query)["openid.return_to"]!;
        var cookie = connectResponse.Headers.GetValues("Set-Cookie")
            .Select(s => s.Split(';')[0]).FirstOrDefault(c => c.StartsWith("collectify.steam.state="));

        // First callback with valid cookie connects.
        var (status1, _) = await CallbackClientAsync(factory, alice, returnTo, cookie);
        Assert.Equal(HttpStatusCode.Found, status1);

        // Replay same state+cookie — atomic consume means the second is rejected.
        var (status2, location2) = await CallbackClientAsync(factory, alice, returnTo, cookie);
        Assert.Equal(HttpStatusCode.Found, status2);
        Assert.Contains("steam=error", location2!.ToString());
    }

    [Fact]
    public async Task Games_AndImport_AreOwnerScoped()
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
        var bob = await factory.CreateAuthenticatedUserAsync("bob");

        await LinkSteamAsync(factory, alice);
        await alice.Client.PostAsJsonAsync("/api/accounts/steam/import", new { ExternalGameIds = new[] { "1" } });

        // Bob is not connected and must not see Alice's ledger/import state.
        var games = await bob.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");
        Assert.Equal("notconnected", games!.Status);

        var aliceGames = await alice.Client.GetJsonAsync<SteamPreviewDto>("/api/accounts/steam/games");
        Assert.Equal("ok", aliceGames!.Status);
        Assert.Single(aliceGames.Titles.Where(t => t.State == "imported"));
    }

    // -------- Helpers ----------
    private static async Task LinkSteamAsync(CollectifyApiFactory factory, TestExtensions.TestUser user)
    {
        var connectResponse = await user.Client.PostAsync("/api/accounts/steam/connect", null);
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
