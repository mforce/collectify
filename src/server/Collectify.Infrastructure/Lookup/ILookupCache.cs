namespace Collectify.Infrastructure.Lookup;

/// <summary>
/// Provider-agnostic cache for outbound metadata calls. Entries are keyed by
/// provider and provider-specific key; cached values are disposable and may
/// be rebuilt from the upstream provider at any time.
/// </summary>
public interface ILookupCache
{
    Task<T?> GetAsync<T>(string provider, string key, CancellationToken ct = default);
    Task SetAsync<T>(string provider, string key, T value, TimeSpan ttl, CancellationToken ct = default);
}
