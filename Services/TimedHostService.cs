using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Services;   // IZoomMeetingService

/// <summary>
/// Runs every 5-minute block **one minute before** the block boundary,
/// i.e. hh:04, hh:09, … hh:54, hh:59.
/// Shuts down cleanly when the host stops.
/// </summary>
public sealed class TimedHostedService : BackgroundService
{
    private readonly ILogger<TimedHostedService> _logger;
    private readonly IServiceScopeFactory        _scopeFactory;

    public TimedHostedService(
        ILogger<TimedHostedService> logger,
        IServiceScopeFactory        scopeFactory)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    // ------------------------------------------------------------------
    // Entry point: kick off the scheduler and immediately return control
    // to the host; the returned Task completes only when the host stops.
    // ------------------------------------------------------------------
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TimedHostedService started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Wait until :04, :09, :14, … :59.
                TimeSpan delay = GetDelayUntilNextRun(DateTime.Now);
                if (delay < TimeSpan.Zero)
                {
                    _logger.LogWarning("Calculated negative delay {Delay}, clamping to zero", delay);
                    delay = TimeSpan.Zero;
                }
                _logger.LogInformation("Waiting {Delay} until next run. Fired at {now} v1", delay, DateTime.Now);
                await Task.Delay(delay, stoppingToken);
                //_logger.LogInformation("Waiting {Delay} until next run. Fired at {now} v2", delay, DateTime.Now);

                //await Task.Delay(delay, stoppingToken);

                // 2. Run the Zoom work.
                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown – swallow.
        }
        finally
        {
            _logger.LogInformation("TimedHostedService stopping");
        }
    }

    // ------------------------------------------------------------------
    // Work performed each tick
    // ------------------------------------------------------------------
    private async Task DoWorkAsync(CancellationToken ct)
    {
        _logger.LogInformation("Zoom slot service fired at {UtcNow}", DateTime.UtcNow);

        try
        {
            // Create a DI scope so scoped services (e.g. DbContext) work.
            using var scope   = _scopeFactory.CreateScope();
            var zoomSvc       = scope.ServiceProvider.GetRequiredService<IZoomMeetingService>();

            //await zoomSvc.AssignMeetingsAsyncFromTimer();
            await zoomSvc.CleanupMeetingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running ZoomMeetingService in background");
        }
    }

    // ------------------------------------------------------------------
    // Calculate delay until next hh:04, hh:09, … hh:59.
    // ------------------------------------------------------------------
    private static TimeSpan GetDelayUntilNextRun(DateTime now)
    {
        // 1. Find the next 5-minute boundary: 00, 05, 10, …
        var boundary = new DateTime(
            now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind)
            .AddMinutes(((now.Minute / 5) + 1) * 5);

        // 2. We want to fire one minute *before* that.
        var target = boundary.AddMinutes(-1);

        // 3. Guarantee the target is in the future; if we’re already past it,
        //    jump to the *following* boundary minus 1 minute.
        if (target <= now)
            target = boundary.AddMinutes(4);   // 5-min block – 1 min

        return target - now;   // always positive
    }

}
