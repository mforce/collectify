using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Lookup.Images;

/// <summary>
/// Materialises a remote cover-image URL into a locally stored file under
/// <c>data/covers/</c>. Returns the public URL the SPA should embed in
/// <c>&lt;img&gt;</c> tags.
///
/// The contract is forgiving by design: null / blank / already-local paths
/// pass through unchanged so call sites can wrap every Save without
/// branching, and download failures fall back to the original remote URL
/// (the browser can still load it directly) rather than blocking the save.
/// </summary>
public interface ICoverImageStore
{
    Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default);
}

public sealed class CoverImageStore : ICoverImageStore
{
    public const string HttpClientName = "covers";

    private readonly string _coversDir;
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<CoverImageStore> _log;

    public CoverImageStore(string coversDir, IHttpClientFactory factory, ILogger<CoverImageStore> log)
    {
        _coversDir = coversDir;
        _factory = factory;
        _log = log;
    }

    public async Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        if (!IsRemoteUrl(imagePath)) return imagePath;

        var (hash, ext) = HashAndExtension(imagePath);
        var filename = hash + ext;
        var localPath = Path.Combine(_coversDir, filename);
        var publicUrl = $"/covers/{filename}";

        if (File.Exists(localPath)) return publicUrl;

        try
        {
            var http = _factory.CreateClient(HttpClientName);
            var bytes = await http.GetByteArrayAsync(imagePath, ct);
            Directory.CreateDirectory(_coversDir);
            // Atomic write so a crash mid-download never leaves a half-written
            // file at the canonical path.
            var temp = localPath + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, localPath, overwrite: true);
            return publicUrl;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to cache cover image {Url}; falling back to the remote URL", imagePath);
            return imagePath;
        }
    }

    private static bool IsRemoteUrl(string s) =>
        s.StartsWith("http://", StringComparison.Ordinal) || s.StartsWith("https://", StringComparison.Ordinal);

    private static (string hash, string ext) HashAndExtension(string url)
    {
        var sha = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var hash = Convert.ToHexString(sha)[..16].ToLowerInvariant();

        // Pick the extension from the URL's path, but only accept a small
        // safelist so a hostile poster_path can't make us write
        // /covers/abc.aspx (or anything else surprising).
        var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) ext = ".jpg";
        return (hash, ext);
    }
}
