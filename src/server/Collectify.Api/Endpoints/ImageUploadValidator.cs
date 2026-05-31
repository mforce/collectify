using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Collectify.Api.Endpoints;

/// <summary>Result of image upload validation: either bytes or an error.</summary>
public readonly struct ImageUploadResult
{
    public byte[]? Bytes { get; }
    public IResult? Error { get; }

    public static ImageUploadResult Success(byte[] bytes) => new(bytes, null);
    public static ImageUploadResult Failure(IResult result) => new(null, result);

    private ImageUploadResult(byte[]? bytes, IResult? error)
    {
        Bytes = bytes;
        Error = error;
    }
}

/// <summary>
/// Shared image upload validation used by both CoversEndpoints and the
/// by-image vision lookup routes. Validates content-type, file size, and
/// magic bytes.
/// </summary>
public static class ImageUploadValidator
{
    // 5 MiB upload cap.
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    /// <summary>
    /// Validates the form file and returns the raw bytes on success,
    /// or an IResult error on failure.
    /// </summary>
    public static async Task<ImageUploadResult> ValidateAndReadAsync(
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ImageUploadResult.Failure(Results.BadRequest(new { error = "A non-empty file is required." }));
        if (file.Length > MaxUploadBytes)
            return ImageUploadResult.Failure(Results.StatusCode(StatusCodes.Status413PayloadTooLarge));
        if (file.ContentType is null || !AllowedContentTypes.Contains(file.ContentType))
            return ImageUploadResult.Failure(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        if (!MagicBytesMatch(bytes, file.ContentType))
            return ImageUploadResult.Failure(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));

        return ImageUploadResult.Success(bytes);
    }

    /// <summary>
    /// Confirms the leading bytes of an upload actually look like the
    /// declared Content-Type. Returns true for content-types we don't
    /// sniff (defensive default for whitelist entries without a stable
    /// header signature).
    /// </summary>
    public static bool MagicBytesMatch(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4) return false;
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" when bytes.Length >= 8 =>
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            "image/webp" when bytes.Length >= 12 =>
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => true,
        };
    }
}
