using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class GamesPhase11Tests
{
    private record GameResponse(
        int Id, string Title, string? Description,
        int? PersonalRating, CollectionStatus Status, Condition? Condition,
        DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
        CompletionStatus CompletionStatus, int? HoursPlayed, DateOnly? LastPlayedOn,
        string[] Tags);

    private static object Dto(
        string title = "Hades",
        int? rating = null,
        CompletionStatus completion = CompletionStatus.NotStarted,
        int? hours = null,
        string[]? tags = null) => new
        {
            Title = title,
            IsDigital = true,
            DigitalStore = DigitalStore.Steam,
            Description = "Roguelike from Supergiant.",
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

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/",
            Dto(rating: 8, completion: CompletionStatus.Beaten, hours: 60));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

    [Fact]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/games/", Dto(rating: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await (await alice.Client.PostAsJsonAsync("/api/games/",
            Dto(tags: ["Roguelike", "Indie"])))
            .ReadJsonAsync<GameResponse>();

        Assert.Equal(new[] { "indie", "roguelike" }, body!.Tags);
    }
}
