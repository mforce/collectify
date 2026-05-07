using Microsoft.Extensions.Configuration;

namespace Collectify.Api.Endpoints;

public static class CoversEndpoints
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    /// <summary>
    /// Streams cached cover images out of the configured covers directory.
    /// Intentionally not auth-required so img tags work without a fetch
    /// dance; filenames are content-hashed (16 hex chars + extension) so
    /// they're not enumerable. The directory is read from configuration on
    /// each request so test hosts can override Collectify:DataDir.
    /// </summary>
    public static IEndpointRouteBuilder MapCoversEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/covers/{filename}", (string filename, IConfiguration config) =>
        {
            // Defence in depth against path traversal: the public contract
            // is a flat, hash-named filename. Reject anything that doesn't
            // match that shape before touching the filesystem.
            if (string.IsNullOrEmpty(filename)) return Results.NotFound();
            if (filename.Contains('/') || filename.Contains('\\') || filename.Contains("..")) return Results.NotFound();
            if (Path.GetFileName(filename) != filename) return Results.NotFound();

            var dataDir = config["Collectify:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
            var coversDir = Path.Combine(dataDir, "covers");
            var path = Path.Combine(coversDir, filename);
            if (!File.Exists(path)) return Results.NotFound();

            var ext = Path.GetExtension(filename);
            var contentType = ContentTypes.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";

            return Results.File(path, contentType);
        });

        return app;
    }
}
