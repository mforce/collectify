using System.Net;
using System.Net.Http.Json;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class HealthEndpointTests : IClassFixture<CollectifyApiFactory>
{
    private readonly CollectifyApiFactory _factory;

    public HealthEndpointTests(CollectifyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_Unauthenticated_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    private record HealthResponse(string Status);
}
