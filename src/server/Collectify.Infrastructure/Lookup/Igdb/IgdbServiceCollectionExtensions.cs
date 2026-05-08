using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

public static class IgdbServiceCollectionExtensions
{
    /// <summary>
    /// Replace the stub <see cref="IGameMetadataProvider"/> with a real
    /// IGDB-backed one. Call after AddMetadataLookup so the options are
    /// bound first; the IgdbAuth singleton caches the Twitch OAuth token
    /// across every typed-HttpClient instance. The api.igdb.com BaseAddress
    /// is wired here so the provider can use relative URIs ("games").
    /// </summary>
    public static IServiceCollection AddIgdbGameProvider(this IServiceCollection services, IConfiguration _)
    {
        // Twitch token client. No baked-in auth headers -- the URL carries
        // client_id / client_secret as query params for the
        // client-credentials grant.
        services.AddHttpClient(IgdbAuth.HttpClientName, client =>
        {
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddSingleton<IIgdbAuth>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(IgdbAuth.HttpClientName);
            return new IgdbAuth(
                http,
                sp.GetRequiredService<IOptions<MetadataLookupOptions>>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IgdbAuth>>());
        });

        services.AddHttpClient<IgdbGameProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MetadataLookupOptions>>().Value;
            client.BaseAddress = new Uri(opts.Igdb.BaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.RemoveAll<IGameMetadataProvider>();
        services.AddScoped<IGameMetadataProvider>(sp => sp.GetRequiredService<IgdbGameProvider>());

        return services;
    }
}
