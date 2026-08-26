using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Store;

public static class SteamStoreServiceCollectionExtensions
{
    public const string PublicBaseUrlKey = "Collectify:PublicBaseUrl";
    /// <summary>
    /// Kept for documentation/tests only — no longer the runtime fallback. The
    /// public base is derived from the live request origin (or the configured
    /// <see cref="PublicBaseUrlKey"/>) so Steam callbacks resolve to the
    /// externally visible host, never a development-only localhost.
    /// </summary>
    public const string DefaultPublicBaseUrl = "http://localhost:5173";

    /// <summary>
    /// Register the Steam store-import feature: options, typed Steam HTTP
    /// client, OpenID verifier, and the import service. Steam is fail-soft —
    /// with no API key the endpoints report configured=false and the UI shows
    /// a hint instead of wiring the flow.
    /// </summary>
    public static IServiceCollection AddSteamStoreImport(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SteamOptions>()
            .Bind(config.GetSection(SteamOptions.SectionName))
            .Validate(
                options => options.Steam.CacheTtl > TimeSpan.Zero,
                "Collectify:Platforms:Steam:CacheTtl must be greater than zero.")
            .Validate(
                options => options.Steam.ImportCap > 0,
                "Collectify:Platforms:Steam:ImportCap must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient(SteamClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SteamOptions>>().Value;
            client.BaseAddress = new Uri(opts.Steam.ApiBaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHttpClient(SteamOpenIdVerifier.HttpClientName);

        services.AddScoped<ISteamClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new SteamClient(
                factory.CreateClient(SteamClient.HttpClientName),
                sp.GetRequiredService<IOptions<SteamOptions>>(),
                sp.GetRequiredService<Collectify.Infrastructure.Lookup.ILookupCache>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SteamClient>>());
        });

        services.AddScoped<ISteamOpenIdVerifier>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new SteamOpenIdVerifier(
                factory.CreateClient(SteamOpenIdVerifier.HttpClientName),
                sp.GetRequiredService<IOptions<SteamOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SteamOpenIdVerifier>>());
        });

        services.AddScoped<SteamStoreImportService>();

        // Singleton flag set at startup: whether the Steam store tables exist in
        // the current DB (always true on SQLite; optionally false on an upgraded
        // existing Postgres install). Endpoints fail soft to "not configured"
        // when false instead of crashing with an undefined-table 500.
        services.AddSingleton<SteamSchemaGuard>();

        return services;
    }

    /// <summary>
    /// Resolve the public base URL for OpenID return_to / SPA redirects.
    ///
    /// Precedence:
    ///   1. An explicitly-configured <see cref="PublicBaseUrlKey"/>, which wins
    ///      for reverse-proxy / deployed setups.
    ///   2. <paramref name="requestBase"/> — the externally visible request
    ///      origin derived by the caller (Api layer) from the live request
    ///      (honouring X-Forwarded-Proto/Host) when no override is set. This is
    ///      the key fix: an unconfigured install no longer falls back to a
    ///      development-only localhost, so Steam callbacks resolve to the real
    ///      host (e.g. the container behind the proxy) instead of the user's
    ///      own machine.
    ///   3. null — fail-soft: callers report configured=false rather than
    ///      emitting a bogus redirect.
    /// </summary>
    public static string? ResolvePublicBaseUrl(IConfiguration config, string? requestBase = null)
    {
        var configured = config[PublicBaseUrlKey];
        if (!string.IsNullOrWhiteSpace(configured))
            return NormalizeBase(configured);
        if (!string.IsNullOrWhiteSpace(requestBase))
            return NormalizeBase(requestBase);
        return null;
    }

    private static string? NormalizeBase(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return null;
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
