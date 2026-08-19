using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Store;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class MetadataLookupServiceCollectionExtensionsTests
{
    private static ServiceCollection NewServices() => new();

    private static IConfiguration ConfigWith(params (string Key, string Value)[] entries)
    {
        var builder = new ConfigurationBuilder();
        foreach (var (key, value) in entries)
            builder.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value });
        return builder.Build();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection, IConfiguration> register, IConfiguration config)
    {
        var services = NewServices();
        register(services, config);
        return services.BuildServiceProvider();
    }

    // 1. Missing provider resolves MemoryDistributedCache + DistributedCacheAdapter.
    [Fact]
    public void AddMetadataLookup_WithoutProviderConfig_RegistersMemoryCacheAndDistributedAdapter()
    {
        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith());

        Assert.IsType<MemoryDistributedCache>(sp.GetRequiredService<IDistributedCache>());
        Assert.IsAssignableFrom<DistributedCacheAdapter>(sp.GetRequiredService<ILookupCache>());
    }

    // 2. Explicit "memory" does the same.
    [Fact]
    public void AddMetadataLookup_WithExplicitMemoryProvider_RegistersMemoryCache()
    {
        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith(("Collectify:Cache:Provider", "memory")));

        Assert.IsType<MemoryDistributedCache>(sp.GetRequiredService<IDistributedCache>());
    }

    // 3. Two child scopes resolve the same ILookupCache singleton instance.
    [Fact]
    public void AddMetadataLookup_CacheIsSingleton_ResolvesSameInstanceAcrossScopes()
    {
        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith());

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<ILookupCache>();
        var b = scope2.ServiceProvider.GetRequiredService<ILookupCache>();

        Assert.Same(a, b);
    }

    // 4. Redis binds Configuration + default InstanceName without a live connection.
    [Fact]
    public void AddMetadataLookup_Redis_BindsConfigurationAndDefaultInstanceName_WithoutConnecting()
    {
        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith(
                ("Collectify:Cache:Provider", "redis"),
                ("Collectify:Cache:Redis:Configuration", "localhost:6379,syncTimeout=1000")));

        var redisOptions = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        Assert.Equal("localhost:6379,syncTimeout=1000", redisOptions.Configuration);
        Assert.Equal("collectify:", redisOptions.InstanceName);
    }

    // 5. Redis binds an explicit deployment prefix unchanged.
    [Fact]
    public void AddMetadataLookup_Redis_BindsExplicitInstanceNameUnchanged()
    {
        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith(
                ("Collectify:Cache:Provider", "redis"),
                ("Collectify:Cache:Redis:Configuration", "localhost:6379"),
                ("Collectify:Cache:Redis:InstanceName", "mydeploy:")));

        var redisOptions = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        Assert.Equal("mydeploy:", redisOptions.InstanceName);
    }

    // 10. Missing / null / whitespace InstanceName produce "collectify:"; nonblank preserved.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMetadataLookup_Redis_MissingOrBlankInstanceName_DefaultsToCollectify(string? instanceName)
    {
        var entries = new List<(string, string)>
        {
            ("Collectify:Cache:Provider", "redis"),
            ("Collectify:Cache:Redis:Configuration", "localhost:6379"),
        };
        if (instanceName is not null)
            entries.Add(("Collectify:Cache:Redis:InstanceName", instanceName));

        using var sp = BuildProvider(
            (s, c) => s.AddMetadataLookup(c),
            ConfigWith(entries.ToArray()));

        Assert.Equal("collectify:", sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value.InstanceName);
    }

    // 6. Redis with blank configuration fails clearly at registration time.
    [Fact]
    public void AddMetadataLookup_Redis_WithBlankConfiguration_ThrowsInvalidOperation()
    {
        var services = NewServices();
        var config = ConfigWith(
            ("Collectify:Cache:Provider", "redis"),
            ("Collectify:Cache:Redis:Configuration", "  "));

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddMetadataLookup(config));
        Assert.Contains("Collectify:Cache:Redis:Configuration", ex.Message);
    }

    // 7. Unknown explicit provider fails clearly and lists memory/redis.
    [Fact]
    public void AddMetadataLookup_UnknownProvider_ThrowsAndListsValidValues()
    {
        var services = NewServices();
        var config = ConfigWith(("Collectify:Cache:Provider", "sqlite"));

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddMetadataLookup(config));
        Assert.Contains("memory", ex.Message);
        Assert.Contains("redis", ex.Message);
    }

    // 8. Metadata TTL <= zero fails options validation via ValidateOnStart.
    [Theory]
    [InlineData("0:00:00")]
    [InlineData("-0:01:00")]
    public void AddMetadataLookup_InvalidCacheTtl_FailsOnStart(string ttl)
    {
        using var host = new HostBuilder()
            .ConfigureServices((_, s) => s.AddMetadataLookup(ConfigWith(("Collectify:Metadata:CacheTtl", ttl))))
            .Build();

        var ex = Assert.Throws<OptionsValidationException>(() => host.Start());
        Assert.Contains("Collectify:Metadata:CacheTtl", ex.Message);
    }

    // 9. Steam TTL <= zero fails options validation via ValidateOnStart.
    [Theory]
    [InlineData("0:00:00")]
    [InlineData("-0:01:00")]
    public void AddSteamStoreImport_InvalidCacheTtl_FailsOnStart(string ttl)
    {
        using var host = new HostBuilder()
            .ConfigureServices((_, s) => s.AddSteamStoreImport(ConfigWith(("Collectify:Platforms:Steam:CacheTtl", ttl))))
            .Build();

        var ex = Assert.Throws<OptionsValidationException>(() => host.Start());
        Assert.Contains("Collectify:Steam:CacheTtl", ex.Message);
    }
}
