using System.Collections.Specialized;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup.Igdb;
using Collectify.Infrastructure.Store;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Collectify.Api.Endpoints;

public static class SteamStoreEndpoints
{
    private const string SpaImportPath = "/import/steam";
    private const string SteamAuthCookie = "collectify.steam.state";
    private const int MaxPageSize = 100;
    private static readonly TimeSpan AuthRequestLifetime = TimeSpan.FromMinutes(10);

    public record SteamConnectDto(bool Configured, string? RedirectUrl);
    public record SteamConnectionDto(bool Connected, string? SteamId, string? PersonaName);
    public record SteamOwnedTitleDto(string ExternalGameId, string Title, long PlaytimeMinutes, string? IconUrl, string? LogoUrl, string State);
    public record SteamPreviewDto(string Status, SteamOwnedTitleDto[] Titles, bool Truncated, int Total, int ImportCap);
    public record SteamImportRequest(string[]? ExternalGameIds);
    public record SteamImportItemDto(string ExternalGameId, bool Imported, bool AlreadyImported);
    public record SteamImportResultDto(int Imported, int AlreadyImported, SteamImportItemDto[] Items);

    public static IEndpointRouteBuilder MapSteamStoreEndpoints(this IEndpointRouteBuilder app)
    {
        // ------------------------------------------------------------------
        // OpenID callback — public, OUTSIDE RequireAuthorization. GET that
        // mutates state, safe only because it runs under a fully-verified
        // OpenID assertion + one-time (state, cookie) pair. Never takes an
        // OwnerId from the request.
        // ------------------------------------------------------------------
        app.MapGet("/api/accounts/steam/callback", async (
            HttpContext ctx,
            ISteamOpenIdVerifier verifier,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            Microsoft.Extensions.Configuration.IConfiguration config,
            CancellationToken ct) =>
        {
            var publicBase = SteamStoreServiceCollectionExtensions.ResolvePublicBaseUrl(config, RequestOrigin(ctx.Request));
            if (publicBase is null || !verifier.IsConfigured || !schemaGuard.IsSchemaReady)
                return Results.Redirect($"{SpaImportPath}?steam=error");

            // Collect every openid.* param for verification/echo.
            var openId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, v) in ctx.Request.Query)
                if (k.StartsWith("openid.", StringComparison.Ordinal) && v.Count > 0)
                    openId[k] = v[0]!;

            // The state rides inside openid.return_to; rebuild the exact
            // expected return_to (byte-for-byte) for OpenID verification.
            openId.TryGetValue("openid.return_to", out var returnTo);
            var state = ExtractState(returnTo);
            if (state is null)
                return Results.Redirect($"{SpaImportPath}?steam=error");

            // Second factor: browser-bound cookie half (HttpOnly, set on
            // /connect). Checked locally BEFORE any outbound Steam call so a
            // random request can't trigger a Steam round trip (amplification).
            var cookieHalf = ctx.Request.Cookies[SteamAuthCookie];
            if (cookieHalf is null || !await service.HasValidAuthRequestAsync(state, cookieHalf, ct))
                return Results.Redirect($"{SpaImportPath}?steam=error");

            var expectedReturnTo = $"{publicBase}/api/accounts/steam/callback?state={Uri.EscapeDataString(state)}";
            var steamId = await verifier.VerifyAsync(openId, expectedReturnTo, ct);
            if (steamId is null)
                return Results.Redirect($"{SpaImportPath}?steam=error");

            // Best-effort persona lookup (outbound) before the atomic link.
            var persona = await service.GetPersonaNameAsync(steamId, ct);

            // Consume state + upsert connection atomically (single transaction).
            var connection = await service.CompleteConnectAtomicAsync(state, cookieHalf, steamId, persona, ct);
            if (connection is null)
                return Results.Redirect($"{SpaImportPath}?steam=error");

            ctx.Response.Cookies.Delete(SteamAuthCookie);
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Redirect($"{SpaImportPath}?steam=connected");
        }).RequireRateLimiting("steam-callback");

        // ------------------------------------------------------------------
        // Authenticated group.
        // ------------------------------------------------------------------
        var group = app.MapGroup("/api/accounts/steam").RequireAuthorization();

        group.MapPost("/connect", async (
            HttpContext ctx,
            UserManager<AppUser> users,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            ISteamOpenIdVerifier verifier,
            Microsoft.Extensions.Configuration.IConfiguration config,
            CancellationToken ct) =>
        {
            var publicBase = SteamStoreServiceCollectionExtensions.ResolvePublicBaseUrl(config, RequestOrigin(ctx.Request));
            if (publicBase is null || !verifier.IsConfigured || !schemaGuard.IsSchemaReady)
                return Results.Ok(new SteamConnectDto(false, null));

            var ownerId = users.GetUserId(ctx.User)!;
            var (state, cookieHalf) = await service.CreateAuthRequestAsync(ownerId, AuthRequestLifetime, ct);

            ctx.Response.Cookies.Append(SteamAuthCookie, cookieHalf, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                // Never send over cleartext, but don't hard-require https: the
                // repo's own auth cookie deliberately omits Secure so plain-HTTP
                // self-hosted installs work. Derive from the request instead.
                Secure = ctx.Request.IsHttps,
                MaxAge = AuthRequestLifetime,
            });

            var returnTo = $"{publicBase}/api/accounts/steam/callback?state={Uri.EscapeDataString(state)}";
            var redirectUrl = BuildOpenIdRedirect(returnTo, verifier);
            return Results.Ok(new SteamConnectDto(true, redirectUrl));
        });

        group.MapGet("/", async (
            HttpContext ctx,
            UserManager<AppUser> users,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            CancellationToken ct) =>
        {
            if (!schemaGuard.IsSchemaReady)
                return Results.Ok(new SteamConnectionDto(false, null, null));
            var ownerId = users.GetUserId(ctx.User)!;
            var c = await service.GetConnectionAsync(ownerId, ct);
            return Results.Ok(new SteamConnectionDto(c is not null, c?.ExternalAccountId, c?.ExternalDisplayName));
        });

        group.MapGet("/games", async (
            HttpContext ctx,
            string? q,
            int? offset,
            int? limit,
            bool? hideImported,
            UserManager<AppUser> users,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            IOptions<SteamOptions> options,
            CancellationToken ct) =>
        {
            if (!schemaGuard.IsSchemaReady)
                return Results.Ok(new SteamPreviewDto("notconnected", [], false, 0, options.Value.Steam.ImportCap));
            var effOffset = offset ?? 0;
            var effLimit = limit ?? MaxPageSize;
            if (effOffset < 0 || effLimit < 1 || effLimit > MaxPageSize)
                return Results.BadRequest(new
                {
                    error = "Invalid pagination.",
                    offset = effOffset,
                    limit = effLimit,
                    maxLimit = MaxPageSize,
                });
            var ownerId = users.GetUserId(ctx.User)!;
            var preview = await service.GetOwnedTitlesAsync(
                ownerId,
                search: q,
                offset: effOffset,
                limit: effLimit,
                hideImported: hideImported ?? false,
                ct: ct);
            return Results.Ok(new SteamPreviewDto(
                preview.Status.ToString().ToLowerInvariant(),
                preview.Titles
                    .Select(t => new SteamOwnedTitleDto(
                        t.ExternalGameId, t.Title, t.PlaytimeMinutes, t.IconUrl, t.LogoUrl,
                        t.State == SteamTitleImportState.Imported ? "imported" : "importable"))
                    .ToArray(),
                preview.Truncated,
                preview.Total,
                options.Value.Steam.ImportCap));
        });

        group.MapPost("/import", async (
            [FromBody] SteamImportRequest req,
            HttpContext ctx,
            UserManager<AppUser> users,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            IgdbBackfillRunner backfill,
            Microsoft.Extensions.Options.IOptions<SteamOptions> options,
            CancellationToken ct) =>
        {
            // Schema missing (upgraded existing Postgres install): fail soft with
            // an empty result rather than crash on an undefined table.
            if (!schemaGuard.IsSchemaReady)
                return Results.Ok(new SteamImportResultDto(0, 0, []));

            // Reject oversized selections up front (400) rather than quietly
            // processing a capped prefix — "you picked too many to import".
            var distinct = req?.ExternalGameIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct().Count() ?? 0;
            if (distinct == 0)
                return Results.BadRequest(new { error = "No games selected to import." });
            if (distinct > options.Value.Steam.ImportCap)
                return Results.BadRequest(new
                {
                    error = "Too many games selected.",
                    cap = options.Value.Steam.ImportCap,
                    submitted = distinct,
                });

            var ownerId = users.GetUserId(ctx.User)!;
            // req is guaranteed non-null here: the guards above returned a 400
            // unless ExternalGameIds had at least one non-whitespace item.
            var gameIds = req?.ExternalGameIds ?? [];
            var (results, createdGames) = await service.ImportAsync(ownerId, gameIds, ct);

            // Imported games show up empty until the backfill sweep runs (which,
            // even with the startup sweep, is only on the NEXT boot or a timer
            // tick). Backfill the newly-created games IMMEDIATELY so metadata
            // appears right after import rather than the user staring at empty
            // fields. IgdbBackfillRunner is a no-op when IGDB/Twitch is
            // unconfigured, and its fill-only merge never overwrites the Steam
            // import's own data. Bounded by pacing (350ms/lookup) so a huge
            // import doesn't blow IGDB's rate cap mid-request. Fail-soft: the
            // runner logs and isolates per-game failures internally, so a metadata
            // hiccup must never turn a successful import into an error response —
            // the background sweep will retry anything left empty later.
            var createdIds = createdGames.Select(g => g.Id).ToArray();
            if (createdIds.Length > 0)
            {
                try
                {
                    await backfill.BackfillGamesAsync(createdIds, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Never fail the import over optional metadata enrichment.
                }
            }

            return Results.Ok(new SteamImportResultDto(
                results.Count(r => r.Imported),
                results.Count(r => r.AlreadyImported),
                results.Select(r => new SteamImportItemDto(r.ExternalGameId, r.Imported, r.AlreadyImported)).ToArray()));
        });

        group.MapDelete("/", async (
            HttpContext ctx,
            UserManager<AppUser> users,
            SteamSchemaGuard schemaGuard,
            SteamStoreImportService service,
            CancellationToken ct) =>
        {
            if (!schemaGuard.IsSchemaReady) return Results.NoContent();
            var ownerId = users.GetUserId(ctx.User)!;
            await service.DisconnectAsync(ownerId, ct);
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>Extracts the ?state= query param from a return_to URL.</summary>
    internal static string? ExtractState(string? returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo)) return null;
        if (!Uri.TryCreate(returnTo, UriKind.Absolute, out var uri)) return null;
        var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var state = q["state"];
        return string.IsNullOrWhiteSpace(state) ? null : state;
    }

    /// <summary>Builds the Steam OpenID 2.0 checkid_setup login URL.</summary>
    internal static string BuildOpenIdRedirect(string returnTo, ISteamOpenIdVerifier verifier)
    {
        var realm = returnTo.Substring(0, returnTo.IndexOf("/api/accounts/steam/callback", StringComparison.Ordinal));
        // OpenID 2.0 checkid_setup: identity/claimed_id use the special
        // identifier_select sentinel so Steam chooses the account. Sending a
        // bare claimed prefix here is invalid per the spec and rejected by
        // Steam.
        const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";
        var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);
        qs["openid.ns"] = "http://specs.openid.net/auth/2.0";
        qs["openid.mode"] = "checkid_setup";
        qs["openid.return_to"] = returnTo;
        qs["openid.realm"] = realm;
        qs["openid.identity"] = IdentifierSelect;
        qs["openid.claimed_id"] = IdentifierSelect;
        return $"https://steamcommunity.com/openid/login?{qs}";
    }

    /// <summary>
    /// Derives the externally visible origin (scheme://host) of the current
    /// request for OpenID return_to / SPA redirects. Honours the standard
    /// X-Forwarded-Proto / X-Forwarded-Host headers added by a reverse proxy
    /// (the container serves plain HTTP on a private port behind Traefik/CF), so
    /// an unconfigured <c>Collectify:PublicBaseUrl</c> still resolves to the
    /// real public host instead of the Vite dev server. Only the first (outermost)
    /// forwarded value is trusted, which is how well-behaved proxies append.
    /// </summary>
    internal static string? RequestOrigin(HttpRequest request)
    {
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                     ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault()
                   ?? request.Host.Value;
        if (string.IsNullOrWhiteSpace(host)) return null;
        return $"{scheme}://{host}";
    }
}
