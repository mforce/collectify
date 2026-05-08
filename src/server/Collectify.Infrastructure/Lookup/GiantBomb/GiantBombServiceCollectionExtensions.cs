using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.GiantBomb;

public static class GiantBombServiceCollectionExtensions
{
    /// <summary>
    /// Wires up <see cref="GiantBombGameUpcClient"/> as the default
    /// <see cref="IGiantBombGameUpcClient"/>. The IGDB game provider
    /// pulls it as a UPC fallback when UPCitemdb misses; tests can swap
    /// in a fake before calling AddIgdbGameProvider.
    /// </summary>
    public static IServiceCollection AddGiantBombGameUpcClient(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<GiantBombGameUpcClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MetadataLookupOptions>>().Value;
            client.BaseAddress = new Uri(opts.GiantBomb.BaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            // GiantBomb returns 403 without a User-Agent. If unset, the
            // client's IsConfigured short-circuits before sending, so an
            // empty header here is harmless.
            if (!string.IsNullOrWhiteSpace(opts.GiantBomb.UserAgent))
                client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.GiantBomb.UserAgent);
        });

        services.RemoveAll<IGiantBombGameUpcClient>();
        services.AddScoped<IGiantBombGameUpcClient>(sp => sp.GetRequiredService<GiantBombGameUpcClient>());

        return services;
    }
}
