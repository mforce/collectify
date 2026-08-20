using Collectify.Domain.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.MusicBrainz;

public static class MusicBrainzServiceCollectionExtensions
{
    /// <summary>
    /// Replace the stub IMetadataProvider&lt;MusicLookupResult&gt; with a real
    /// MusicBrainz-backed one. Call after AddMetadataLookup so the options
    /// are bound first; the typed HttpClient picks up
    /// MetadataLookupOptions.MusicBrainz.BaseUrl + UserAgent from the same
    /// Collectify:Metadata config block.
    /// </summary>
    public static IServiceCollection AddMusicBrainzMusicProvider(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<MusicBrainzMusicProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MetadataLookupOptions>>().Value;
            client.BaseAddress = new Uri(opts.MusicBrainz.BaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            // MB requires a contact-bearing User-Agent on every request
            // and returns 503 without one. If unset, IsConfigured returns
            // false and the provider short-circuits before ever calling
            // the wire, so an empty header here is harmless.
            if (!string.IsNullOrWhiteSpace(opts.MusicBrainz.UserAgent))
                client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.MusicBrainz.UserAgent);
        });

        services.RemoveAll<IMetadataProvider<MusicLookupResult>>();
        services.AddScoped<IMetadataProvider<MusicLookupResult>>(sp => sp.GetRequiredService<MusicBrainzMusicProvider>());

        return services;
    }
}
