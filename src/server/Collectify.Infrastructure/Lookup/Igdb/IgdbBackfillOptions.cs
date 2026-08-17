namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Options for the IGDB backfill background service. Bound from the
/// "Collectify:IgdbBackfill" config section. Validated at startup via
/// <see cref="Microsoft.Extensions.DependencyInjection.OptionsBuilderExtensions.ValidateOnStart"/>
/// so a bad env var surfaces as a startup error rather than a timer that
/// silently throws mid-loop.
/// </summary>
public sealed class IgdbBackfillOptions
{
    public const string SectionName = "Collectify:IgdbBackfill";

    /// <summary>Master off-switch. When false the hosted service never starts.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Delay before the first sweep and between consecutive sweeps. Must be
    /// &gt; 0 (a zero/negative here throws from PeriodicTimer at runtime).
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Delay applied between individual IGDB lookups within one sweep, so a
    /// burst of titles doesn't exceed IGDB's ~4 req/s self-hosted cap.
    /// </summary>
    public TimeSpan PacingDelay { get; set; } = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Hard ceiling on how many games a single sweep will attempt, protecting
    /// the monthly IGDB quota from a huge first run (e.g. a 500-title import).
    /// </summary>
    public int MaxGamesPerSweep { get; set; } = 100;

    /// <summary>
    /// Consecutive provider calls inside one sweep that returned no results
    /// before we assume IGDB is throttling (429 is surfaced as an empty list by
    /// IgdbGameProvider, so a storm would otherwise look like "no match" and
    /// burn the quota). Aborts the sweep once this many empties are seen.
    /// </summary>
    public int EmptyResultAbortThreshold { get; set; } = 10;
}
