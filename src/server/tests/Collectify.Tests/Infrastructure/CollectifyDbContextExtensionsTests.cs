using Collectify.Infrastructure;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Collectify.Tests.Infrastructure;

public sealed class CollectifyDbContextExtensionsTests
{
    [Fact]
    public void Sqlite_UsesInfrastructureMigrationAssemblyAndProductionMarker()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"collectify-provider-{Guid.NewGuid():N}");
        try
        {
            using var provider = BuildProvider(Configuration(
                ("Collectify:Database:Provider", "sqlite"),
                ("Collectify:DataDir", dataDir)));
            using var scope = provider.CreateScope();

            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<CollectifyDbContext>>();
            var relational = Assert.Single(options.Extensions.OfType<RelationalOptionsExtension>());

            Assert.Equal("Collectify.Infrastructure", relational.MigrationsAssembly);
            AssertProductionMarker(scope.ServiceProvider, options);
        }
        finally
        {
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
    }

    [Fact]
    public void Postgres_UsesPostgresMigrationAssemblyHistorySchemaSearchPathAndProductionMarker()
    {
        using var provider = BuildProvider(Configuration(
            ("Collectify:Database:Provider", "postgres"),
            ("Collectify:Database:ConnectionString",
                "Host=127.0.0.1;Database=collectify;Username=collectify;Search Path=untrusted")));
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<CollectifyDbContext>>();
        var relational = Assert.Single(options.Extensions.OfType<RelationalOptionsExtension>());
        var connection = new NpgsqlConnectionStringBuilder(relational.ConnectionString);

        Assert.Equal("Collectify.PostgresMigrations", relational.MigrationsAssembly);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal("public", relational.MigrationsHistoryTableSchema);
        Assert.Equal("public", connection.SearchPath);
        AssertProductionMarker(scope.ServiceProvider, options);
    }

    [Fact]
    public void UnsupportedProvider_FailsClosedAndNamesConfigurationKeyAndValue()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(("Collectify:Database:Provider", "mysql"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddCollectifyDbContext(configuration));

        Assert.Equal(
            "Unsupported database provider 'mysql' configured in " +
            "'Collectify:Database:Provider'. Supported providers: 'sqlite', 'postgres'.",
            exception.Message);
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddCollectifyDbContext(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(
                pair => pair.Key,
                pair => (string?)pair.Value,
                StringComparer.OrdinalIgnoreCase))
            .Build();

    private static void AssertProductionMarker(
        IServiceProvider provider,
        DbContextOptions<CollectifyDbContext> options)
    {
        var markerType = typeof(CollectifyDbContext).Assembly.GetType(
            "Collectify.Infrastructure.Data.CollectifyDbContextRegistrationMarker");
        Assert.NotNull(markerType);
        var registered = provider.GetRequiredService(markerType);
        var configured = Assert.Single(
            options.FindExtension<CoreOptionsExtension>()!.Interceptors!,
            interceptor => interceptor.GetType() == markerType);
        Assert.Same(registered, configured);
    }
}
