using Collectify.Infrastructure.Lookup.Stub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collectify.Infrastructure.Lookup;

public static class MetadataLookupServiceCollectionExtensions
{
    /// <summary>
    /// Register the lookup seam: options binding, cache, IHttpClientFactory,
    /// and a stub for every provider slot. Concrete providers (TMDB,
    /// MusicBrainz, IGDB) replace the stubs when their PRs land via
    /// services.AddHttpClient&lt;T&gt;() + a Replace() of the relevant
    /// IXxxMetadataProvider registration.
    /// </summary>
    public static IServiceCollection AddMetadataLookup(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<MetadataLookupOptions>()
            .Bind(config.GetSection(MetadataLookupOptions.SectionName));

        services.AddHttpClient();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILookupCache, LookupCache>();

        // Register stubs as the default. A real provider PR will Replace()
        // these (or call TryAdd() before this runs) once it ships.
        services.TryAddScoped<IMovieMetadataProvider, StubMovieProvider>();
        services.TryAddScoped<IMusicMetadataProvider, StubMusicProvider>();
        services.TryAddScoped<IGameMetadataProvider, StubGameProvider>();

        return services;
    }
}
