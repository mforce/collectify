using System.Net;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class DashboardEndpointsTests
{
    private record DashboardCounts(int Movies, int Music, int Games);
    private record DashboardRecent(
        string Type, int Id, string Title, int? Year, string? ImagePath, DateTime AddedAt);
    private record DashboardSummary(DashboardCounts Counts, DashboardRecent[] Recent);

    [Fact]
    public async Task Get_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_EmptyCollection_ReturnsZeroCountsAndEmptyRecent()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<DashboardSummary>("/api/dashboard");

        Assert.NotNull(body);
        Assert.Equal(0, body!.Counts.Movies);
        Assert.Equal(0, body.Counts.Music);
        Assert.Equal(0, body.Counts.Games);
        Assert.Empty(body.Recent);
    }

    [Fact]
    public async Task Get_ReturnsCountsScopedToTheCurrentOwner()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");

        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Inception" });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Tenet" });
        await factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "OK Computer", ArtistName = "Radiohead", Format = MusicFormat.Cd });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Hades", Platform = GamePlatform.Pc });
        // Bob's row -- must not appear in Alice's counts.
        await factory.SeedAsync(new Movie { OwnerId = bob.Id, Title = "Bob's movie" });

        var body = await alice.Client.GetJsonAsync<DashboardSummary>("/api/dashboard");

        Assert.Equal(2, body!.Counts.Movies);
        Assert.Equal(1, body.Counts.Music);
        Assert.Equal(1, body.Counts.Games);
    }

    [Fact]
    public async Task Get_RecentIsInterleavedByAddedAtDescAndCappedAt6()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        // Seed across types out of chronological order so the endpoint
        // really has to merge by AddedAt and not by source list.
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Old movie", AddedAt = t0 });
        await factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Newer album", ArtistName = "x", Format = MusicFormat.Cd, AddedAt = t0.AddMinutes(3) });
        await factory.SeedAsync(new Game { OwnerId = alice.Id, Title = "Newest game", Platform = GamePlatform.Pc, AddedAt = t0.AddMinutes(5) });
        await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Mid movie", AddedAt = t0.AddMinutes(2) });

        // Pad past the 6-item cap so we can confirm the limit.
        for (int i = 0; i < 4; i++)
        {
            await factory.SeedAsync(new Movie
            {
                OwnerId = alice.Id, Title = $"Filler {i}", AddedAt = t0.AddSeconds(i),
            });
        }

        var body = await alice.Client.GetJsonAsync<DashboardSummary>("/api/dashboard");

        Assert.NotNull(body);
        Assert.Equal(6, body!.Recent.Length);
        // Sorted by AddedAt desc -- verify the top three are the
        // explicitly-distinct entries we seeded.
        Assert.Equal("Newest game", body.Recent[0].Title);
        Assert.Equal("Newer album", body.Recent[1].Title);
        Assert.Equal("Mid movie", body.Recent[2].Title);
        // Type discriminator round-trips per row.
        Assert.Equal("games", body.Recent[0].Type);
        Assert.Equal("music", body.Recent[1].Type);
        Assert.Equal("movies", body.Recent[2].Type);
    }

    [Fact]
    public async Task Get_RecentEntriesIncludeIdYearAndImagePath()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var m = await factory.SeedAsync(new Movie
        {
            OwnerId = alice.Id,
            Title = "Inception",
            Year = 2010,
            ImagePath = "/covers/abc1234567890def",
        });

        var body = await alice.Client.GetJsonAsync<DashboardSummary>("/api/dashboard");

        var hit = Assert.Single(body!.Recent);
        Assert.Equal(m.Id, hit.Id);
        Assert.Equal(2010, hit.Year);
        Assert.Equal("/covers/abc1234567890def", hit.ImagePath);
    }
}
