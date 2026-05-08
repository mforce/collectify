using System.Net;
using System.Text;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.GiantBomb;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class GiantBombGameUpcClientTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _dbOptions;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public GiantBombGameUpcClientTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_dbOptions);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private GiantBombGameUpcClient NewClient(StubHandler handler, MetadataLookupOptions? overrideOptions = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.giantbomb.com/api/") };
        var cache = new LookupCache(new CollectifyDbContext(_dbOptions), _clock);
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            GiantBomb = new GiantBombOptions { ApiKey = "secret-key", UserAgent = "Collectify/1.0 (test@example.com)" },
        };
        return new GiantBombGameUpcClient(http, cache, Options.Create(options), NullLogger<GiantBombGameUpcClient>.Instance);
    }

    private const string MatchJson = """
        {
          "status_code": 1,
          "error": "OK",
          "results": [
            {
              "id": 36105,
              "name": "Halo 3 (Limited Edition)",
              "game": { "id": 21251, "name": "Halo 3" }
            }
          ]
        }
        """;

    [Fact]
    public void IsConfigured_RequiresBothApiKeyAndUserAgent()
    {
        var both = NewClient(new StubHandler("[]"));
        var noKey = NewClient(new StubHandler("[]"), new MetadataLookupOptions
        {
            GiantBomb = new GiantBombOptions { ApiKey = null, UserAgent = "ua" },
        });
        var noUa = NewClient(new StubHandler("[]"), new MetadataLookupOptions
        {
            GiantBomb = new GiantBombOptions { ApiKey = "k", UserAgent = "  " },
        });

        Assert.True(both.IsConfigured);
        Assert.False(noKey.IsConfigured);
        Assert.False(noUa.IsConfigured);
    }

    [Fact]
    public async Task LookupAsync_NotConfigured_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var client = NewClient(handler, new MetadataLookupOptions());

        Assert.Null(await client.LookupAsync("0883929473076"));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_BlankBarcode_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var client = NewClient(handler);

        Assert.Null(await client.LookupAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_HitsReleasesEndpoint_WithUpcFilterAndKey()
    {
        var handler = new StubHandler(MatchJson);
        await NewClient(handler).LookupAsync("0883929473076");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("releases/", url);
        Assert.Contains("filter=upc:0883929473076", url);
        Assert.Contains("api_key=secret-key", url);
        Assert.Contains("format=json", url);
    }

    [Fact]
    public async Task LookupAsync_PrefersGameName_OverReleaseName()
    {
        // Release name carries regional cruft ("Limited Edition"); the
        // game name is the canonical title we want for the IGDB search.
        var hit = await NewClient(new StubHandler(MatchJson)).LookupAsync("0883929473076");

        Assert.NotNull(hit);
        Assert.Equal("Halo 3", hit!.Title);
    }

    [Fact]
    public async Task LookupAsync_FallsBackToReleaseName_WhenGameNameMissing()
    {
        const string body = """
            {
              "status_code": 1,
              "results": [
                { "id": 1, "name": "Some Obscure Release", "game": null }
              ]
            }
            """;
        var hit = await NewClient(new StubHandler(body)).LookupAsync("9999999999999");

        Assert.NotNull(hit);
        Assert.Equal("Some Obscure Release", hit!.Title);
    }

    [Fact]
    public async Task LookupAsync_NonOkStatusCode_ReturnsNull()
    {
        // status_code 100 is GiantBomb's "Invalid API Key"; payload may
        // still be 200 OK on the wire. Don't trust it.
        const string body = """
            { "status_code": 100, "error": "Invalid API Key", "results": [] }
            """;
        Assert.Null(await NewClient(new StubHandler(body)).LookupAsync("0883929473076"));
    }

    [Fact]
    public async Task LookupAsync_EmptyResults_ReturnsNull()
    {
        const string body = """{ "status_code": 1, "results": [] }""";
        Assert.Null(await NewClient(new StubHandler(body)).LookupAsync("0000"));
    }

    [Fact]
    public async Task LookupAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(MatchJson);
        var c1 = NewClient(handler);
        var c2 = NewClient(handler);

        await c1.LookupAsync("0883929473076");
        await c2.LookupAsync("0883929473076");

        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task LookupAsync_OnUpstreamFailure_ReturnsNullAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var client = NewClient(handler);

        Assert.Null(await client.LookupAsync("0883929473076"));
        await client.LookupAsync("0883929473076");
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
