using System.Collections.Generic;

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

    /// <summary>Max results returned by photo-snap lookup. Default 100.</summary>
    public int VisionResultLimit { get; set; } = 100;

    public TmdbOptions Tmdb { get; set; } = new();
    public MusicBrainzOptions MusicBrainz { get; set; } = new();
    public IgdbOptions Igdb { get; set; } = new();
    public UpcOptions Upc { get; set; } = new();
    public VisionOptions Vision { get; set; } = new();
    public NoiseWordsOptions NoiseWords { get; set; } = new();

    /// <summary>
    /// Resolves the effective noise-word set for a category by merging the
    /// built-in defaults (from Domain) with any config-provided extras.
    /// </summary>
    public HashSet<string> GetNoiseWordsFor(Category category)
    {
        var defaults = category switch
        {
            Category.Games => Collectify.Domain.OcrNoiseWords.Games,
            Category.Movies => Collectify.Domain.OcrNoiseWords.Movies,
            Category.Music => Collectify.Domain.OcrNoiseWords.Music,
            _ => throw new System.ArgumentOutOfRangeException(nameof(category)),
        };

        var extras = category switch
        {
            Category.Games => NoiseWords.GamesExtra,
            Category.Movies => NoiseWords.MoviesExtra,
            Category.Music => NoiseWords.MusicExtra,
            _ => throw new System.ArgumentOutOfRangeException(nameof(category)),
        };

        var merged = new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var word in extras)
            merged.Add(word);
        return merged;
    }

    public enum Category
    {
        Games,
        Movies,
        Music,
    }
}

/// <summary>
/// Extra noise words that extend the built-in sets. Bound from config
/// (Collectify:Metadata:NoiseWords:GamesExtra, etc.) as comma-separated
/// lists. Empty by default — the Domain-level built-ins cover the common cases.
/// </summary>
public sealed class NoiseWordsOptions
{
    /// <summary>Comma-separated extra words for game cover OCR filtering.</summary>
    public string? GamesExtraRaw { get; set; }
    public HashSet<string> GamesExtra => ParseExtra(GamesExtraRaw);

    /// <summary>Comma-separated extra words for movie cover OCR filtering.</summary>
    public string? MoviesExtraRaw { get; set; }
    public HashSet<string> MoviesExtra => ParseExtra(MoviesExtraRaw);

    /// <summary>Comma-separated extra words for album cover OCR filtering.</summary>
    public string? MusicExtraRaw { get; set; }
    public HashSet<string> MusicExtra => ParseExtra(MusicExtraRaw);

    private static HashSet<string> ParseExtra(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
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
