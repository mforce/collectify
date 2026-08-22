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
        var configuredProvider = configuration[DatabaseOptions.ProviderKey];
        var provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? DatabaseOptions.DefaultProvider
            : configuredProvider.Trim();

        if (!provider.Equals(DatabaseOptions.SqliteProvider, StringComparison.OrdinalIgnoreCase)
            && !provider.Equals(DatabaseOptions.PostgresProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported database provider '{provider}' configured in " +
                $"'{DatabaseOptions.ProviderKey}'. Supported providers: " +
                $"'{DatabaseOptions.SqliteProvider}', '{DatabaseOptions.PostgresProvider}'.");
        }

        services.AddSingleton<CollectifyDbContextRegistrationMarker>();

        return services.AddDbContext<CollectifyDbContext>((serviceProvider, options) =>
        {
            if (provider.Equals(DatabaseOptions.PostgresProvider, StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration[DatabaseOptions.ConnectionStringKey]
                    ?? throw new InvalidOperationException(
                        $"Database provider is '{provider}' but no connection string is configured. " +
                        "Set Collectify__Database__ConnectionString.");
                var connection = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    SearchPath = DatabaseOptions.PostgresSchema,
                };

                options.UseNpgsql(connection.ConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(DatabaseOptions.PostgresMigrationsAssembly);
                    npgsql.MigrationsHistoryTable(
                        DatabaseOptions.MigrationsHistoryTable,
                        DatabaseOptions.PostgresSchema);
                });
            }
            else
            {
                var dataDir = configuration["Collectify:DataDir"]
                    ?? Path.Combine(AppContext.BaseDirectory, "data");
                Directory.CreateDirectory(dataDir);
                var dbPath = Path.Combine(dataDir, "collectify.db");
                options.UseSqlite(
                    $"Data Source={dbPath}",
                    sqlite => sqlite.MigrationsAssembly(DatabaseOptions.SqliteMigrationsAssembly));
            }

            options.AddInterceptors(
                serviceProvider.GetRequiredService<CollectifyDbContextRegistrationMarker>());
        });
    }

    /// <summary>
    /// Returns a PostgreSQL-quoted identifier for a database name, doubling any
    /// embedded double-quote so the name cannot terminate the SQL literal early.
    /// Used to build a safe <c>CREATE DATABASE</c> statement from an untrusted or
    /// unusual name without injection. Public so the Postgres test project can
    /// exercise the injection guard directly (see C8's M-UNQUOTED-ID mutation).
    /// </summary>
    public static string QuoteDatabaseIdentifier(string databaseName)
        => new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);

    /// <summary>
    /// Ensures the target PostgreSQL database exists before migrations run.
    /// Connects to the <c>postgres</c> admin database, checks for the target
    /// database, and creates it if missing. Does not touch the schema —
    /// migrations handle that.
    /// </summary>
    public static async Task EnsurePostgresDatabaseAsync(IConfiguration configuration)
    {
        var connectionString = configuration[DatabaseOptions.ConnectionStringKey]
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The configured PostgreSQL connection string does not specify a database name.");
        }

        var quotedDatabase = QuoteDatabaseIdentifier(databaseName);

        // Connect to the default "postgres" admin database to check/create.
        builder.Database = DatabaseOptions.DefaultPostgresAdminDatabase;
        using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @db",
            adminConn);
        cmd.Parameters.AddWithValue("db", databaseName);

        var exists = await cmd.ExecuteScalarAsync();
        if (exists is null || exists is DBNull)
        {
            using var create = new NpgsqlCommand(
                $"CREATE DATABASE {quotedDatabase}",
                adminConn);
            await create.ExecuteNonQueryAsync();
        }
    }
}
