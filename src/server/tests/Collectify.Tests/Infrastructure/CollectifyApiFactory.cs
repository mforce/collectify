using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collectify.Tests.Infrastructure;

public sealed class CollectifyApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Optional override for tests that want a scripted movie provider. Set
    /// via init-only property so xUnit's IClassFixture (which requires a
    /// single public parameterless ctor) still works.
    /// </summary>
    public IMovieMetadataProvider? MovieProvider { get; init; }

    /// <summary>Optional override for tests that want a scripted music provider.</summary>
    public IMusicMetadataProvider? MusicProvider { get; init; }

    /// <summary>Optional override for tests that want a scripted game provider.</summary>
    public IGameMetadataProvider? GameProvider { get; init; }

    /// <summary>
    /// Toggles the <c>Collectify:Auth:AllowRegistration</c> flag. Defaults
    /// to <c>false</c> so the registration endpoint stays 404 unless a
    /// test deliberately opts in.
    /// </summary>
    public bool AllowRegistration { get; init; }

    public CollectifyApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Collectify:Auth:AllowRegistration"] = AllowRegistration ? "true" : "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Fully replace the production AddDbContext registration. EF Core
            // registers two services for each AddDbContext call:
            //   - DbContextOptions<T>        (the resolved options)
            //   - IDbContextOptionsConfiguration<T>  (a wrapper around the
            //     configure callback)
            // Both have to be cleared. If we only remove DbContextOptions<T>
            // the production callback still runs when options are built and
            // tries to mkdir the data directory, which blows up for non-root
            // users on Linux when AppContext.BaseDirectory resolves to "/".
            services.RemoveAll<DbContextOptions<CollectifyDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<CollectifyDbContext>>();

            services.AddDbContext<CollectifyDbContext>(opt => opt.UseSqlite(_connection));

            // Swap the real cover store out so endpoint tests don't try to
            // download a real CDN payload. The fake mirrors the public
            // contract: null/blank/local paths pass through; anything that
            // looks remote is replaced with "/covers/{12-hex}".
            services.RemoveAll<ICoverImageStore>();
            services.AddScoped<ICoverImageStore, FakeCoverImageStore>();

            // Optional override for tests that want a scripted movie
            // provider (e.g. lookup-by-id behaviour). Without this,
            // production's TmdbMovieProvider is registered with an
            // unset API key, which is fine for "configured: false"
            // assertions.
            if (MovieProvider is not null)
            {
                services.RemoveAll<IMovieMetadataProvider>();
                services.AddSingleton(MovieProvider);
            }
            if (MusicProvider is not null)
            {
                services.RemoveAll<IMusicMetadataProvider>();
                services.AddSingleton(MusicProvider);
            }
            if (GameProvider is not null)
            {
                services.RemoveAll<IGameMetadataProvider>();
                services.AddSingleton(GameProvider);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class FakeCoverImageStore : ICoverImageStore
{
    public Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return Task.FromResult<string?>(null);
        if (!imagePath.StartsWith("http://", StringComparison.Ordinal) &&
            !imagePath.StartsWith("https://", StringComparison.Ordinal))
            return Task.FromResult<string?>(imagePath);

        // Deterministic so tests can assert exact values from a remote URL.
        var hash = Math.Abs(imagePath.GetHashCode()).ToString("x").PadLeft(12, '0');
        return Task.FromResult<string?>($"/covers/{hash}");
    }
}
