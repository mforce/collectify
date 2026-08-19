using System.Text;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class DistributedCacheAdapterTests
{
    private static DistributedCacheAdapter NewAdapter(IDistributedCache cache)
        => new(cache, NullLogger<DistributedCacheAdapter>.Instance);

    private static DistributedCacheAdapter NewAdapter(
        IDistributedCache cache,
        ILogger<DistributedCacheAdapter> logger)
        => new(cache, logger);

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
        public Action<CancellationToken>? OnRemove { get; set; }

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
            OnRemove?.Invoke(token);
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
    public async Task Failures_DoNotLogRawCacheKeysOrIdentifiers()
    {
        const string steamId = "76561198000000000";
        const string barcode = "0883929473076";
        const string privateQuery = "private query";
        var logger = new CapturingLogger<DistributedCacheAdapter>();

        var readFailure = new RecordingDistributedCache
        {
            ThrowOnGet = new InvalidOperationException($"backend failed for lookup:steam-owned:owned:{steamId}"),
        };
        await NewAdapter(readFailure, logger)
            .GetAsync<Sample>("steam-owned", $"owned:{steamId}");

        var writeFailure = new RecordingDistributedCache
        {
            ThrowOnSet = new InvalidOperationException($"backend failed for lookup:upc:barcode:{barcode}"),
        };
        await NewAdapter(writeFailure, logger)
            .SetAsync("upc", $"barcode:{barcode}", new Sample("Movie", 1999), TimeSpan.FromDays(1));

        var corruptRemovalFailure = new RecordingDistributedCache
        {
            Store = { [$"lookup:tmdb:search:{privateQuery}"] = Encoding.UTF8.GetBytes("{ nope") },
            ThrowOnRemove = new InvalidOperationException($"backend failed for lookup:tmdb:search:{privateQuery}"),
        };
        await NewAdapter(corruptRemovalFailure, logger)
            .GetAsync<Sample>("tmdb", $"search:{privateQuery}");

        Assert.Equal(4, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
        foreach (var sensitiveValue in new[] { steamId, barcode, privateQuery })
        {
            Assert.All(logger.Entries, entry => Assert.DoesNotContain(sensitiveValue, entry.Message));
            Assert.All(
                logger.Entries,
                entry => Assert.DoesNotContain(
                    entry.State,
                    property => property.Value?.ToString()?.Contains(sensitiveValue, StringComparison.Ordinal) == true));
        }
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
        using var cts = new CancellationTokenSource();
        CancellationToken removalToken = default;
        backing.OnRemove = token =>
        {
            removalToken = token;
            cts.Cancel();
        };
        var cache = NewAdapter(backing);

        // The corrupt-entry removal path must still propagate caller cancellation.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.GetAsync<PlatformSample>("igdb", "search:witcher", cts.Token));
        Assert.Equal(cts.Token, removalToken);
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IEnumerable<KeyValuePair<string, object?>>
                ?? Array.Empty<KeyValuePair<string, object?>>();
            Entries.Add(new LogEntry(formatter(state, exception), properties.ToList(), exception));
        }
    }

    private sealed record LogEntry(
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> State,
        Exception? Exception);
}
