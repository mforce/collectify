using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Lookup;

public sealed class DistributedCacheAdapter : ILookupCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheAdapter> _log;

    public DistributedCacheAdapter(
        IDistributedCache cache,
        ILogger<DistributedCacheAdapter> log)
    {
        _cache = cache;
        _log = log;
    }

    public async Task<T?> GetAsync<T>(
        string provider,
        string key,
        CancellationToken ct = default)
    {
        var physicalKey = GetPhysicalKey(provider, key);
        byte[]? bytes;

        try
        {
            bytes = await _cache.GetAsync(physicalKey, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Lookup cache read failed for provider {Provider}", provider);
            return default;
        }

        if (bytes is null)
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(bytes, LookupCacheJson.Options);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(
                ex,
                "Ignoring stale or incompatible lookup cache entry for provider {Provider}, type {Type}",
                provider,
                typeof(T).FullName);
            await RemoveCorruptEntryAsync(provider, physicalKey, ct);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string provider,
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var physicalKey = GetPhysicalKey(provider, key);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, LookupCacheJson.Options);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        };

        try
        {
            await _cache.SetAsync(physicalKey, bytes, options, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Lookup cache write failed for provider {Provider}", provider);
        }
    }

    private async Task RemoveCorruptEntryAsync(
        string provider,
        string physicalKey,
        CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(physicalKey, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Lookup cache removal failed for provider {Provider}", provider);
        }
    }

    private static string GetPhysicalKey(string provider, string key)
        => $"lookup:{provider}:{key}";
}
