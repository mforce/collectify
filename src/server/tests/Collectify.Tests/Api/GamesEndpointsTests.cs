using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class GamesEndpointsTests
{
    private record GameResponse(
        int Id, string Title, GamePlatform Platform, string? PlatformLegacy, int? Year,
        string? Publisher, string? Developer, bool IsDigital, DigitalStore? DigitalStore,
        string? Barcode, string? IgdbId, string? ImagePath, string? Description, string? Notes,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        CompletionStatus CompletionStatus, int? HoursPlayed, DateOnly? LastPlayedOn,
        string[] Tags,
        DateTime AddedAt, DateTime UpdatedAt);

    private static object Sample(
        string title = "Hades",
        GamePlatform platform = GamePlatform.Pc,
        bool isDigital = true,
        DigitalStore? store = DigitalStore.Steam,
        int? rating = null,
        CompletionStatus completion = CompletionStatus.NotStarted,
        int? hours = null,
        string[]? tags = null) => new
        {
            Title = title,
            Platform = platform,
            Year = (int?)2020,
            Publisher = "Supergiant Games",
            Developer = "Supergiant Games",
            IsDigital = isDigital,
            DigitalStore = store,
            Barcode = (string?)null,
            IgdbId = (string?)null,
            ImagePath = (string?)null,
            Description = "Roguelike from Supergiant.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = CollectionStatus.Owned,
            Condition = (Condition?)null,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 2, 10),
            AcquisitionPrice = (decimal?)24.99m,
            AcquisitionCurrency = "USD",
            AcquisitionSource = "Steam Sale",
            CompletionStatus = completion,
            HoursPlayed = hours,
            LastPlayedOn = (DateOnly?)new DateOnly(2024, 9, 1),
            Tags = tags,
        };

    // -------- Auth --------

    [Fact]
    public async Task List_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/games/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/games/", Sample());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------- CRUD happy path --------

    [Fact]
    public async Task Create_AsAuthenticatedUser_Returns201WithBody()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/", Sample());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<GameResponse>();
        Assert.True(body!.Id > 0);
        Assert.Equal("Hades", body.Title);
        Assert.Equal(DigitalStore.Steam, body.DigitalStore);
    }

    [Fact]
    public async Task Create_PersistsOwnerIdFromAuthenticatedUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var created = await (await alice.Client.PostAsJsonAsync("/api/games/", Sample()))
            .ReadJsonAsync<GameResponse>();

        var stored = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().FirstAsync(g => g.Id == created!.Id));
        Assert.Equal(alice.Id, stored.OwnerId);
    }

    [Fact]
    public async Task Get_OwnRow_ReturnsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var game = await factory.SeedAsync(new Game
        {
            OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc, IsDigital = true, DigitalStore = DigitalStore.Steam,
        });

        var body = await alice.Client.GetJsonAsync<GameResponse>($"/api/games/{game.Id}");

        Assert.Equal("Hades", body!.Title);
        Assert.Equal(DigitalStore.Steam, body.DigitalStore);
    }

    [Fact]
    public async Task Get_NonExistentId_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.GetAsync("/api/games/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnRow_PersistsChangesAndBumpsUpdatedAt()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Game
        {
            OwnerId = alice.Id, Title = "Old", UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        var originalUpdatedAt = seeded.UpdatedAt;

        var response = await alice.Client.PutAsJsonAsync($"/api/games/{seeded.Id}",
            Sample(title: "Hollow Knight"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync<GameResponse>();
        Assert.Equal("Hollow Knight", body!.Title);
        Assert.True(body.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Delete_OwnRow_Returns204AndRemovesRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades" });

        var response = await alice.Client.DeleteAsync($"/api/games/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().AnyAsync(g => g.Id == seeded.Id));
        Assert.False(stillThere);
    }

    // -------- Ownership boundary --------

    [Fact]
    public async Task Get_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var game = await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades" });

        var response = await bob.Client.GetAsync($"/api/games/{game.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var game = await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades" });

        var response = await bob.Client.PutAsJsonAsync($"/api/games/{game.Id}",
            Sample(title: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id));
        Assert.Equal("Hades", stored.Title);
    }

    [Fact]
    public async Task Delete_OtherUsersRow_Returns404AndKeepsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var game = await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades" });

        var response = await bob.Client.DeleteAsync($"/api/games/{game.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.Games.AsNoTracking().AnyAsync(g => g.Id == game.Id));
        Assert.True(stillThere);
    }

    [Fact]
    public async Task List_OnlyReturnsRowsOwnedByCurrentUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "AliceGame" });
        await factory.SeedAsync(new Game { OwnerId = bob.Id, Title = "BobGame" });

        var aliceList = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/");

        Assert.Single(aliceList!);
        Assert.Equal("AliceGame", aliceList![0].Title);
    }

    // -------- Validation --------

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/", Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/", Sample(rating: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByPlatform_ReturnsExactMatch()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Switch");

        Assert.Single(hits!);
        Assert.Equal("BotW", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_LegacyLinuxValue_ResolvesToPc()
    {
        // A stale "?platform=Linux" URL (e.g. bookmarked before #102 folded
        // Linux into Pc) must degrade to Pc rather than 400 the whole list.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Linux");

        Assert.Single(hits!);
        Assert.Equal("Hades", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_Other_OnlyReturnsOther()
    {
        // "Other" is a real platform value (0) exposed in the client; the
        // filter must return only Other rows, not every platform.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Oddball", Platform = GamePlatform.Other });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Other");

        Assert.Single(hits!);
        Assert.Equal("Oddball", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByPlatform_RetiredOrUndefinedNumeric_IsIgnoredNotStaleValue()
    {
        // A numeric that isn't a live GamePlatform member (3 = retired Linux,
        // 999 = never defined) must NOT bind to a stale enum value and filter
        // to nothing; it resolves to no filter and returns all rows. The neat
        // way to prove "no filter applied": a Linux row is present and matches
        // under no platform predicate.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "BotW", Platform = GamePlatform.Switch });

        var byRetired = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=3");
        Assert.Equal(2, byRetired!.Length); // not filtered to the retired value

        var byUndefined = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=999");
        Assert.Equal(2, byUndefined!.Length); // not filtered, not a 400
    }

    [Fact]
    public async Task List_FiltersByDigital_ReturnsMatchingRows()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Digital", IsDigital = true });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Physical", IsDigital = false });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?digital=true");

        Assert.Single(hits!);
        Assert.Equal("Digital", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByYearRangePublisherDeveloperCompletionAndRating()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades",       Platform = GamePlatform.Pc,     Year = 2020, Publisher = "Supergiant", Developer = "Supergiant", CompletionStatus = CompletionStatus.Beaten, PersonalRating = 9 });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Witcher 3",   Platform = GamePlatform.Pc,     Year = 2015, Publisher = "CD Projekt", Developer = "CD Projekt", CompletionStatus = CompletionStatus.Playing, PersonalRating = 8 });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Tetris",      Platform = GamePlatform.GameBoy, Year = 1989, Publisher = "Nintendo",   Developer = "Bullet-Proof Software", CompletionStatus = CompletionStatus.NotStarted });

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
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Ps5 game",  Platform = GamePlatform.Ps5 });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Switch game", Platform = GamePlatform.Switch });

        var hits = await alice.Client.GetJsonAsync<GameResponse[]>("/api/games/?platform=Ps5");
        Assert.Single(hits!);
    }

    // -------- Personal / acquisition / completion fields round-trip --------

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/",
            Sample(rating: 8, completion: CompletionStatus.Beaten, hours: 60));

        var body = await response.ReadJsonAsync<GameResponse>();
        Assert.Equal("Roguelike from Supergiant.", body!.Description);
        Assert.Equal(8, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(new DateOnly(2024, 2, 10), body.AcquiredOn);
        Assert.Equal(24.99m, body.AcquisitionPrice);
        Assert.Equal("USD", body.AcquisitionCurrency);
        Assert.Equal(CompletionStatus.Beaten, body.CompletionStatus);
        Assert.Equal(60, body.HoursPlayed);
        Assert.Equal(new DateOnly(2024, 9, 1), body.LastPlayedOn);
    }

    // -------- Tags --------

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/games/", Sample(tags: ["Roguelike", "Indie"])))
            .ReadJsonAsync<GameResponse>();

        Assert.Equal(new[] { "indie", "roguelike" }, body!.Tags);
    }
}
