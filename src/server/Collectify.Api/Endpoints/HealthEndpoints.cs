using System.Reflection;

namespace Collectify.Api.Endpoints;

public static class HealthEndpoints
{
    public record HealthResponse(string Status, string Version);

    // Cached so every container healthcheck doesn't re-walk the
    // assembly. The version comes from the <Version> property in
    // Collectify.Api.csproj (or whatever the release workflow injects
    // via -p:Version=).
    private static readonly string AssemblyVersion =
        typeof(HealthEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HealthEndpoints).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// Liveness probe. Intentionally anonymous and DB-free: container
    /// orchestrators (Docker HEALTHCHECK, k8s liveness, Watchtower)
    /// hit it on a tight cadence and a write-stall or migration-in-
    /// progress shouldn't flap the container.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", AssemblyVersion)));
        return app;
    }
}
