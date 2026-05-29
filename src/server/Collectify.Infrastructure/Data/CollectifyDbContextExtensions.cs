using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collectify.Infrastructure.Data;

/// <summary>
/// Registers <see cref="CollectifyDbContext"/> with the provider selected
/// by <c>Collectify:Database:Provider</c> config (default: <c>sqlite</c>).
/// </summary>
public static class CollectifyDbContextExtensions
{
    public static IServiceCollection AddCollectifyDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Collectify:Database:Provider"]
            ?? DatabaseOptions.DefaultProvider;

        return services.AddDbContext<CollectifyDbContext>(opt =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "postgres":
                {
                    var connectionString = configuration["Collectify:Database:ConnectionString"]
                        ?? throw new InvalidOperationException(
                            $"Database provider is '{provider}' but no connection string is configured. " +
                            "Set Collectify__Database__ConnectionString.");
                    opt.UseNpgsql(connectionString);
                    break;
                }

                case "sqlite":
                default:
                {
                    var dataDir = configuration["Collectify:DataDir"]
                        ?? Path.Combine(AppContext.BaseDirectory, "data");
                    Directory.CreateDirectory(dataDir);
                    var dbPath = Path.Combine(dataDir, "collectify.db");
                    opt.UseSqlite($"Data Source={dbPath}");
                    break;
                }
            }
        });
    }
}
