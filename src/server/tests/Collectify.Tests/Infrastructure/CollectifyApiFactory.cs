using Collectify.Infrastructure.Data;
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
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }
}
