using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Store;

public static class SteamStoreServiceCollectionExtensions
{
    public const string PublicBaseUrlKey = "Collectify:PublicBaseUrl";
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
            .Bind(config.GetSection(SteamOptions.SectionName));

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

        return services;
    }

    /// <summary>
    /// Resolve the public base URL for OpenID return_to / SPA redirects.
    /// Fail-soft: on an invalid/absent value returns null so callers report
    /// configured=false rather than crashing app boot.
    /// </summary>
    public static string? ResolvePublicBaseUrl(IConfiguration config)
    {
        var raw = config[PublicBaseUrlKey] ?? DefaultPublicBaseUrl;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return null;
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
