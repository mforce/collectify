using System.Net;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class CoversEndpointsTests
{
    [Fact]
    public async Task Get_ExistingFile_Returns200WithCorrectContentType()
    {
        await using var factory = new CollectifyApiFactory();
        Directory.CreateDirectory(factory.CoversDir);
        await File.WriteAllBytesAsync(
            Path.Combine(factory.CoversDir, "abc1234567890def.jpg"),
            new byte[] { 0xFF, 0xD8, 0xFF });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/covers/abc1234567890def.jpg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_MissingFile_Returns404()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/covers/missing0000000.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    // These get URI-decoded to traversal attempts; the endpoint must reject.
    [InlineData("/covers/..%2F..%2Fetc%2Fpasswd")]
    [InlineData("/covers/%2E%2E%2Fsecrets")]
    public async Task Get_PathTraversalAttempt_Returns404(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_DoesNotRequireAuthentication()
    {
        // <img> tags in the SPA need to load these without an explicit fetch.
        // The hash-named file system makes them effectively unguessable.
        await using var factory = new CollectifyApiFactory();
        Directory.CreateDirectory(factory.CoversDir);
        await File.WriteAllBytesAsync(Path.Combine(factory.CoversDir, "deadbeefcafe1234.png"), new byte[] { 1 });

        var client = factory.CreateClient(); // no auth cookie
        var response = await client.GetAsync("/covers/deadbeefcafe1234.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }
}
