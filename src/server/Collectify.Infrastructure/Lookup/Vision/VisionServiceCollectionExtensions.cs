using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collectify.Infrastructure.Lookup.Vision;

public static class VisionServiceCollectionExtensions
{
    public static IServiceCollection AddVisionClient(
        this IServiceCollection services, IConfiguration _)
    {
        services.AddHttpClient<CloudVisionClient>("vision")
            .SetHandlerLifetime(TimeSpan.FromMinutes(2));

        services.AddScoped<IVisionClient, CloudVisionClient>();
        return services;
    }
}
