using System.Net;
using System.Text;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Igdb;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class IgdbGameProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _dbOptions;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public IgdbGameProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_dbOptions);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private IgdbGameProvider NewProvider(
        HttpMessageHandler handler,
        FakeAuth? auth = null,
        MetadataLookupOptions? overrideOptions = null,
        FakeUpcClient? upc = null,
        FakeGiantBombClient? giantBomb = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.igdb.com/v4/") };
        var cache = new LookupCache(new CollectifyDbContext(_dbOptions), _clock);
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "client", TwitchClientSecret = "secret" },
        };
        return new IgdbGameProvider(
            http,
            auth ?? new FakeAuth("client", "tok"),
            upc ?? FakeUpcClient.NotRecognised(),
            giantBomb ?? FakeGiantBombClient.NotConfigured(),
            cache,
            Options.Create(options),
            NullLogger<IgdbGameProvider>.Instance);
    }

    private const string SingleGameJson = """
        [
          {
            "id": 1942,
            "name": "The Witcher 3: Wild Hunt",
            "first_release_date": 1431993600,
            "summary": "A monster hunter searches for his adopted daughter.",
            "cover": { "image_id": "co1wyy" },
            "involved_companies": [
              { "company": { "name": "CD Projekt Red" }, "developer": true,  "publisher": false },
              { "company": { "name": "Warner Bros" },    "developer": false, "publisher": true  }
            ],
            "platforms": [ { "name": "PC" }, { "name": "PlayStation 4" } ],
            "genres":    [ { "name": "RPG" }, { "name": "Adventure" } ]
          }
        ]
        """;

    [Fact]
    public void IsConfigured_ReflectsBothCredentials()
    {
        var both = NewProvider(new StubHandler("[]"));
        var missingSecret = NewProvider(new StubHandler("[]"), overrideOptions: new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "client", TwitchClientSecret = null },
        });
        var missingId = NewProvider(new StubHandler("[]"), overrideOptions: new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "  ", TwitchClientSecret = "secret" },
        });

        Assert.True(both.IsConfigured);
        Assert.False(missingSecret.IsConfigured);
        Assert.False(missingId.IsConfigured);
    }

    [Fact]
    public async Task SearchAsync_WithoutCredentials_ShortCircuitsToEmpty_AndDoesNotCallIgdb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, overrideOptions: new MetadataLookupOptions());

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        Assert.Empty(await provider.SearchAsync("   "));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_PostsApicalypseBodyWithSearchAndFields_AndAuthHeaders()
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.SearchAsync("witcher");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.igdb.com/v4/games", req.Url);
        Assert.Contains("search \"witcher\";", req.Body);
        Assert.Contains("fields name,first_release_date,cover.image_id", req.Body);
        Assert.Contains("limit 10;", req.Body);
        Assert.Equal("client", req.ClientId);
        Assert.Equal("Bearer tok", req.Authorization);
    }

    [Fact]
    public async Task SearchAsync_MapsGame_IncludingDeveloperPublisherCoverPlatformAndYear()
    {
        var provider = NewProvider(new StubHandler(SingleGameJson));

        var hit = Assert.Single(await provider.SearchAsync("witcher"));

        Assert.Equal("igdb", hit.Provider);
        Assert.Equal("1942", hit.ProviderKey);
        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
        Assert.Equal(2015, hit.Year);
        Assert.Equal("PC", hit.Platform);
        Assert.Equal("CD Projekt Red", hit.Developer);
        Assert.Equal("Warner Bros", hit.Publisher);
        Assert.Equal("RPG, Adventure", hit.Genres);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg", hit.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_GameWithoutCover_ReturnsNullImageUrl()
    {
        const string json = """
            [ { "id": 1, "name": "Indie", "platforms": [ { "name": "PC" } ] } ]
            """;
        var hit = (await NewProvider(new StubHandler(json)).SearchAsync("indie")).Single();

        Assert.Null(hit.ImageUrl);
        Assert.Null(hit.Year);
        Assert.Null(hit.Developer);
        Assert.Null(hit.Publisher);
    }

    [Fact]
    public async Task SearchAsync_EscapesQuotesInQuery()
    {
        var handler = new StubHandler("[]");
        var provider = NewProvider(handler);

        await provider.SearchAsync("foo \"bar\" baz");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("search \"foo \\\"bar\\\" baz\";", req.Body);
    }

    [Fact]
    public async Task SearchAsync_RepeatedQuery_ServesFromCache()
    {
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler); // shared sqlite cache

        await p1.SearchAsync("witcher");
        await p2.SearchAsync("WITCHER"); // case-insensitive cache key

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_OnUpstreamFailure_ReturnsEmptyAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var provider = NewProvider(handler);

        Assert.Empty(await provider.SearchAsync("x"));
        await provider.SearchAsync("x"); // should retry, not serve a cached []
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetByIdAsync_PostsApicalypseWhereClause()
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.GetByIdAsync("1942");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("where id = 1942;", req.Body);
        Assert.Contains("limit 1;", req.Body);
        Assert.DoesNotContain("search ", req.Body);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyResponse_ReturnsNullWithoutCaching()
    {
        var handler = new StubHandler("[]");
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync("1942"));
        await provider.GetByIdAsync("1942");
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonNumericKey_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync("not-a-number"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetByIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler);

        var first = await p1.GetByIdAsync("1942");
        var second = await p2.GetByIdAsync("1942");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("The Witcher 3: Wild Hunt", second!.Title);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetByIdAsync_AndSearchAsync_UseSeparateCacheNamespaces()
    {
        // A search whose query happens to equal an IGDB id must not satisfy
        // a by-id lookup for that same id.
        var searchHandler = new StubHandler("""
            [ { "id": 9999, "name": "Different", "platforms": [ { "name": "PC" } ] } ]
            """);
        await NewProvider(searchHandler).SearchAsync("1942");
        Assert.Single(searchHandler.Requests);

        var idHandler = new StubHandler(SingleGameJson);
        var byId = await NewProvider(idHandler).GetByIdAsync("1942");

        Assert.Equal("The Witcher 3: Wild Hunt", byId!.Title);
        Assert.Single(idHandler.Requests);
    }

    [Fact]
    public async Task PostGames_On401_RefreshesTokenAndRetriesOnce()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.OK, SingleGameJson));
        var auth = new FakeAuth("client", "stale-tok") { OnRefresh = "fresh-tok" };
        var provider = NewProvider(handler, auth);

        var hits = await provider.SearchAsync("witcher");

        Assert.Single(hits);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer stale-tok", handler.Requests[0].Authorization);
        Assert.Equal("Bearer fresh-tok", handler.Requests[1].Authorization);
        Assert.Equal(1, auth.RefreshCount);
    }

    [Fact]
    public async Task PostGames_OnDouble401_GivesUpAndDoesNotLoop()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.Unauthorized, "{}"));
        var auth = new FakeAuth("client", "bad");
        var provider = NewProvider(handler, auth);

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task PostGames_WhenAuthReturnsNoToken_ShortCircuitsWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var auth = new FakeAuth("client", token: null);
        var provider = NewProvider(handler, auth);

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Empty(handler.Requests);
    }

    // ---------- SearchByBarcodeAsync ----------

    [Fact]
    public async Task SearchByBarcodeAsync_NotConfigured_ShortCircuitsAndDoesNotHitUpc()
    {
        var upc = FakeUpcClient.Returning("Hades");
        var provider = NewProvider(new StubHandler(SingleGameJson), overrideOptions: new MetadataLookupOptions(), upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0123456789012"));
        Assert.Empty(upc.RequestedBarcodes);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_BlankBarcode_ReturnsEmptyWithoutCalling()
    {
        var upc = FakeUpcClient.Returning("Hades");
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("   "));
        Assert.Empty(upc.RequestedBarcodes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_UpcMiss_ReturnsEmptyWithoutTitleSearch()
    {
        var upc = FakeUpcClient.NotRecognised();
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0000000000000"));
        Assert.Single(upc.RequestedBarcodes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_DispatchesToIgdbTitleSearch_WithUpcTitle()
    {
        var upc = FakeUpcClient.Returning("The Witcher 3");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler, upc: upc);

        var hits = await provider.SearchByBarcodeAsync("0883929473076");

        Assert.Single(hits);
        var req = Assert.Single(handler.Requests);
        // The title surfaced by UPC drives the Apicalypse search clause.
        Assert.Contains("search \"The Witcher 3\";", req.Body);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_RepeatedCallsServeFromCache()
    {
        var upc = FakeUpcClient.Returning("The Witcher 3");
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler, upc: upc);
        var p2 = NewProvider(handler, upc: upc); // shared sqlite cache

        await p1.SearchByBarcodeAsync("0883929473076");
        await p2.SearchByBarcodeAsync("0883929473076");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_FallsBackToGiantBomb_WhenUpcMisses()
    {
        // UPCitemdb didn't recognise the code; GiantBomb did. The
        // GiantBomb-resolved title drives the IGDB Apicalypse search.
        var upc = FakeUpcClient.NotRecognised();
        var giantBomb = FakeGiantBombClient.Returning("Halo 3");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler, upc: upc, giantBomb: giantBomb);

        var hits = await provider.SearchByBarcodeAsync("0883929473076");

        Assert.Single(hits);
        Assert.Single(upc.RequestedBarcodes);
        Assert.Single(giantBomb.RequestedBarcodes);
        var req = Assert.Single(handler.Requests);
        Assert.Contains("search \"Halo 3\";", req.Body);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_DoesNotCallGiantBomb_WhenUpcAlreadyHit()
    {
        // Belt-and-braces: when the primary UPC source resolves the
        // code, we shouldn't fan out to GiantBomb (saves a request and
        // keeps results consistent with the primary).
        var upc = FakeUpcClient.Returning("The Witcher 3");
        var giantBomb = FakeGiantBombClient.Returning("Different Game");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler, upc: upc, giantBomb: giantBomb);

        await provider.SearchByBarcodeAsync("0883929473076");

        Assert.Empty(giantBomb.RequestedBarcodes);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_BothSourcesMiss_ReturnsEmptyWithoutTitleSearch()
    {
        var upc = FakeUpcClient.NotRecognised();
        var giantBomb = FakeGiantBombClient.ConfiguredNotRecognised();
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc, giantBomb: giantBomb);

        Assert.Empty(await provider.SearchByBarcodeAsync("0000000000000"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_SkipsGiantBomb_WhenItIsNotConfigured()
    {
        // Default fake = NotConfigured; the provider must not consult
        // it (the IsConfigured guard short-circuits before any call).
        var upc = FakeUpcClient.NotRecognised();
        var giantBomb = FakeGiantBombClient.NotConfigured();
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc, giantBomb: giantBomb);

        Assert.Empty(await provider.SearchByBarcodeAsync("0000000000000"));
        Assert.Empty(giantBomb.RequestedBarcodes);
    }

    private sealed class FakeAuth : IIgdbAuth
    {
        private string? _token;
        public int RefreshCount { get; private set; }
        public string? OnRefresh { get; init; }
        public string? ClientId { get; }

        public FakeAuth(string clientId, string? token)
        {
            ClientId = clientId;
            _token = token;
        }

        public Task<string?> GetTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            if (forceRefresh)
            {
                RefreshCount++;
                if (OnRefresh is not null) _token = OnRefresh;
            }
            return Task.FromResult(_token);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, string Body, string? ClientId, string? Authorization);

    private static async Task<CapturedRequest> CaptureAsync(HttpRequestMessage request)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
        request.Headers.TryGetValues("Client-ID", out var clientIds);
        return new CapturedRequest(
            request.Method,
            request.RequestUri!.AbsoluteUri,
            body,
            clientIds?.FirstOrDefault(),
            request.Headers.Authorization?.ToString());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public List<CapturedRequest> Requests { get; } = new();

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CaptureAsync(request));
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _queue;
        public List<CapturedRequest> Requests { get; } = new();

        public SequenceHandler(params (HttpStatusCode Status, string Body)[] items)
        {
            _queue = new Queue<(HttpStatusCode, string)>(items);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CaptureAsync(request));
            var (status, body) = _queue.Count > 0 ? _queue.Dequeue() : (HttpStatusCode.OK, "[]");
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
