using System.Globalization;
using System.Text;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// A matched IGDB candidate plus the tier of confidence behind the match.
/// </summary>
public enum MatchTier
{
    /// <summary>Normalised titles are identical.</summary>
    Exact,
}

/// <summary>
/// A matched IGDB candidate plus the tier of confidence behind the match.
/// </summary>
public sealed record BackfillMatch(GameLookupResult Result, MatchTier Tier);

/// <summary>
/// Pure, side-effect-free selection of the best IGDB candidate for a local
/// game. No DB, no HTTP — trivially table-testable.
///
/// Matching policy (mirrors issue #132's "skip-uncertain, PC-biased" rule,
/// hardened after code review):
///  - Titles are normalised before comparison: Unicode-canonicalised, so
///    composed/decomposed accents match ("Pokémon" == "Pokemon"), then
///    lowercased and stripped of whitespace + punctuation.
///  - Only an EXACT normalised equality is accepted. Prefix / partial matching
///    is deliberately excluded: "Dark Souls II" must not auto-link to
///    "Dark Souls II: Scholar of the First Sin" (a distinct re-release SKU).
///  - Among exact candidates we prefer, in order:
///      1. one whose release year is within ±1 of the local game's known year
///         (disambiguates identical titles like DOOM 1993 vs DOOM 2016), then
///      2. one whose platform maps to the local game's own platform. This makes
///         the bias PC only when the local game is PC (Steam-imported), and
///         console-aware for a manually entered Switch/PS5 game.
///  - If several exact candidates survive with no year and no platform to
///    separate them, we DECLINE rather than guess — a wrong auto-link writes
///    IGDB metadata onto the wrong game and sets IgdbId, so it would never be
///    re-checked. A miss is cheap (user resolves via the prefilled search UI).
/// </summary>
public static class IgdbBackfillPlanner
{
    /// <summary>
    /// Year tolerance when using the local game's known release year to break
    /// ties among identical titles (IGDB and Steam often differ by a year).
    /// </summary>
    internal const int YearTolerance = 1;

    /// <summary>
    /// Unicode-canonicalise (FormD) and strip combining marks, then lowercase
    /// and drop every non-alphanumeric character. FormD means composed
    /// "é" and decomposed "e" + combining acute both collapse to "e", so
    /// "Pokémon" and "Pokemon" normalise equal.
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        var sb = new StringBuilder(title.Length);
        foreach (var ch in title.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Pick the best IGDB candidate for <paramref name="game"/>, or null when
    /// no confident (exact, unambiguous) match exists — leave the game unlinked
    /// for manual resolution via the UI.
    /// </summary>
    public static BackfillMatch? BestMatch(Game game, IReadOnlyList<GameLookupResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.IsNullOrWhiteSpace(game.Title) || candidates is null || candidates.Count == 0)
            return null;

        var target = NormalizeTitle(game.Title);
        if (target.Length == 0) return null;

        // Edition variants (e.g. a local "Tomb Raider Game of the Year" vs an
        // IGDB "Tomb Raider") are the SAME release, so compare on the
        // edition-stripped base name rather than the full string. This only
        // broadens the candidate set to other editions of the same game; the
        // single-survivor gate in <see cref="Pick"/> still refuses to auto-link
        // when year/platform can't isolate exactly one candidate.
        var bareTarget = StripEdition(target);
        var matching = candidates
            .Where(c => StripEdition(NormalizeTitle(c.Title)) == bareTarget)
            .ToList();
        if (matching.Count == 0) return null;

        return Pick(matching, game);
    }

    // Trailing edition/subtitle qualifiers that denote the SAME base release.
    // Operates on the normalised (lowercased, no-space) title; each is a
    // suffix stripped once, longest-first so "gameoftheyearedition" wins over
    // "gameoftheyear" before "edition" alone.
    private static readonly string[] EditionSuffixes =
    {
        "gameoftheyearedition", "gameoftheyear", "completeedition", "goty",
        "deluxe", "definitiveedition", "edition", "remastered",
    };

    private static string StripEdition(string normalized)
    {
        foreach (var suffix in EditionSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                return normalized[..^suffix.Length];
        }
        return normalized;
    }

    private static BackfillMatch? Pick(List<GameLookupResult> exact, Game game)
    {
        var group = exact;

        // 1. Narrow by year when the local game has a known release year.
        if (game.Year is { } localYear)
        {
            var yearAligned = group
                .Where(c => c.Year is { } y && Math.Abs(y - localYear) <= YearTolerance)
                .ToList();
            if (yearAligned.Count > 0)
            {
                group = yearAligned;
            }
            else
            {
                // The local year is known but no candidate is within tolerance.
                // If any candidate exposes an explicit year that contradicts it
                // (e.g. a local 2016 DOOM vs a lone 1993 entry), decline rather
                // than fall back to the full group and permanently lock in a
                // wrong link. Only fall through when every candidate year is
                // unknown (no contradiction evidence, e.g. IGDB lacks a date).
                if (group.Any(c => c.Year is not null)) return null;
            }
        }

        // 2. Narrow by the local game's own platform (PC for Steam-imported,
        //    console for a manually entered Switch/PS5 game). Only accept when
        //    exactly one candidate survives — never blanket-pick the first of
        //    several same-platform releases (DOOM 1993 vs DOOM 2016).
        var byPlatform = group.Where(c => c.Platform == game.Platform).ToList();
        if (byPlatform.Count == 1) return new BackfillMatch(byPlatform[0], MatchTier.Exact);

        // 3. Single unambiguous candidate is the common safe case (a game whose
        //    IGDB entry has one exact title / no conflicting SKUs).
        if (group.Count == 1) return new BackfillMatch(group[0], MatchTier.Exact);

        // 4. Still ambiguous (identical titles, conflicting platform SKUs, no
        //    year to separate them) — decline rather than risk a wrong link.
        return null;
    }
}
