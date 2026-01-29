using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Services;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Wait until :04, :09, :14, … :59.
                TimeSpan delay = GetDelayUntilNextRun(DateTime.Now);
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }
                await Task.Delay(delay, stoppingToken);

                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown – swallow.
        }
        finally
        {
            //_logger.LogInformation("TimedHostedService stopping");
        }
    }

    // ------------------------------------------------------------------
    // Work performed each tick
    // ------------------------------------------------------------------
    private async Task DoWorkAsync(CancellationToken ct)
    {
       // _logger.LogInformation("Zoom slot service fired at {UtcNow}", DateTime.UtcNow);

        try
        {
            // Create a DI scope so scoped services (e.g. DbContext) work.
            using var scope = _scopeFactory.CreateScope();
            var zoomSvc = scope.ServiceProvider.GetRequiredService<IZoomMeetingService>();
            var twilioSvc = scope.ServiceProvider.GetRequiredService<ITwilioService>();
            var emailReader  = scope.ServiceProvider.GetRequiredService<IEmailInboxReader>();
           
            await zoomSvc.CleanupMeetingsAsync();
            await emailReader.CheckInboxAsync();
            
            

            //TODO: UNCOMMENT WHEN MARKLESSONASSUFFICIENT IS FIXED.
            /*
            2025-12-03T04:14:00.1460569Z fail: TimedHostedService[0]
            2025-12-03T04:14:00.1461229Z       Error running ZoomMeetingService in background
            2025-12-03T04:14:00.1461348Z       System.NullReferenceException: Object reference not set to an instance of an object.
            2025-12-03T04:14:00.1476305Z          at DotNetCoreSqlDb.Services.ZoomMeetingService.MarkLessonAsSufficient() in /home/runner/work/msdocs-app-service-sqldb-dotnetcore/msdocs-app-service-sqldb-dotnetcore/Services/ZoomMeetingService.cs:line 308
            2025-12-03T04:14:00.1481515Z          at TimedHostedService.DoWorkAsync(CancellationToken ct) in /home/runner/work/msdocs-app-service-sqldb-dotnetcore/msdocs-app-service-sqldb-dotnetcore/Services/TimedHostService.cs:line 63
            2025-12-03T04:15:06  No new trace in the past 1 min(s).
            */
            //await zoomSvc.MarkLessonAsSufficient();
            //await twilioSvc.RemindStudents();
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running ZoomMeetingService in background");
        }
        /*try
        {
            // Create a DI scope so scoped services (e.g. DbContext) work.
            using var scope = _scopeFactory.CreateScope();
            var balanceSvc = scope.ServiceProvider.GetRequiredService<IUpdateBalanceService>();
            await balanceSvc.UpdateStudentBalanceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("UpdateStudentBalanceAsync was canceled.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running UpdateBalanceService in background");
        }*/
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
