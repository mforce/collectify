using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Upc;

public static class UpcServiceCollectionExtensions
{
    /// <summary>
    /// Wires up <see cref="UpcItemDbClient"/> as the default
    /// <see cref="IUpcLookupClient"/>. Movie / game providers receive it
    /// through DI so they can resolve a barcode to a title hint before
    /// running their own (already-cached) title search.
    /// </summary>
    public static IServiceCollection AddUpcItemDbLookup(this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<UpcItemDbClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MetadataLookupOptions>>().Value;
            client.BaseAddress = new Uri(opts.Upc.BaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.RemoveAll<IUpcLookupClient>();
        services.AddScoped<IUpcLookupClient>(sp => sp.GetRequiredService<UpcItemDbClient>());

        return services;
    }
}
