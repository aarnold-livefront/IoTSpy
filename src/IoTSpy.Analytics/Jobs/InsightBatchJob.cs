using IoTSpy.Analytics.Services;
using IoTSpy.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Analytics.Jobs;

public sealed class InsightBatchJob(
    IServiceScopeFactory scopeFactory,
    AnalyticsOptions options,
    ILogger<InsightBatchJob> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.BatchIntervalMinutes);

    // Signaled by the API controller to trigger an immediate batch run
    private readonly SemaphoreSlim _trigger = new(0, 1);

    public void TriggerNow() => _trigger.Release();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InsightBatchJob started (interval={Interval})", _interval);

        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for either the timer or a manual trigger
            var timerTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            var triggerTask = _trigger.WaitAsync(stoppingToken);
            await Task.WhenAny(timerTask, triggerTask);

            if (stoppingToken.IsCancellationRequested) break;

            await RunBatchAsync(stoppingToken);
        }
    }

    internal async Task RunBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpy.Storage.IoTSpyDbContext>();
        var insightService = scope.ServiceProvider.GetRequiredService<IInsightService>();

        // Find captures with no TrafficInsight yet
        var unscored = await db.Captures
            .Where(c => !db.TrafficInsights.Any(i => i.CaptureId == c.Id))
            .OrderByDescending(c => c.Timestamp)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        if (unscored.Count == 0)
        {
            logger.LogDebug("InsightBatchJob: no unscored captures found");
            return;
        }

        logger.LogInformation("InsightBatchJob: scoring {Count} captures", unscored.Count);
        var processed = 0;

        foreach (var capture in unscored)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await insightService.ScoreAsync(capture, ct);
                processed++;
                if (processed % 100 == 0)
                    logger.LogInformation("InsightBatchJob: {Processed}/{Total} scored", processed, unscored.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "InsightBatchJob: failed to score capture {CaptureId}", capture.Id);
            }
        }

        logger.LogInformation("InsightBatchJob: completed — {Processed} captures scored", processed);
    }
}
