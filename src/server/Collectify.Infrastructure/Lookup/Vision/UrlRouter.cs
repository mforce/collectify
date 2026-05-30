namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>
/// Extracts provider IDs from trusted-domain URLs returned by
/// WEB_DETECTION. Parses uri.Host and uri.AbsolutePath segments
/// instead of regex-matching the full URL — tolerates trailing slashes,
/// query strings, and URLs without slugs.
/// </summary>
public static class UrlRouter
{
    public static string? ExtractTmdbId(Uri uri)
    {
        if (!IsHost(uri, "themoviedb.org") && !IsHost(uri, "www.themoviedb.org"))
            return null;

        // /movie/27205-inception or /movie/27205
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "movie")
            return null;

        // First path segment after /movie/ — strip trailing slug if present
        var idPart = segments[1].Split('-')[0];
        return long.TryParse(idPart, out _) ? idPart : null;
    }

    public static string? ExtractMusicBrainzReleaseId(Uri uri)
    {
        if (!IsHost(uri, "musicbrainz.org"))
            return null;

        // /release/<mbid>
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "release")
            return null;

        var mbid = segments[1];
        // Validate MBID shape: 8-4-4-4-12 hex
        if (mbid.Length != 36)
            return null;
        for (var i = 0; i < mbid.Length; i++)
        {
            var c = mbid[i];
            if (i == 8 || i == 13 || i == 18 || i == 23)
            {
                if (c != '-') return null;
            }
            else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return null;
            }
        }
        return mbid;
    }

    /// <summary>
    /// Always returns null. IGDB's API accepts only positive numeric IDs
    /// in GetByIdAsync — slugs from igdb.com URLs (e.g. "the-witcher-3")
    /// cannot be resolved without a separate slug-to-id search round-trip.
    /// Games still benefit from the OCR and web-entity paths; only the
    /// direct URL routing step is skipped.
    /// </summary>
    public static string? ExtractIgdbId(Uri uri) => null;

    [Obsolete("Use ExtractIgdbId (always null). IGDB has no slug-to-id endpoint.")]
    public static string? ExtractIgdbSlug(Uri uri) => null;

    private static bool IsHost(Uri uri, string expected)
        => uri.Host.Equals(expected, StringComparison.Ordinal);
}
