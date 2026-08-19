using Collectify.Infrastructure.Lookup.Stub;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collectify.Infrastructure.Lookup;

public static class MetadataLookupServiceCollectionExtensions
{
    /// <summary>
    /// Register the lookup seam: options binding + validate-on-start, the
    /// distributed cache (memory default or opt-in Redis), the adapter as a
    /// singleton, IHttpClientFactory, and a stub for every provider slot.
    /// Concrete providers (TMDB, MusicBrainz, IGDB) replace the stubs when
    /// their PRs land via services.AddHttpClient&lt;T&gt;() + a Replace() of
    /// the relevant IXxxMetadataProvider registration.
    /// </summary>
    public static IServiceCollection AddMetadataLookup(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<MetadataLookupOptions>()
            .Bind(config.GetSection(MetadataLookupOptions.SectionName))
            .Validate(options => options.CacheTtl > TimeSpan.Zero, "Collectify:Metadata:CacheTtl must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient();
        services.TryAddSingleton(TimeProvider.System);

        var cacheProvider = config["Collectify:Cache:Provider"]?.Trim();
        switch (cacheProvider?.ToLowerInvariant())
        {
            case null or "" or "memory":
                services.AddDistributedMemoryCache();
                break;

            case "redis":
                var redisConfiguration = config["Collectify:Cache:Redis:Configuration"];
                if (string.IsNullOrWhiteSpace(redisConfiguration))
                    throw new InvalidOperationException(
                        "Collectify:Cache:Redis:Configuration is required when Collectify:Cache:Provider is redis.");

                var configuredInstanceName = config["Collectify:Cache:Redis:InstanceName"];
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConfiguration;
                    options.InstanceName = string.IsNullOrWhiteSpace(configuredInstanceName)
                        ? "collectify:"
                        : configuredInstanceName;
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Collectify cache provider '{cacheProvider}'. Valid values are 'memory' and 'redis'.");
        }

        services.AddSingleton<ILookupCache, DistributedCacheAdapter>();

        // Register stubs as the default. A real provider PR will Replace()
        // these (or call TryAdd() before this runs) once it ships.
        services.TryAddScoped<IMovieMetadataProvider, StubMovieProvider>();
        services.TryAddScoped<IMusicMetadataProvider, StubMusicProvider>();
        services.TryAddScoped<IGameMetadataProvider, StubGameProvider>();

        return services;
    }
}
