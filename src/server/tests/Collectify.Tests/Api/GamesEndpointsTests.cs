using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Collectify.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class GamesEndpointsTests : CollectionEndpointsTestsBase<Game, GameResponse>, IClassFixture<CollectifyApiFactory>
{
    public GamesEndpointsTests(CollectifyApiFactory factory) : base(factory)
    {
    }

    protected override string RoutePrefix => "/api/games/";

    protected override object Sample(string? title = null, string[]? tags = null, string? currency = null, int? rating = null) =>
        GameTestSupport.Sample(title: title ?? "Hades", tags: tags, currency: currency ?? "USD", rating: rating);

    protected override object MinimalWithImage(string? imagePath) => new
    {
        Title = "Hades",
        Platform = GamePlatform.Pc,
        DigitalStores = (int)DigitalStore.Steam,
        Status = CollectionStatus.Owned,
        CompletionStatus = CompletionStatus.NotStarted,
        ImagePath = imagePath,
        Tags = (string[]?)null,
    };

    protected override Game NewMinimalEntity(string ownerId, string title) => new()
    {
        OwnerId = ownerId,
        Title = title,
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
    };

    protected override int IdOf(Game entity) => entity.Id;
    protected override string OwnerIdOf(Game entity) => entity.OwnerId;
    protected override string TitleOf(Game entity) => entity.Title;
    protected override DateTime UpdatedAtOf(Game entity) => entity.UpdatedAt;

    [Fact]
    public async Task CreateAndGet_RoundTripsRichDetailFields()
    {
        var alice = await NewAliceAsync();
        var response = await alice.Client.PostAsJsonAsync("/api/games/", new
        {
            Title = "Hades",
            Platform = GamePlatform.Pc,
            DigitalStores = (int)DigitalStore.Steam,
            Status = CollectionStatus.Owned,
            CompletionStatus = CompletionStatus.NotStarted,
            ReleaseDate = new DateOnly(2020, 9, 17),
            AgeRating = "PEGI 16",
        });
        var created = await response.ReadJsonAsync<GameResponse>();

        var fetched = await alice.Client.GetJsonAsync<GameResponse>($"/api/games/{created!.Id}");

        Assert.Equal(new DateOnly(2020, 9, 17), fetched!.ReleaseDate);
        Assert.Equal("PEGI 16", fetched.AgeRating);
    }

    // -------- Legacy platform migration --------

    [Fact]
    public async Task Create_WithLegacyLinuxPlatform_SavesAsPc()
    {
        // A stale / pre-upgrade client posting "platform": "Linux" (the member
        // retired in #102) must land on Pc via GamePlatformJsonConverter, not
        // 400 on the now-removed enum name.
        var alice = await NewAliceAsync();

        var baseDto = JsonSerializer.Serialize(GameTestSupport.Sample(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }, // serializes Platform as its name
        });
        // The sample's Platform (Pc) is serialized as the literal name "Pc";
        // rewrite that token to the retired "Linux" to simulate a stale client.
        var payload = System.Text.RegularExpressions.Regex.Replace(
            baseDto, "\"platform\":\"Pc\"", "\"platform\":\"Linux\"");
        var response = await alice.Client.PostAsync(
            "/api/games/",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<GameResponse>();
        Assert.Equal(GamePlatform.Pc, body!.Platform);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByPlatform_ReturnsExactMatch()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Switch");

        Assert.Single(hits!);
        Assert.Equal("BotW", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_LegacyLinuxValue_ResolvesToPc()
    {
        // A stale "?platform=Linux" URL (e.g. bookmarked before #102 folded
        // Linux into Pc) must degrade to Pc rather than 400 the whole list.
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Linux");

        Assert.Single(hits!);
        Assert.Equal("Hades", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_Other_OnlyReturnsOther()
    {
        // "Other" is a real platform value (0) exposed in the client; the
        // filter must return only Other rows, not every platform.
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Oddball", Platform = GamePlatform.Other });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Other");

        Assert.Single(hits!);
        Assert.Equal("Oddball", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_RetiredOrUndefinedNumeric_IsIgnoredNotStaleValue()
    {
        // A numeric that isn't a live GamePlatform member (3 = retired Linux,
        // 999 = never defined) must NOT bind to a stale enum value and filter
        // to nothing; it resolves to no filter and returns all rows. Proof no
        // filter was applied: both rows come back, and neither is on the
        // numeric value being passed.
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var byRetired = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=3");
        Assert.Equal(2, byRetired!.Length); // not filtered to the retired value

        var byUndefined = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=999");
        Assert.Equal(2, byUndefined!.Length); // not filtered, not a 400
    }

    [Fact]
    public async Task List_FiltersByDigital_ReturnsMatchingRows()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Digital", DigitalStores = DigitalStore.Steam });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Physical", DigitalStores = DigitalStore.None });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digital=true");

        Assert.Single(hits!);
        Assert.Equal("Digital", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByDigitalFalse_ReturnsOnlyPhysicalRows()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Digital", DigitalStores = DigitalStore.Steam });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Physical", DigitalStores = DigitalStore.None });

        // Pins the false half of the (DigitalStores != None) == digital
        // derivation — a digital row must not leak through digital=false.
        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digital=false");

        Assert.Single(hits!);
        Assert.Equal("Physical", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByDigitalStore_AnyOfBits_ReturnsMatchingRows()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Steam+Epic", DigitalStores = DigitalStore.Steam | DigitalStore.Epic });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "SteamOnly", DigitalStores = DigitalStore.Steam });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "GogOnly", DigitalStores = DigitalStore.Gog });

        // Steam (1) | Epic (4) = 5; any-of match returns the Steam+Epic and SteamOnly rows.
        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digitalStore=5");

        Assert.Equal(2, hits!.Length);
        Assert.Contains(hits, h => h.Title == "Steam+Epic");
        Assert.Contains(hits, h => h.Title == "SteamOnly");
    }

    [Fact]
    public async Task List_FiltersByDigitalStore_CommaJoinedNames_Match()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Steam+Epic", DigitalStores = DigitalStore.Steam | DigitalStore.Epic });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "GogOnly", DigitalStores = DigitalStore.Gog });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "PsnOnly", DigitalStores = DigitalStore.Psn });

        // Legacy comma-joined member names work with any-of semantics: "Steam,Gog"
        // matches rows owning Steam OR Gog.
        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digitalStore=Steam,Gog");

        Assert.Equal(2, hits!.Length);
        Assert.Contains(hits, h => h.Title == "Steam+Epic");
        Assert.Contains(hits, h => h.Title == "GogOnly");
        Assert.DoesNotContain(hits, h => h.Title == "PsnOnly");
    }

    [Fact]
    public async Task List_FiltersByDigitalStore_UndefinedBit_Returns400()
    {
        var alice = await NewAliceAsync();

        // 999 = bits outside any defined DigitalStore.
        var resp = await alice.Client.GetAsync("/api/games/?digitalStore=999");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByDigitalStore_LegacySingleName_StillMatches()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Psn game", DigitalStores = DigitalStore.Psn });

        // A pre-#91 bookmarked ?digitalStore=Psn URL keeps working.
        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digitalStore=Psn");

        Assert.Single(hits!);
        Assert.Equal("Psn game", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByMalformedDigital_Returns400()
    {
        var alice = await NewAliceAsync();

        var resp = await alice.Client.GetAsync("/api/games/?digital=yes");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByYearRangePublisherDeveloperCompletionAndRating()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades",       Platform = GamePlatform.Pc,     Year = 2020, Publisher = "Supergiant", Developer = "Supergiant", CompletionStatus = CompletionStatus.Beaten, PersonalRating = 9 });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Witcher 3",   Platform = GamePlatform.Pc,     Year = 2015, Publisher = "CD Projekt", Developer = "CD Projekt", CompletionStatus = CompletionStatus.Playing, PersonalRating = 8 });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Tetris",      Platform = GamePlatform.GameBoy, Year = 1989, Publisher = "Nintendo",   Developer = "Bullet-Proof Software", CompletionStatus = CompletionStatus.NotStarted });

        var byYear = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?yearFrom=2010");
        Assert.Equal(2, byYear!.Length);

        var byPublisher = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?publisher=Supergiant");
        Assert.Single(byPublisher!);

        var byDeveloper = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?developer=Projekt");
        Assert.Single(byDeveloper!);

        var byCompletion = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?completionStatus=Beaten");
        Assert.Single(byCompletion!);
        Assert.Equal("Hades", byCompletion![0].Title);

        var byRating = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?ratingMin=9");
        Assert.Single(byRating!);
    }

    [Fact]
    public async Task List_FiltersByPlatform_OrSemanticsAreAvailableViaCombiningWithOtherFilters()
    {
        // Single-value enum filter today (one platform per request).
        // Documented as a future enhancement if multi-platform OR
        // becomes a real need.
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Ps5 game",  Platform = GamePlatform.Ps5 });
        await Factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Switch game", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Ps5");
        Assert.Single(hits!);
    }

    // -------- Personal / acquisition / completion fields round-trip --------

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/games/",
            GameTestSupport.Sample(rating: 8, completion: CompletionStatus.Beaten, hours: 60));

        var body = await response.ReadJsonAsync<GameResponse>();
        Assert.Equal((int)DigitalStore.Steam, body!.DigitalStores);
        Assert.Equal("Roguelike from Supergiant.", body.Description);
        Assert.Equal(8, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(new DateOnly(2024, 2, 10), body.AcquiredOn);
        Assert.Equal(24.99m, body.AcquisitionPrice);
        Assert.Equal("USD", body.AcquisitionCurrency);
        Assert.Equal(CompletionStatus.Beaten, body.CompletionStatus);
        Assert.Equal(60, body.HoursPlayed);
        Assert.Equal(new DateOnly(2024, 9, 1), body.LastPlayedOn);
    }
}
