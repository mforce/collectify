using System.Text.Json;
using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Lookup;

/// <summary>
/// Provider-agnostic cache for outbound metadata calls. Entries are keyed by
/// (Provider, Key) which is uniquely indexed at the DB level. JsonResponse
/// stores the raw provider payload so we can re-shape it later without
/// needing another HTTP round trip.
/// </summary>
public interface ILookupCache
{
    Task<T?> GetAsync<T>(string provider, string key, TimeSpan ttl, CancellationToken ct = default);
    Task SetAsync<T>(string provider, string key, T value, CancellationToken ct = default);
}

public sealed class LookupCache : ILookupCache
{
    private readonly CollectifyDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<LookupCache> _log;

    public LookupCache(CollectifyDbContext db, TimeProvider clock, ILogger<LookupCache>? log = null)
    {
        _db = db;
        _clock = clock;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LookupCache>.Instance;
    }

    public async Task<T?> GetAsync<T>(string provider, string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var entry = await _db.LookupCache.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Provider == provider && e.Key == key, ct);
        if (entry is null) return default;
        if (_clock.GetUtcNow().UtcDateTime - entry.FetchedAt > ttl) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(entry.JsonResponse, LookupCacheJson.Options);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(
                ex,
                "Ignoring stale or incompatible lookup cache entry for provider {Provider}, key {Key}, type {Type}",
                provider,
                key,
                typeof(T).FullName);
            await DeleteAsync(provider, key, ct);
            return default;
        }
    }

    public async Task SetAsync<T>(string provider, string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, LookupCacheJson.Options);
        var existing = await _db.LookupCache
            .FirstOrDefaultAsync(e => e.Provider == provider && e.Key == key, ct);
        if (existing is null)
        {
            _db.LookupCache.Add(new LookupCacheEntry
            {
                Provider = provider,
                Key = key,
                JsonResponse = json,
                FetchedAt = _clock.GetUtcNow().UtcDateTime,
            });
        }
        else
        {
            existing.JsonResponse = json;
            existing.FetchedAt = _clock.GetUtcNow().UtcDateTime;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task DeleteAsync(string provider, string key, CancellationToken ct)
    {
        await _db.LookupCache
            .Where(e => e.Provider == provider && e.Key == key)
            .ExecuteDeleteAsync(ct);
    }
}
