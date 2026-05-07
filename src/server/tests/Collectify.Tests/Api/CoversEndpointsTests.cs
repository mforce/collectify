using System.Net;
using Collectify.Domain.Entities;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class CoversEndpointsTests
{
    [Fact]
    public async Task Get_ExistingHash_Returns200WithStoredContentType()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/covers/abc1234567890def");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Get_MissingHash_Returns404()
    {
        await using var factory = new CollectifyApiFactory();

        var response = await factory.CreateClient().GetAsync("/covers/0000000000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    // These get URI-decoded to traversal attempts; must not reach the DB.
    [InlineData("/covers/..%2F..%2Fetc%2Fpasswd")]
    [InlineData("/covers/%2E%2E")]
    [InlineData("/covers/has-a-dash")]
    [InlineData("/covers/UPPERCASE")]
    [InlineData("/covers/short")]
    [InlineData("/covers/way-too-long-to-possibly-be-a-cover-hash")]
    public async Task Get_MalformedHash_Returns404(string url)
    {
        await using var factory = new CollectifyApiFactory();

        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_DoesNotRequireAuthentication()
    {
        // <img> tags need to load without an explicit fetch + cookie dance.
        // The hash is 16 hex chars derived from a URL the user already saw
        // in their own collection, so it's effectively unguessable.
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "deadbeefcafe1234",
            ContentType = "image/png",
            Bytes = [1, 2, 3],
        });

        var response = await factory.CreateClient().GetAsync("/covers/deadbeefcafe1234"); // no cookie

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }
}
