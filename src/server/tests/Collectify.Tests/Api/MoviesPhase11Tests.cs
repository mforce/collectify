using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class MoviesPhase11Tests
{
    private record MovieResponse(
        int Id, string Title, string? Description,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        WatchStatus WatchStatus, DateOnly? LastWatchedOn, int WatchCount,
        string[] Tags);

    private static object Dto(
        string title = "Inception",
        int? rating = null,
        CollectionStatus status = CollectionStatus.Owned,
        Condition? condition = null,
        string? currency = null,
        string[]? tags = null,
        WatchStatus watchStatus = WatchStatus.Unwatched,
        int watchCount = 0) => new
        {
            Title = title,
            Formats = MovieFormat.BluRay,
            Description = "A heist on the subconscious.",
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

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Dto(
            rating: 9,
            status: CollectionStatus.Owned,
            condition: Domain.Enums.Condition.LikeNew,
            currency: "USD",
            watchStatus: WatchStatus.Watched,
            watchCount: 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.Equal("A heist on the subconscious.", body!.Description);
        Assert.Equal(9, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Domain.Enums.Condition.LikeNew, body.Condition);
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

        var body = await (await alice.Client.PostAsJsonAsync("/api/movies/", Dto(currency: "eur")))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal("EUR", body!.AcquisitionCurrency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest(int rating)
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Dto(rating: rating));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Create_WithRatingAtBoundary_Returns201(int rating)
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Dto(rating: rating));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithCurrencyOfWrongLength_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", Dto(currency: "EU"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Dto(tags: ["Sci-Fi", "Heist", "Nolan"])))
            .ReadJsonAsync<MovieResponse>();

        // Returned alphabetical, lowercased.
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
            Dto(tags: ["Sci-Fi", "sci-fi", "  Sci-Fi  ", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal(new[] { "heist", "sci-fi" }, body!.Tags);
    }

    [Fact]
    public async Task Update_ReplacesTagSetRatherThanMerging()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Dto(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"/api/movies/{created!.Id}",
            Dto(tags: ["Drama"])))
            .ReadJsonAsync<MovieResponse>();

        Assert.Equal(new[] { "drama" }, updated!.Tags);
    }

    [Fact]
    public async Task Update_WithEmptyTagArray_RemovesAllTags()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Dto(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<MovieResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"/api/movies/{created!.Id}",
            Dto(tags: Array.Empty<string>())))
            .ReadJsonAsync<MovieResponse>();

        Assert.Empty(updated!.Tags);
    }

    [Fact]
    public async Task Delete_Movie_RemovesJoinRowsButKeepsTagEntity()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/",
            Dto(tags: ["Sci-Fi"])))
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

        await alice.Client.PostAsJsonAsync("/api/movies/", Dto(tags: ["sci-fi"]));
        await bob.Client.PostAsJsonAsync("/api/movies/", Dto(title: "Bob's", tags: ["sci-fi"]));

        var totalTags = await factory.WithDbAsync(db => db.Tags.CountAsync());
        Assert.Equal(2, totalTags);

        var aliceTags = await factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(1, aliceTags);
    }
}
