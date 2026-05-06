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
        string? TmdbId, string? ImdbId, string? ImagePath, string? Notes,
        DateTime AddedAt, DateTime UpdatedAt);

    private static object SampleDto(string title = "Inception", int? year = 2010, MovieFormat formats = MovieFormat.BluRay) =>
        new
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
            Notes = (string?)null,
        };

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

        var response = await client.PostAsJsonAsync("/api/movies/", SampleDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAuthenticatedUser_Returns201WithBody()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", SampleDto("The Matrix", 1999));

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

        var created = await (await alice.Client.PostAsJsonAsync("/api/movies/", SampleDto()))
            .ReadJsonAsync<MovieResponse>();

        var stored = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().FirstAsync(m => m.Id == created!.Id));
        Assert.Equal(alice.Id, stored.OwnerId);
    }

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", SampleDto(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithWhitespaceTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", SampleDto(title: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task Update_WithEmptyTitle_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var seeded = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await alice.Client.PutAsJsonAsync($"/api/movies/{seeded.Id}",
            SampleDto(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            SampleDto(title: "New Title", year: 2001));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.Equal("New Title", body!.Title);
        Assert.Equal(2001, body.Year);
        Assert.True(body.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Update_OtherUsersRow_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var aliceMovie = await factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        var response = await bob.Client.PutAsJsonAsync($"/api/movies/{aliceMovie.Id}",
            SampleDto(title: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await factory.WithDbAsync(db =>
            db.Movies.AsNoTracking().FirstAsync(m => m.Id == aliceMovie.Id));
        Assert.Equal("Heat", stored.Title);
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
}
