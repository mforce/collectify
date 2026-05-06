using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class MusicEndpointsTests
{
    private record AlbumResponse(
        int Id, string Title, string ArtistName, int? Year,
        MusicFormat Format, string? Label, string? Genres, string? Barcode,
        string? MusicBrainzReleaseId, string? DiscogsId, string? ImagePath, string? Description, string? Notes,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        int ListenCount, DateOnly? LastPlayedOn,
        string[] Tags,
        DateTime AddedAt, DateTime UpdatedAt);

    private static object Sample(
        string title = "OK Computer",
        string artist = "Radiohead",
        int? year = 1997,
        MusicFormat format = MusicFormat.Cd,
        int? rating = null,
        string[]? tags = null,
        int listenCount = 0) => new
        {
            Title = title,
            ArtistName = artist,
            Year = year,
            Format = format,
            Label = (string?)null,
            Genres = (string?)null,
            Barcode = (string?)null,
            MusicBrainzReleaseId = (string?)null,
            DiscogsId = (string?)null,
            ImagePath = (string?)null,
            Description = "Third studio album.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = CollectionStatus.Owned,
            Condition = (Condition?)Domain.Enums.Condition.Good,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 1, 15),
            AcquisitionPrice = (decimal?)12.50m,
            AcquisitionCurrency = "GBP",
            AcquisitionSource = "Rough Trade",
            ListenCount = listenCount,
            LastPlayedOn = (DateOnly?)new DateOnly(2024, 8, 1),
            Tags = tags,
        };

    // -------- Auth --------

    [Fact]
    public async Task List_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/music/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/music/", Sample());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------- CRUD happy path --------

    [Fact]
    public async Task Create_AsAuthenticatedUser_Returns201WithBody()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Sample());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<AlbumResponse>();
        Assert.True(body!.Id > 0);
        Assert.Equal("OK Computer", body.Title);
        Assert.Equal("Radiohead", body.ArtistName);
    }

    [Fact]
    public async Task Create_PersistsOwnerIdFromAuthenticatedUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var created = await (await alice.Client.PostAsJsonAsync("/api/music/", Sample()))
            .ReadJsonAsync<AlbumResponse>();

        var stored = await factory.WithDbAsync(db =>
            db.MusicAlbums.AsNoTracking().FirstAsync(a => a.Id == created!.Id));
        Assert.Equal(alice.Id, stored.OwnerId);
    }

    [Fact]
    public async Task Get_OwnRow_ReturnsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var album = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead", Year = 2000,
        });

        var body = await alice.Client.GetJsonAsync<AlbumResponse>($"/api/music/{album.Id}");

        Assert.Equal("Kid A", body!.Title);
        Assert.Equal("Radiohead", body.ArtistName);
    }

    [Fact]
    public async Task Get_NonExistentId_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.GetAsync("/api/music/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnRow_PersistsChangesAndBumpsUpdatedAt()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Old", ArtistName = "Radiohead",
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        var originalUpdatedAt = seeded.UpdatedAt;

        var response = await alice.Client.PutAsJsonAsync($"/api/music/{seeded.Id}",
            Sample(title: "In Rainbows", year: 2007));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync<AlbumResponse>();
        Assert.Equal("In Rainbows", body!.Title);
        Assert.True(body.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Delete_OwnRow_Returns204AndRemovesRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead",
        });

        var response = await alice.Client.DeleteAsync($"/api/music/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.MusicAlbums.AsNoTracking().AnyAsync(a => a.Id == seeded.Id));
        Assert.False(stillThere);
    }

    // -------- Ownership boundary --------

    [Fact]
    public async Task Get_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var album = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead",
        });

        var response = await bob.Client.GetAsync($"/api/music/{album.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var album = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead",
        });

        var response = await bob.Client.PutAsJsonAsync($"/api/music/{album.Id}",
            Sample(title: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = await factory.WithDbAsync(db =>
            db.MusicAlbums.AsNoTracking().FirstAsync(a => a.Id == album.Id));
        Assert.Equal("Kid A", stored.Title);
    }

    [Fact]
    public async Task Delete_OtherUsersRow_Returns404AndKeepsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var album = await factory.SeedAsync(new MusicAlbum
        {
            OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead",
        });

        var response = await bob.Client.DeleteAsync($"/api/music/{album.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.MusicAlbums.AsNoTracking().AnyAsync(a => a.Id == album.Id));
        Assert.True(stillThere);
    }

    [Fact]
    public async Task List_OnlyReturnsRowsOwnedByCurrentUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        await factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "AliceAlbum", ArtistName = "AliceArtist" });
        await factory.SeedAsync(new MusicAlbum { OwnerId = bob.Id, Title = "BobAlbum", ArtistName = "BobArtist" });

        var aliceList = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/");

        Assert.Single(aliceList!);
        Assert.Equal("AliceAlbum", aliceList![0].Title);
    }

    // -------- Validation --------

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyArtist_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Sample(artist: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Sample(rating: 11));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByQuery_MatchesArtistSubstring()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead" });
        await factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Funeral", ArtistName = "Arcade Fire" });

        var hits = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?query=radio");

        Assert.Single(hits!);
        Assert.Equal("Radiohead", hits![0].ArtistName);
    }

    // -------- Personal / acquisition / listen fields round-trip --------

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Sample(rating: 10, listenCount: 42));

        var body = await response.ReadJsonAsync<AlbumResponse>();
        Assert.Equal("Third studio album.", body!.Description);
        Assert.Equal(10, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Condition.Good, body.Condition);
        Assert.Equal(new DateOnly(2024, 1, 15), body.AcquiredOn);
        Assert.Equal(12.50m, body.AcquisitionPrice);
        Assert.Equal("GBP", body.AcquisitionCurrency);
        Assert.Equal("Rough Trade", body.AcquisitionSource);
        Assert.Equal(42, body.ListenCount);
        Assert.Equal(new DateOnly(2024, 8, 1), body.LastPlayedOn);
    }

    // -------- Tags --------

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/music/", Sample(tags: ["Alt-Rock", "90s"])))
            .ReadJsonAsync<AlbumResponse>();

        Assert.Equal(new[] { "90s", "alt-rock" }, body!.Tags);
    }
}
