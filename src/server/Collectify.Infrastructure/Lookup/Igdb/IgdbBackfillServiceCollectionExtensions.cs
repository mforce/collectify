using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collectify.Infrastructure.Lookup.Igdb;

public static class IgdbBackfillServiceCollectionExtensions
{
    /// <summary>
    /// Register the IGDB backfill background service: options (validated at
    /// startup so a bad env var is a loud start error, not a silent timer that
    /// throws mid-loop), the scoped sweep runner, and the hosted service.
    /// The service skips itself lazily when disabled or when IGDB is
    /// unconfigured.
    /// </summary>
    public static IServiceCollection AddIgdbBackfill(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<IgdbBackfillOptions>()
            .Bind(config.GetSection(IgdbBackfillOptions.SectionName))
            .Validate(
                o => o.Interval > TimeSpan.Zero
                     && o.PacingDelay >= TimeSpan.Zero
                     && o.MaxGamesPerSweep > 0
                     && o.EmptyResultAbortThreshold > 0,
                "Collectify:IgdbBackfill requires Interval > 0, PacingDelay >= 0, MaxGamesPerSweep > 0, EmptyResultAbortThreshold > 0")
            .ValidateOnStart();

        services.AddScoped<IgdbBackfillRunner>();
        services.AddHostedService<IgdbBackfillService>();

        return services;
    }
}
