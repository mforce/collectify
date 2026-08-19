using System.Net;
using System.Text;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Upc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class UpcItemDbClientTests
{
    private UpcItemDbClient NewClient(
        StubHandler handler,
        LookupCacheMockStorage storage,
        MetadataLookupOptions? overrideOptions = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.upcitemdb.com/") };
        var options = overrideOptions ?? new MetadataLookupOptions();
        var expectedTtl = options.CacheTtl;
        storage.SetupStorage<UpcLookupResult>(expectedTtl);
        return new UpcItemDbClient(http, storage.Mock.Object, Options.Create(options), NullLogger<UpcItemDbClient>.Instance);
    }

    private const string MatchJson = """
        {
          "code": "OK",
          "total": 1,
          "items": [
            {
              "ean": "0883929473076",
              "title": "Inception (Blu-ray)",
              "brand": "Warner Bros",
              "manufacturer": "Warner Home Video"
            }
          ]
        }
        """;

    [Fact]
    public async Task LookupAsync_BlankBarcode_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var client = NewClient(handler, new LookupCacheMockStorage());

        Assert.Null(await client.LookupAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_HitsTrialEndpointWithUpcParam()
    {
        var handler = new StubHandler(MatchJson);
        var client = NewClient(handler, new LookupCacheMockStorage());

        await client.LookupAsync("0883929473076");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("prod/trial/lookup", url);
        Assert.Contains("upc=0883929473076", url);
    }

    [Fact]
    public async Task LookupAsync_MapsTitleAndBrand()
    {
        var hit = await NewClient(new StubHandler(MatchJson), new LookupCacheMockStorage()).LookupAsync("0883929473076");

        Assert.NotNull(hit);
        Assert.Equal("Inception (Blu-ray)", hit!.Title);
        Assert.Equal("Warner Bros", hit.Brand);
        Assert.Equal("Warner Home Video", hit.Manufacturer);
    }

    [Fact]
    public async Task LookupAsync_TreatsBlankTitleAsNotRecognised()
    {
        const string body = """
            { "code": "OK", "total": 1, "items": [ { "title": "" } ] }
            """;
        Assert.Null(await NewClient(new StubHandler(body), new LookupCacheMockStorage()).LookupAsync("0000"));
    }

    [Fact]
    public async Task LookupAsync_OnEmptyItems_ReturnsNull()
    {
        const string body = """{ "code": "OK", "total": 0, "items": [] }""";
        Assert.Null(await NewClient(new StubHandler(body), new LookupCacheMockStorage()).LookupAsync("0000"));
    }

    [Fact]
    public async Task LookupAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(MatchJson);
        var storage = new LookupCacheMockStorage();
        var c1 = NewClient(handler, storage);
        var c2 = NewClient(handler, storage); // shared mock storage

        var first = await c1.LookupAsync("0883929473076");
        var second = await c2.LookupAsync("0883929473076");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Inception (Blu-ray)", second!.Title);
        // Single network call across both invocations -- the trial endpoint
        // is rate-limited, so the cache earning its keep here matters.
        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_ForwardsConfiguredTtlOnWrite()
    {
        var expectedTtl = TimeSpan.FromMinutes(29);
        var handler = new StubHandler(MatchJson);
        var storage = new LookupCacheMockStorage();
        var client = NewClient(handler, storage, new MetadataLookupOptions { CacheTtl = expectedTtl });

        await client.LookupAsync("0883929473076");

        Assert.NotEmpty(storage.Writes);
        Assert.All(storage.Writes, w => Assert.Equal(expectedTtl, w.Ttl));
    }

    [Fact]
    public async Task LookupAsync_OnUpstreamFailure_ReturnsNullAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var client = NewClient(handler, new LookupCacheMockStorage());

        Assert.Null(await client.LookupAsync("0883929473076"));
        await client.LookupAsync("0883929473076"); // should retry, not serve cached null
        Assert.Equal(2, handler.RequestedUrls.Count);
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
