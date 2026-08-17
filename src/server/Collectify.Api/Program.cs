using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Collectify.Api.Endpoints;
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
    // Partition keyed on the effective client address. By default that's the
    // direct peer (Connection.RemoteIpAddress) — the honest, unspoofable signal,
    // correct for a directly-reachable install. When Collectify sits behind a
    // reverse proxy / TLS terminator, every client shares the proxy's peer IP,
    // which would collapse per-client limiting back to one global bucket. To
    // restore real per-client limiting in that deployment, set
    // Collectify:Platforms:Steam:TrustForwardedIp=true so the partition key is
    // taken from X-Forwarded-For (the first, outermost proxy-appended value).
    // This is deliberately opt-in rather than default: trusting a spoofable
    // forwarded header when no trusted proxy is in front would let a direct
    // attacker mint a fresh bucket per request and bypass the limit entirely.
    var trustForwardedIp = builder.Configuration
        .GetSection(SteamOptions.SectionName).GetSection("Steam")
        .GetValue<bool?>("TrustForwardedIp") ?? false;

    rate.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rate.AddPolicy("steam-callback", httpContext =>
    {
        var ip = trustForwardedIp && httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd)
                 && fwd.Count > 0 && !string.IsNullOrWhiteSpace(fwd[0])
            ? fwd[0]!.Split(',')[0].Trim()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();

    // SQLite: migrations own the schema.
    // Postgres: shared migrations carry SQLite-specific DDL (BLOB vs bytea),
    // so we use EnsureCreated() which builds the schema from the current model.
    // For a self-hosted app this is fine — schema evolution requires a DB reset.
    var provider = builder.Configuration["Collectify:Database:Provider"]
        ?? Collectify.Infrastructure.DatabaseOptions.DefaultProvider;
    if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
    {
        await CollectifyDbContextExtensions.EnsurePostgresDatabaseAsync(builder.Configuration);
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

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
            "Skipped SteamAuthRequest sweep (table missing on existing Postgres; a DB reset is required to enable store import).");
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
