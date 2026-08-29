using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Collectify.Api.Endpoints;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Vision;
using Collectify.Infrastructure.Lookup.Igdb;
using Collectify.Infrastructure.Lookup.Images;
using Collectify.Infrastructure.Lookup.MusicBrainz;
using Collectify.Infrastructure.Lookup.Tmdb;
using Collectify.Infrastructure.Lookup.Upc;
using Collectify.Infrastructure.Store;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database provider selection via Collectify:Database:Provider (default: sqlite).
// Tests replace this registration entirely with an in-memory SQLite connection.
builder.Services.AddCollectifyDbContext(builder.Configuration);

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName));

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
builder.Services.AddRateLimiter(rate =>
{
    // Per-client fixed-window guard for the public Steam OpenID callback so a
    // stream of forged requests can't spam the outbound Steam verify step.
    //
    // Partition keyed on HttpContext.Connection.RemoteIpAddress. When the app
    // sits behind a reverse proxy, UseForwardedHeaders (configured below with
    // the trusted proxy networks) rewrites RemoteIpAddress to the real client
    // BEFORE rate limiting runs, so we always partition on the true client even
    // behind a proxy — and never touch a spoofable forwarded header by hand.
    // If no proxy is trusted (directly-reachable install), RemoteIpAddress is
    // the honest peer address, so the rate limit can't be bypassed by sending a
    // fake X-Forwarded-For.
    rate.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rate.AddPolicy("steam-callback", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    // GamePlatform keeps its own converter so retired/write paths degrade
    // (Linux -> Pc) instead of 400-ing; all other enums use the string form.
    opt.SerializerOptions.Converters.Add(new GamePlatformJsonConverter());
    // Non-flags, write-boundary enums reject any value that is not a defined
    // member (issue #115): a defined integer still binds (pre-existing wire
    // contract), but an arbitrary/retired integer (e.g. 999) now 400s instead
    // of persisting an unnamed enum value. Registered before the global
    // converter below so each wins for its own type.
    opt.SerializerOptions.Converters.Add(new DefinedEnumConverter<CollectionStatus>());
    opt.SerializerOptions.Converters.Add(new DefinedEnumConverter<Condition>());
    opt.SerializerOptions.Converters.Add(new DefinedEnumConverter<WatchStatus>());
    opt.SerializerOptions.Converters.Add(new DefinedEnumConverter<CompletionStatus>());
    opt.SerializerOptions.Converters.Add(new DefinedEnumConverter<MusicFormat>());
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddMetadataLookup(builder.Configuration);
// UPC client first: TMDB and IGDB providers depend on it for barcode
// lookups (MusicBrainz indexes barcodes natively and skips it).
builder.Services.AddUpcItemDbLookup(builder.Configuration);
builder.Services.AddTmdbMovieProvider(builder.Configuration);
builder.Services.AddMusicBrainzMusicProvider(builder.Configuration);
builder.Services.AddIgdbGameProvider(builder.Configuration);
builder.Services.AddVisionClient(builder.Configuration);
// Background IGDB metadata backfill (issue #132). Sweeps Game rows with
// IgdbId == null after a Steam import (or any manual game) and fills metadata
// + cover in the background. Skips itself when IGDB is unconfigured.
builder.Services.AddIgdbBackfill(builder.Configuration);
builder.Services.AddSteamStoreImport(builder.Configuration);

// Cover-image cache. Bytes live in the CoverImages table alongside the
// rest of the data so a backup of collectify.db is a complete snapshot.
// The handler caps automatic redirects (defense against a hostile/looping
// URL chain) and the client bounds the download size; see CoverImageStore.
builder.Services.AddHttpClient(CoverImageStore.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxAutomaticRedirections = CoverImageStore.MaxRedirects,
    AllowAutoRedirect = true,
});
builder.Services.AddScoped<ICoverImageStore, CoverImageStore>();
builder.Services.AddScoped<CoverImageGarbageCollector>();

var app = builder.Build();

// Honour X-Forwarded-* headers ONLY from trusted reverse proxies. This is the
// secure way to recover the real client address (and scheme) behind Cloudflare
// / Traefik: the middleware drops forwarded values from any address that isn't
// in KnownProxies/KnownNetworks, so an external client can't spoof a caller IP
// to defeat per-client rate limiting. Configure Collectify:ReverseProxy:
// KnownProxies (e.g. "10.0.0.0/8") for your deployment. When unset, all
// forwarded headers are ignored and RemoteIpAddress is the direct peer — the
// correct, honest behaviour for a directly-reachable install.
//
// Both configuration forms are accepted: a scalar comma-separated value
// (Collectify__ReverseProxy__KnownProxies="10.0.0.0/8,192.168.1.5") and the
// indexed-array form (…KnownProxies__0, …KnownProxies__1). The environment
// provider stores a scalar as a single entry, so we split each bound value on
// commas rather than assuming Get<string[]>() yields one item per address.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
var proxyCandidates = (builder.Configuration.GetSection("Collectify:ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
foreach (var ip in proxyCandidates)
{
    if (System.Net.IPAddress.TryParse(ip, out var address))
        forwardedOptions.KnownProxies.Add(address);
    else if (System.Net.IPNetwork.TryParse(ip, out var network)) // CIDR prefix, e.g. "10.0.0.0/8"
        forwardedOptions.KnownIPNetworks.Add(network);
}
app.UseForwardedHeaders(forwardedOptions);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();

    // The coordinator preserves PostgreSQL's provision-then-migrate lifecycle
    // and creates a verified SQLite snapshot before applying pending migrations.
    // Any provisioning, backup, verification, or migration failure stops startup.
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrationCoordinator>()
        .MigrateAsync(db, app.Lifetime.ApplicationStopping);

    // Record whether the Steam store-import tables exist. On Postgres an upgrade
    // of an existing install leaves them absent (EnsureCreated is a no-op); the
    // guard lets the Steam endpoints fail soft to "not configured" instead of
    // crashing with an undefined-table 500. Best-effort: never let a failed
    // detection take down app boot, and always allow the sweep below to try.
    var steamSchema = scope.ServiceProvider.GetRequiredService<SteamSchemaGuard>();
    try
    {
        steamSchema.MarkReady(await SteamSchemaGuard.DetectAsync(db, app.Lifetime.ApplicationStopping));
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not determine Steam schema presence; treating store import as unavailable");
        steamSchema.MarkReady(false);
    }
    // Resolve any free-text Game.Platform values that the
    // ConvertGamePlatformToEnum migration preserved in PlatformLegacy.
    // No-ops on a fresh DB or once everything's resolved.
    await GamePlatformBackfill.RunAsync(db);
    // Drop CoverImages rows no longer referenced by any owning entity.
    // Keeps the CoverImages table bounded as users re-scan / relookup
    // metadata over time.
    await scope.ServiceProvider.GetRequiredService<CoverImageGarbageCollector>().SweepAsync(db);
    // Remove expired/consumed Steam OpenID auth requests so the table doesn't
    // grow unbounded with abandoned connect attempts. Best-effort: on an
    // existing Postgres install EnsureCreated is a no-op, so the table may not
    // exist yet — never let that take down app boot for users who don't use
    // Steam. Fail soft like every other startup step.
    try
    {
        await db.SteamAuthRequests
            .Where(r => r.Consumed || r.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "Skipped SteamAuthRequest sweep (Steam auth table unavailable; store import may be incomplete).");
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapMoviesEndpoints();
app.MapMusicEndpoints();
app.MapGamesEndpoints();
app.MapTagEndpoints();
app.MapLookupEndpoints();
app.MapCoversEndpoints();
app.MapDashboardEndpoints();
app.MapHealthEndpoints();
app.MapSteamStoreEndpoints();

var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(webRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program { }
