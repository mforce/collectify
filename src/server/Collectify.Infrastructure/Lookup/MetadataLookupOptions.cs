namespace Collectify.Infrastructure.Lookup;

/// <summary>
/// Bound from the "Collectify:Metadata" config section. Each provider gets
/// its own subsection so we can ship empty defaults and let users opt in by
/// setting an env var (Collectify__Metadata__Tmdb__ApiKey, etc.).
/// </summary>
public sealed class MetadataLookupOptions
{
    public const string SectionName = "Collectify:Metadata";

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromDays(30);

    public TmdbOptions Tmdb { get; set; } = new();
    public MusicBrainzOptions MusicBrainz { get; set; } = new();
    public IgdbOptions Igdb { get; set; } = new();
    public UpcOptions Upc { get; set; } = new();
    public VisionOptions Vision { get; set; } = new();
}

public sealed class VisionOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://vision.googleapis.com/v1/";
}

public sealed class TmdbOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";
    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p/w342";
}

public sealed class MusicBrainzOptions
{
    /// <summary>
    /// MusicBrainz requires a contact-bearing User-Agent. No key, but the UA
    /// is the rate-limit identity, so unset = skip the provider.
    /// </summary>
    public string? UserAgent { get; set; }
    public string BaseUrl { get; set; } = "https://musicbrainz.org/ws/2/";
}

public sealed class IgdbOptions
{
    public string? TwitchClientId { get; set; }
    public string? TwitchClientSecret { get; set; }
    public string BaseUrl { get; set; } = "https://api.igdb.com/v4/";
}

/// <summary>
/// Bound from "Collectify:Metadata:Upc". The trial endpoint has no key
/// (it's IP rate-limited), so the only configurable bit is the BaseUrl
/// for tests / future paid-tier swaps.
/// </summary>
public sealed class UpcOptions
{
    public string BaseUrl { get; set; } = "https://api.upcitemdb.com/";
}
