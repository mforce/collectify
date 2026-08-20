using Collectify.Domain.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Tmdb;

public static class TmdbServiceCollectionExtensions
{
    /// <summary>
    /// Replace the stub IMovieMetadataProvider with a real TMDB-backed one.
    /// Call after AddMetadataLookup so the options are bound first; the
    /// typed HttpClient picks up MetadataLookupOptions.Tmdb.BaseUrl from
    /// the same Collectify:Metadata config block.
    /// </summary>
    public static IServiceCollection AddTmdbMovieProvider(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<TmdbMovieProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MetadataLookupOptions>>().Value;
            client.BaseAddress = new Uri(opts.Tmdb.BaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        // AddMetadataLookup uses TryAddScoped for the stubs, so a hard
        // RemoveAll + AddScoped here wins regardless of call order. The bare
        // generic covers search/by-id/barcode routes; IMovieMetadataProvider
        // additionally carries the IMDB-id capability for /movies/by-imdb-id.
        services.RemoveAll<IMetadataProvider<MovieLookupResult>>();
        services.AddScoped<IMetadataProvider<MovieLookupResult>>(sp => sp.GetRequiredService<TmdbMovieProvider>());

        services.RemoveAll<IMovieMetadataProvider>();
        services.AddScoped<IMovieMetadataProvider>(sp => sp.GetRequiredService<TmdbMovieProvider>());

        return services;
    }
}
