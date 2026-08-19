using System.Net;
using System.Text;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class SteamClientTests
{
    private const string SteamId = "76561198000000000";

    private static SteamOptions SteamOptionsWithKey() => new()
    {
        Steam = new SteamOptions.SteamSubOptions { ApiKey = "steam-key" },
    };

    private SteamClient NewClient(StubHandler handler, LookupCacheMockStorage storage, SteamOptions? options = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.steampowered.com/") };
        var effectiveOptions = options ?? SteamOptionsWithKey();
        var expectedTtl = effectiveOptions.Steam.CacheTtl;
        storage.SetupStorage<SteamGamesResult>(expectedTtl);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        return new SteamClient(http, Options.Create(effectiveOptions), storage.Mock.Object, clock, NullLogger<SteamClient>.Instance);
    }

    private const string NonEmptyJson = """
        {
          "response": {
            "game_count": 2,
            "games": [
              { "appid": 730, "name": "CS:GO", "playtime_forever": 100 },
              { "appid": 570, "name": "Dota 2", "playtime_forever": 200 }
            ]
          }
        }
        """;

    private const string EmptyJson = """
        { "response": { "game_count": 0, "games": [] } }
        """;

    [Fact]
    public async Task GetOwnedGamesAsync_WithoutApiKey_ReturnsUnavailable_AndDoesNotCall()
    {
        var handler = new StubHandler("never called");
        var storage = new LookupCacheMockStorage();
        var client = NewClient(handler, storage, new SteamOptions
        {
            Steam = new SteamOptions.SteamSubOptions { ApiKey = null },
        });

        var result = await client.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamFetchStatus.Unavailable, result.Status);
        Assert.Empty(handler.RequestedUrls);
        Assert.Empty(storage.Writes);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_SuccessNonEmpty_PassesConfiguredTtlToCache()
    {
        var expectedTtl = TimeSpan.FromMinutes(31);
        var handler = new StubHandler(NonEmptyJson);
        var storage = new LookupCacheMockStorage();
        var client = NewClient(handler, storage, new SteamOptions
        {
            Steam = new SteamOptions.SteamSubOptions
            {
                ApiKey = "steam-key",
                CacheTtl = expectedTtl,
            },
        });

        var result = await client.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamFetchStatus.Ok, result.Status);
        Assert.Equal(2, result.Games.Count);
        Assert.Single(handler.RequestedUrls);

        Assert.NotEmpty(storage.Writes);
        var write = Assert.Single(storage.Writes);
        Assert.Equal("steam-owned", write.Provider);
        Assert.Equal("owned:" + SteamId, write.Key);
        Assert.Equal(expectedTtl, write.Ttl);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_SuccessEmpty_PassesConfiguredTtlToCache()
    {
        var handler = new StubHandler(EmptyJson);
        var storage = new LookupCacheMockStorage();
        var options = SteamOptionsWithKey();
        var client = NewClient(handler, storage, options);

        var result = await client.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamFetchStatus.Ok, result.Status);
        Assert.Empty(result.Games);

        Assert.NotEmpty(storage.Writes);
        var write = Assert.Single(storage.Writes);
        Assert.Equal("steam-owned", write.Provider);
        Assert.Equal("owned:" + SteamId, write.Key);
        Assert.Equal(options.Steam.CacheTtl, write.Ttl);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_WhenUnavailable_DoesNotCache()
    {
        var handler = new StubHandler("boom", HttpStatusCode.InternalServerError);
        var storage = new LookupCacheMockStorage();
        var client = NewClient(handler, storage);

        var result = await client.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamFetchStatus.Unavailable, result.Status);
        Assert.Empty(storage.Writes);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_RepeatedCall_ServesFromCache()
    {
        var handler = new StubHandler(NonEmptyJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewClient(handler, storage);
        var p2 = NewClient(handler, storage); // shared mock storage

        var first = await p1.GetOwnedGamesAsync(SteamId);
        var second = await p2.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamFetchStatus.Ok, first.Status);
        Assert.Equal(SteamFetchStatus.Ok, second.Status);
        Assert.Single(handler.RequestedUrls); // second served from cache
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public List<string> RequestedUrls { get; } = new();

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
