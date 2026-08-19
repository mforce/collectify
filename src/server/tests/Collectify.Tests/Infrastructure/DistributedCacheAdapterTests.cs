using System.Text;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class DistributedCacheAdapterTests
{
    private static DistributedCacheAdapter NewAdapter(IDistributedCache cache)
        => new(cache, NullLogger<DistributedCacheAdapter>.Instance);

    private record Sample(string Title, int Year);
    private record PlatformSample(GamePlatform? Platform);

    /// <summary>A controllable ISystemClock backing a real MemoryDistributedCache.</summary>
    private sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    /// <summary>
    /// Recording/throwing IDistributedCache for exact key, option, removal and
    /// backend-failure assertions. Honours a supplied already-cancelled token.
    /// </summary>
    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public List<(string Key, byte[]? Value, DistributedCacheEntryOptions? Options)> Sets { get; } = new();
        public List<string> Removed { get; } = new();
        public Dictionary<string, byte[]> Store { get; } = new();

        public Exception? ThrowOnGet { get; set; }
        public Exception? ThrowOnSet { get; set; }
        public Exception? ThrowOnRemove { get; set; }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (ThrowOnGet is not null) return Task.FromException<byte[]?>(ThrowOnGet);
            return Task.FromResult(Store.TryGetValue(key, out var v) ? v : null);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (ThrowOnSet is not null) return Task.FromException(ThrowOnSet);
            Sets.Add((key, value, options));
            Store[key] = value;
            return Task.CompletedTask;
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (ThrowOnRemove is not null) return Task.FromException(ThrowOnRemove);
            Removed.Add(key);
            Store.Remove(key);
            return Task.CompletedTask;
        }

        public byte[]? Get(string key) => Store.TryGetValue(key, out var v) ? v : null;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => Store[key] = value;

        public void Refresh(string key) { }

        public void Remove(string key) => Store.Remove(key);
    }

    // ---- Equivalent coverage preserved from the old LookupCacheTests ----

    [Fact]
    public async Task Get_BeforeSet_ReturnsDefault()
    {
        var cache = NewAdapter(new RecordingDistributedCache());

        var result = await cache.GetAsync<Sample>("tmdb", "550");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProviderAndKeyTogetherIdentifyEntries()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);

        await cache.SetAsync("tmdb", "550", new Sample("Fight Club", 1999), TimeSpan.FromDays(30));
        await cache.SetAsync("imdb", "550", new Sample("Other thing", 2010), TimeSpan.FromDays(30));

        var tmdb = await cache.GetAsync<Sample>("tmdb", "550");
        var imdb = await cache.GetAsync<Sample>("imdb", "550");

        Assert.Equal("Fight Club", tmdb!.Title);
        Assert.Equal("Other thing", imdb!.Title);
    }

    [Fact]
    public async Task Set_OnExistingKey_Overwrites()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);

        await cache.SetAsync("tmdb", "550", new Sample("Old", 1900), TimeSpan.FromDays(30));
        await cache.SetAsync("tmdb", "550", new Sample("New", 2000), TimeSpan.FromDays(30));

        var result = await cache.GetAsync<Sample>("tmdb", "550");

        Assert.Equal("New", result!.Title);
        Assert.Equal(2000, result.Year);
    }

    // ---- Plan Increment 2 adapter behaviors ----

    [Fact]
    public async Task Set_Get_RoundTripsValue_AndRecordsEnumNameNotInteger()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);

        await cache.SetAsync("igdb", "search:witcher", new PlatformSample(GamePlatform.Pc), TimeSpan.FromDays(30));

        var recorded = Assert.Single(backing.Sets);
        var json = Encoding.UTF8.GetString(recorded.Value!);
        Assert.Contains("\"platform\":\"Pc\"", json);
        Assert.DoesNotContain("\"platform\":0", json);

        var roundTripped = await cache.GetAsync<PlatformSample>("igdb", "search:witcher");
        Assert.Equal(GamePlatform.Pc, roundTripped!.Platform);
    }

    [Fact]
    public async Task Set_UsesCompositePhysicalKey()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);

        await cache.SetAsync("tmdb", "550", new Sample("Fight Club", 1999), TimeSpan.FromDays(30));

        Assert.Equal("lookup:tmdb:550", Assert.Single(backing.Sets).Key);
    }

    [Fact]
    public async Task Set_MapsAbsoluteExpirationRelativeToNow()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);
        var ttl = TimeSpan.FromMinutes(5);

        await cache.SetAsync("steam-owned", "owned:12345", new Sample("a", 1), ttl);

        var entryOptions = Assert.Single(backing.Sets).Options;
        Assert.NotNull(entryOptions);
        Assert.Equal(ttl, entryOptions!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Get_AfterRealMemoryExpiration_ReturnsDefault()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var memOptions = Options.Create(new MemoryDistributedCacheOptions
        {
            Clock = clock,
            ExpirationScanFrequency = TimeSpan.Zero,
        });
        var backing = new MemoryDistributedCache(memOptions);
        var cache = NewAdapter(backing);

        await cache.SetAsync("tmdb", "550", new Sample("Fight Club", 1999), TimeSpan.FromDays(30));
        Assert.NotNull(await cache.GetAsync<Sample>("tmdb", "550"));

        clock.UtcNow += TimeSpan.FromDays(31);

        Assert.Null(await cache.GetAsync<Sample>("tmdb", "550"));
    }

    [Fact]
    public async Task Get_WithStringEnumPayload_ReadsEnumValue()
    {
        const string json = "{\"platform\":\"Pc\"}";
        var backing = new RecordingDistributedCache();
        backing.Store["lookup:igdb:search:witcher"] = Encoding.UTF8.GetBytes(json);
        var cache = NewAdapter(backing);

        var result = await cache.GetAsync<PlatformSample>("igdb", "search:witcher");

        Assert.NotNull(result);
        Assert.Equal(GamePlatform.Pc, result!.Platform);
    }

    [Fact]
    public async Task Get_WithCorruptJson_RemovesEntryAndReturnsDefault()
    {
        var backing = new RecordingDistributedCache();
        backing.Store["lookup:igdb:search:witcher"] = Encoding.UTF8.GetBytes("{ nope");
        var cache = NewAdapter(backing);

        var result = await cache.GetAsync<PlatformSample>("igdb", "search:witcher");

        Assert.Null(result);
        Assert.Contains("lookup:igdb:search:witcher", backing.Removed);
    }

    [Fact]
    public async Task Get_WhenBackendThrows_ReturnsDefaultAndLogs()
    {
        var backing = new RecordingDistributedCache { ThrowOnGet = new InvalidOperationException("backend down") };
        var cache = NewAdapter(backing);

        var result = await cache.GetAsync<Sample>("tmdb", "550");

        Assert.Null(result);
    }

    [Fact]
    public async Task Set_WhenBackendThrows_DoesNotEscape()
    {
        var backing = new RecordingDistributedCache { ThrowOnSet = new InvalidOperationException("backend down") };
        var cache = NewAdapter(backing);

        await cache.SetAsync("tmdb", "550", new Sample("Fight Club", 1999), TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task Get_WhenBackendRemoveThrows_DoesNotEscape()
    {
        var backing = new RecordingDistributedCache
        {
            Store = { ["lookup:igdb:search:witcher"] = Encoding.UTF8.GetBytes("{ nope") },
            ThrowOnRemove = new InvalidOperationException("backend down"),
        };
        var cache = NewAdapter(backing);

        var result = await cache.GetAsync<PlatformSample>("igdb", "search:witcher");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithCancelledCallerToken_Propagates()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.GetAsync<Sample>("tmdb", "550", cts.Token));
    }

    [Fact]
    public async Task SetAsync_WithCancelledCallerToken_Propagates()
    {
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.SetAsync("tmdb", "550", new Sample("Fight Club", 1999), TimeSpan.FromDays(30), cts.Token));
    }

    [Fact]
    public async Task Get_WithCorruptJsonAndCancelledCallerToken_PropagatesCancellation()
    {
        var backing = new RecordingDistributedCache();
        backing.Store["lookup:igdb:search:witcher"] = Encoding.UTF8.GetBytes("{ nope");
        var cache = NewAdapter(backing);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The corrupt-entry removal path must still propagate caller cancellation.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.GetAsync<PlatformSample>("igdb", "search:witcher", cts.Token));
    }

    [Fact]
    public async Task Get_WhenBackendCancelsButCallerNot_ReturnsDefault()
    {
        // Backend-originated cancellation with an uncancelled caller token must
        // fail open (logged, return default), NOT propagate.
        var backing = new RecordingDistributedCache
        {
            ThrowOnGet = new OperationCanceledException("backend cancelled"),
        };
        var cache = NewAdapter(backing);

        var result = await cache.GetAsync<Sample>("tmdb", "550");

        Assert.Null(result);
    }

    [Fact]
    public async Task Set_SerializationFailure_IsNotSwallowed()
    {
        // The value is serialized OUTSIDE the backend try block, so a value
        // that cannot be serialized must surface as a serialization/contract
        // failure rather than being caught as a cache outage.
        var backing = new RecordingDistributedCache();
        var cache = NewAdapter(backing);

        await Assert.ThrowsAnyAsync<Exception>(
            () => cache.SetAsync("tmdb", "550", new ThrowsOnSerialize(), TimeSpan.FromDays(30)));
    }

    private sealed class ThrowsOnSerialize
    {
        public string Boom => throw new InvalidOperationException("boom");
    }
}
