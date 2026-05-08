using System.Net;
using System.Text;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Upc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class UpcItemDbClientTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _dbOptions;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public UpcItemDbClientTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_dbOptions);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private UpcItemDbClient NewClient(StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.upcitemdb.com/") };
        var cache = new LookupCache(new CollectifyDbContext(_dbOptions), _clock);
        var options = new MetadataLookupOptions();
        return new UpcItemDbClient(http, cache, Options.Create(options), NullLogger<UpcItemDbClient>.Instance);
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
        var client = NewClient(handler);

        Assert.Null(await client.LookupAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_HitsTrialEndpointWithUpcParam()
    {
        var handler = new StubHandler(MatchJson);
        var client = NewClient(handler);

        await client.LookupAsync("0883929473076");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("prod/trial/lookup", url);
        Assert.Contains("upc=0883929473076", url);
    }

    [Fact]
    public async Task LookupAsync_MapsTitleAndBrand()
    {
        var hit = await NewClient(new StubHandler(MatchJson)).LookupAsync("0883929473076");

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
        Assert.Null(await NewClient(new StubHandler(body)).LookupAsync("0000"));
    }

    [Fact]
    public async Task LookupAsync_OnEmptyItems_ReturnsNull()
    {
        const string body = """{ "code": "OK", "total": 0, "items": [] }""";
        Assert.Null(await NewClient(new StubHandler(body)).LookupAsync("0000"));
    }

    [Fact]
    public async Task LookupAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(MatchJson);
        var c1 = NewClient(handler);
        var c2 = NewClient(handler); // shared sqlite cache

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
    public async Task LookupAsync_OnUpstreamFailure_ReturnsNullAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var client = NewClient(handler);

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
