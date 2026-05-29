using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

    /// <summary>
    /// Ensures the target PostgreSQL database exists before migrations run.
    /// Connects to the <c>postgres</c> admin database, checks for the target
    /// database, and creates it if missing. Does not touch the schema —
    /// migrations handle that.
    /// </summary>
    public static async Task EnsurePostgresDatabaseAsync(IConfiguration configuration)
    {
        var connectionString = configuration["Collectify:Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;

        // Connect to the default "postgres" admin database to check/create.
        builder.Database = "postgres";
        using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @db",
            adminConn);
        cmd.Parameters.AddWithValue("@db", (object?)databaseName!);

        if (await cmd.ExecuteScalarAsync() is not DBNull and not int)
        {
            await new NpgsqlCommand(
                $"CREATE DATABASE \"{databaseName}\"",
                adminConn).ExecuteNonQueryAsync();
        }
    }
}
