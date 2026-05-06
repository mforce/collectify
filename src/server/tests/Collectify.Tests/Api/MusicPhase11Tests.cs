using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class MusicPhase11Tests
{
    private record AlbumResponse(
        int Id, string Title, string ArtistName, string? Description,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        int ListenCount, DateOnly? LastPlayedOn,
        string[] Tags);

    private static object Dto(
        string title = "OK Computer",
        string artist = "Radiohead",
        int? rating = null,
        string[]? tags = null,
        int listenCount = 0) => new
        {
            Title = title,
            ArtistName = artist,
            Format = MusicFormat.Cd,
            Description = "Third studio album.",
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

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Dto(rating: 10, listenCount: 42));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<AlbumResponse>();
        Assert.Equal("Third studio album.", body!.Description);
        Assert.Equal(10, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Domain.Enums.Condition.Good, body.Condition);
        Assert.Equal(new DateOnly(2024, 1, 15), body.AcquiredOn);
        Assert.Equal(12.50m, body.AcquisitionPrice);
        Assert.Equal("GBP", body.AcquisitionCurrency);
        Assert.Equal("Rough Trade", body.AcquisitionSource);
        Assert.Equal(42, body.ListenCount);
        Assert.Equal(new DateOnly(2024, 8, 1), body.LastPlayedOn);
    }

    [Fact]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/music/", Dto(rating: 11));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/music/",
            Dto(tags: ["Alt-Rock", "90s"])))
            .ReadJsonAsync<AlbumResponse>();

        Assert.Equal(new[] { "90s", "alt-rock" }, body!.Tags);
    }
}
