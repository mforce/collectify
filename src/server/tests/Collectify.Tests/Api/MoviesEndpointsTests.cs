using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class MoviesEndpointsTests
{
    private record MovieResponse(
        int Id, string Title, string? OriginalTitle, int? Year,
        MovieFormat Formats, string? Director, int? RuntimeMinutes,
        string? Studio, string? Genres, string? Barcode,
        string? TmdbId, string? ImdbId, string? ImagePath, string? Description, string? Notes,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        WatchStatus WatchStatus, DateOnly? LastWatchedOn, int WatchCount,
        string[] Tags,
        DateTime AddedAt, DateTime UpdatedAt);

    private static object Sample(
        string title = "Inception",
        int? year = 2010,
        MovieFormat formats = MovieFormat.BluRay,
        int? rating = null,
        CollectionStatus status = CollectionStatus.Owned,
        Condition? condition = null,
        string? currency = null,
        WatchStatus watchStatus = WatchStatus.Unwatched,
        int watchCount = 0,
        string[]? tags = null) => new
        {
            Title = title,
            OriginalTitle = (string?)null,
            Year = year,
            Formats = formats,
            Director = "Christopher Nolan",
            RuntimeMinutes = 148,
            Studio = "Warner Bros.",
            Genres = "Sci-Fi, Thriller",
            Barcode = (string?)null,
            TmdbId = (string?)null,
            ImdbId = (string?)null,
            ImagePath = (string?)null,
            Description = "A heist on the subconscious.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = status,
            Condition = condition,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 1, 15),
            AcquisitionPrice = (decimal?)19.99m,
            AcquisitionCurrency = currency,
            AcquisitionSource = "Amazon",
            WatchStatus = watchStatus,
            LastWatchedOn = (DateOnly?)new DateOnly(2024, 6, 1),
            WatchCount = watchCount,
            Tags = tags,
        };

    // -------- Auth --------

    [Fact]
    public async Task List_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/movies/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/movies/", Sample());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------- CRUD happy path --------

    [Fact]
    public async Task Create_AsAuthenticatedUser_Returns201WithBody()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample("The Matrix", 1999));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Id > 0);
        Assert.Equal("The Matrix", body.Title);
        Assert.Equal(1999, body.Year);
    }

    [Fact]
    public async Task Create_PersistsOwnerIdFromAuthenticatedUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/", Sample()))
            .ReadJsonAsync<MovieResponse>();

        var stored = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().FirstAsync(m => m.Id == created!.Id));
        Assert.Equal(alice.Id, stored.OwnerId);
    }

    [Fact]
    public async Task Get_OwnRow_ReturnsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var movie = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat", Year = 1995 });

        var body = await alice.Client.GetJsonAsync<MovieResponse>($"/api/movies/{movie.Id}");

        Assert.Equal(movie.Id, body!.Id);
        Assert.Equal("Heat", body.Title);
    }

    [Fact]
    public async Task Get_NonExistentId_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.GetAsync("/api/movies/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnRow_PersistsChangesAndBumpsUpdatedAt()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Movie
        {
            OwnerId = alice.Id,
            Title = "Old Title",
            Year = 2000,
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        var originalUpdatedAt = seeded.UpdatedAt;

        var response = await alice.Client.PutAsJsonAsync($"/api/movies/{seeded.Id}",
            Sample(title: "New Title", year: 2001));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.Equal("New Title", body!.Title);
        Assert.Equal(2001, body.Year);
        Assert.True(body.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Delete_OwnRow_Returns204AndRemovesRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await alice.Client.DeleteAsync($"/api/movies/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().AnyAsync(m => m.Id == seeded.Id));
        Assert.False(stillThere);
    }

    // -------- Ownership boundary --------

    [Fact]
    public async Task Get_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var aliceMovie = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await bob.Client.GetAsync($"/api/movies/{aliceMovie.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var aliceMovie = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await bob.Client.PutAsJsonAsync($"/api/movies/{aliceMovie.Id}",
            Sample(title: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().FirstAsync(m => m.Id == aliceMovie.Id));
        Assert.Equal("Heat", stored.Title);
    }

    [Fact]
    public async Task Delete_OtherUsersRow_Returns404AndKeepsRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var aliceMovie = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await bob.Client.DeleteAsync($"/api/movies/{aliceMovie.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().AnyAsync(m => m.Id == aliceMovie.Id));
        Assert.True(stillThere);
    }

    [Fact]
    public async Task List_OnlyReturnsRowsOwnedByCurrentUser()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Alice-Movie" });
        await factory.SeedAsync(new Movie { OwnerId = bob.Id, Title = "Bob-Movie" });

        var aliceList = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/");

        Assert.Single(aliceList!);
        Assert.Equal("Alice-Movie", aliceList![0].Title);
    }

    // -------- Validation --------

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithWhitespaceTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(title: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await alice.Client.PutAsJsonAsync($"/api/movies/{seeded.Id}",
            Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest(int rating)
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(rating: rating));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Create_WithRatingAtBoundary_Returns201(int rating)
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(rating: rating));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithCurrencyOfWrongLength_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(currency: "EU"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByQuery_MatchesTitleSubstring()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Inception" });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Interstellar" });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "The Matrix" });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?query=inter");

        Assert.Single(hits!);
        Assert.Equal("Interstellar", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByYear_ReturnsExactMatches()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "A", Year = 2010 });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "B", Year = 2010 });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "C", Year = 2020 });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?year=2010");

        Assert.Equal(2, hits!.Length);
        Assert.All(hits, m => Assert.Equal(2010, m.Year));
    }

    // -------- Personal / acquisition / watch fields round-trip --------

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Sample(
            rating: 9,
            condition: Condition.LikeNew,
            currency: "USD",
            watchStatus: WatchStatus.Watched,
            watchCount: 3));

        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.Equal("A heist on the subconscious.", body!.Description);
        Assert.Equal(9, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Condition.LikeNew, body.Condition);
        Assert.Equal(new DateOnly(2024, 1, 15), body.AcquiredOn);
        Assert.Equal(19.99m, body.AcquisitionPrice);
        Assert.Equal("USD", body.AcquisitionCurrency);
        Assert.Equal("Amazon", body.AcquisitionSource);
        Assert.Equal(WatchStatus.Watched, body.WatchStatus);
        Assert.Equal(new DateOnly(2024, 6, 1), body.LastWatchedOn);
        Assert.Equal(3, body.WatchCount);
    }

    [Fact]
    public async Task Create_NormalizesCurrencyToUppercase()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/movies/", Sample(currency: "eur")))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal("EUR", body!.AcquisitionCurrency);
    }

    // -------- Tags --------

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Sample(tags: ["Sci-Fi", "Heist", "Nolan"])))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal(new[] { "heist", "nolan", "sci-fi" }, body!.Tags);

        var tagCount = await factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(3, tagCount);
    }

    [Fact]
    public async Task Create_WithDuplicateTagsInArray_DeduplicatesIgnoreCase()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Sample(tags: ["Sci-Fi", "sci-fi", "  Sci-Fi  ", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal(new[] { "heist", "sci-fi" }, body!.Tags);
    }

    [Fact]
    public async Task Update_ReplacesTagSetRatherThanMerging()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Sample(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"/api/movies/{created!.Id}",
            Sample(tags: ["Drama"])))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal(new[] { "drama" }, updated!.Tags);
    }

    [Fact]
    public async Task Update_WithEmptyTagArray_RemovesAllTags()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Sample(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"/api/movies/{created!.Id}",
            Sample(tags: Array.Empty<string>())))
            .ReadJsonAsync<MovieResponse>();

        Assert.Empty(updated!.Tags);
    }

    [Fact]
    public async Task Delete_Movie_RemovesJoinRowsButKeepsTagEntity()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Sample(tags: ["Sci-Fi"])))
            .ReadJsonAsync<MovieResponse>();

        var delete = await alice.Client.DeleteAsync($"/api/movies/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var tagsLeft = await factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(1, tagsLeft);
    }

    [Fact]
    public async Task Tags_AreOwnerScoped_BetweenUsers()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");

        await alice.Client.PostAsJsonAsync("/api/movies/", Sample(tags: ["sci-fi"]));
        await bob.Client.PostAsJsonAsync("/api/movies/", Sample(title: "Bob's", tags: ["sci-fi"]));

        var totalTags = await factory.WithDbAsync(db => db.Tags.CountAsync());
        Assert.Equal(2, totalTags);

        var aliceTags = await factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(1, aliceTags);
    }
}
