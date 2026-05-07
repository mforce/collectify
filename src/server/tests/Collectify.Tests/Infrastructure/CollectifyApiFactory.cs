using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collectify.Tests.Infrastructure;

public sealed class CollectifyApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CollectifyApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
