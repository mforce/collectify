using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class TagEndpointsTests
{
    private record TagResponse(int Id, string Name);

    [Fact]
    public async Task List_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_NewName_Returns201Lowercased()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/tags/", new { Name = "Sci-Fi" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<TagResponse>();
        Assert.Equal("sci-fi", body!.Name);
    }

    [Fact]
    public async Task Create_ExistingName_Returns200WithExistingTag()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var first = await (await alice.Client.PostAsJsonAsync("/api/tags/", new { Name = "Sci-Fi" }))
            .ReadJsonAsync<TagResponse>();
        var dup = await alice.Client.PostAsJsonAsync("/api/tags/", new { Name = "sci-fi" });

        Assert.Equal(HttpStatusCode.OK, dup.StatusCode);
        var body = await dup.ReadJsonAsync<TagResponse>();
        Assert.Equal(first!.Id, body!.Id);
    }

    [Fact]
    public async Task Create_Empty_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsJsonAsync("/api/tags/", new { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnerScopedAlphabetically()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        await factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "zeta" });
        await factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "alpha" });
        await factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "mu" });
        await factory.SeedAsync(new Tag { OwnerId = bob.Id, Name = "bobs-only" });

        var aliceTags = await alice.Client.GetJsonAsync<TagResponse[]>("/api/tags/");

        Assert.Equal(new[] { "alpha", "mu", "zeta" }, aliceTags!.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task Delete_OwnTag_Returns204AndRemovesIt()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var tag = await factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "scifi" });

        var response = await alice.Client.DeleteAsync($"/api/tags/{tag.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db => db.Tags.AnyAsync(t => t.Id == tag.Id));
        Assert.False(stillThere);
    }

    [Fact]
    public async Task Delete_OtherUsersTag_Returns404AndKeepsIt()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var bob = await factory.CreateAuthenticatedUserAsync("bob");
        var aliceTag = await factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "scifi" });

        var response = await bob.Client.DeleteAsync($"/api/tags/{aliceTag.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stillThere = await factory.WithDbAsync(db => db.Tags.AnyAsync(t => t.Id == aliceTag.Id));
        Assert.True(stillThere);
    }
}
