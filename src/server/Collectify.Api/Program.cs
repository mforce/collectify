using System.Text.Json.Serialization;
using Collectify.Api.Endpoints;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Images;
using Collectify.Infrastructure.Lookup.Tmdb;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Resolve / create the data directory inside the registration callback so the
// filesystem touch happens lazily — only if this DbContext registration is
// actually used. Tests replace it with an in-memory SqliteConnection and
// never trigger the mkdir, which avoids platform-specific permission
// surprises (e.g. AppContext.BaseDirectory resolving to "/" inside the
// WebApplicationFactory test host).
builder.Services.AddDbContext<CollectifyDbContext>(opt =>
{
    var dataDir = builder.Configuration["Collectify:DataDir"]
        ?? Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(dataDir);
    var dbPath = Path.Combine(dataDir, "collectify.db");
    opt.UseSqlite($"Data Source={dbPath}");
});

builder.Services
    .AddIdentityCore<AppUser>(opt =>
    {
        opt.Password.RequireDigit = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequiredLength = 8;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<CollectifyDbContext>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, opt =>
    {
        opt.Cookie.Name = "collectify.auth";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
        opt.ExpireTimeSpan = TimeSpan.FromDays(30);
        opt.SlidingExpiration = true;
        opt.LoginPath = "/login";
        opt.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddMetadataLookup(builder.Configuration);
builder.Services.AddTmdbMovieProvider(builder.Configuration);

// Cover-image cache. The on-disk path is resolved at DI-resolution time
// (not at builder time) so test hosts that override Collectify:DataDir
// via ConfigureAppConfiguration get the right value -- those callbacks
// fire during Build, which is after this code runs.
builder.Services.AddHttpClient(CoverImageStore.HttpClientName);
builder.Services.AddSingleton<ICoverImageStore>(sp => new CoverImageStore(
    ResolveCoversDir(sp.GetRequiredService<IConfiguration>()),
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<ILogger<CoverImageStore>>()));

static string ResolveCoversDir(IConfiguration config) =>
    Path.Combine(config["Collectify:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data"), "covers");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapMoviesEndpoints();
app.MapMusicEndpoints();
app.MapGamesEndpoints();
app.MapTagEndpoints();
app.MapLookupEndpoints();
app.MapCoversEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(webRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program { }
