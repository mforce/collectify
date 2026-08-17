using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Background host for the IGDB backfill sweep. Owns the timer loop and the
/// per-sweep DI scope; the actual backfill work lives in
/// <see cref="IgdbBackfillRunner"/> (resolved per sweep).
///
/// This is Collectify's first hosted service, so it establishes the convention:
/// a singleton that touches ONLY infrastructure it is told to (an
/// <see cref="IServiceScopeFactory"/>, options, time, logger) — never a scoped
/// DbContext or metadata provider in its constructor (the captive-dependency
/// footgun). Each sweep opens a fresh scope so the scoped DbContext / provider
/// / cover store / runner all share one context and are disposed together.
///
/// Lazily skips itself entirely when disabled by config or when IGDB/Twitch is
/// unconfigured, honouring the app's existing fail-soft convention (a provider
/// that isn't configured must not spin a background loop).
///
/// Fail-soft: unexpected errors are logged and the service continues on the
/// next interval rather than letting an unhandled exception stop the host.
/// Clean shutdown honours the hosted-app stopping token (including during the
/// pacing delay, which is cancellation-aware).
/// </summary>
public sealed class IgdbBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly IOptionsMonitor<IgdbBackfillOptions> _options;
    private readonly ILogger<IgdbBackfillService> _log;

    // Rotates the runner's per-sweep window (see IgdbBackfillRunner.RunSweepAsync)
    // so that games past MaxGamesPerSweep are eventually attempted even when a
    // run of low-id titles never matches. Advancing an unbounded long and letting
    // the runner wrap via `offset % pending.Count` avoids any cross-sweep bookkeeping.
    private long _sweepOffset;

    public IgdbBackfillService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IOptionsMonitor<IgdbBackfillOptions> options,
        ILogger<IgdbBackfillService> log)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_options.CurrentValue.Enabled)
            {
                _log.LogInformation("IGDB backfill disabled (Collectify:IgdbBackfill:Enabled=false); service not started");
                return;
            }

            if (!ProviderConfigured())
            {
                _log.LogInformation("IGDB backfill not started: IGDB/Twitch not configured");
                return;
            }

            await RunLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // Fail-soft: never let a background sweep take down the host.
            _log.LogError(ex, "IGDB backfill service stopped after an unexpected error");
        }
    }

    private bool ProviderConfigured()
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IGameMetadataProvider>();
        return provider.IsConfigured;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // PeriodicTimer's first tick fires after one Interval, so the first
        // sweep runs ~Interval after startup and then every Interval after.
        // (Previously an extra Task.Delay here made the first sweep wait TWO
        // intervals, needlessly delaying metadata after an import.)
        using var timer = new PeriodicTimer(_options.CurrentValue.Interval, _clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await SweepOnceAsync(ct);
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IgdbBackfillRunner>();
            // Rotate the window each sweep: pass the current offset, then advance
            // by the games ACTUALLY attempted (not the configured cap) so a
            // throttle-aborted sweep doesn't skip over its unattempted remainder
            // and permanently starve those games. The runner wraps via
            // `offset % pending.Count`, so every pending game is eventually swept.
            var result = await runner.RunSweepAsync(ct, (int)(_sweepOffset % int.MaxValue));
            _sweepOffset += result.Attempted;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutdown — propagate up through ExecuteAsync
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "IGDB backfill sweep failed; will retry next interval");
        }
    }
}
