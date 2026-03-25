using Cronos;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Api.Services;

/// <summary>
/// Background service that runs scheduled scans according to their cron expressions
/// and performs drift detection by comparing findings with the previous scan.
/// </summary>
public sealed class ScheduledScanService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScannerService _scannerService;
    private readonly IAlertingService _alertingService;
    private readonly ILogger<ScheduledScanService> _logger;

    public ScheduledScanService(
        IServiceScopeFactory scopeFactory,
        IScannerService scannerService,
        IAlertingService alertingService,
        ILogger<ScheduledScanService> logger)
    {
        _scopeFactory = scopeFactory;
        _scannerService = scannerService;
        _alertingService = alertingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledScanService started");

        // Compute NextRunAt for all enabled schedules on startup
        await InitializeNextRunAtAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndFireScansAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in scheduled scan loop");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task InitializeNextRunAtAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduledScanRepository>();
        var schedules = await repo.GetEnabledAsync(ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var schedule in schedules)
        {
            if (schedule.NextRunAt is null)
            {
                schedule.NextRunAt = ComputeNextRun(schedule.CronExpression, now);
                await repo.UpdateAsync(schedule, ct);
            }
        }
    }

    private async Task CheckAndFireScansAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduledScanRepository>();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var scanJobRepo = scope.ServiceProvider.GetRequiredService<IScanJobRepository>();

        var schedules = await repo.GetEnabledAsync(ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var schedule in schedules)
        {
            if (schedule.NextRunAt is null || schedule.NextRunAt > now)
                continue;

            _logger.LogInformation("Firing scheduled scan {Id} for device {DeviceId}", schedule.Id, schedule.DeviceId);

            try
            {
                var device = await deviceRepo.GetByIdAsync(schedule.DeviceId, ct);
                if (device is null)
                {
                    _logger.LogWarning("Device {DeviceId} not found for scheduled scan {Id}", schedule.DeviceId, schedule.Id);
                    schedule.NextRunAt = ComputeNextRun(schedule.CronExpression, now);
                    await repo.UpdateAsync(schedule, ct);
                    continue;
                }

                var job = new ScanJob
                {
                    DeviceId = schedule.DeviceId,
                    TargetIp = device.IpAddress,
                    PortRange = "1-1024",
                    MaxConcurrency = 100,
                    TimeoutMs = 3000,
                    EnableFingerprinting = true,
                    EnableCredentialTest = true,
                    EnableCveLookup = true,
                    EnableConfigAudit = true
                };

                var result = await _scannerService.StartScanAsync(job, ct);
                var previousJobId = schedule.LastScanJobId;

                schedule.LastRunAt = now;
                schedule.LastScanJobId = result.Id;
                schedule.NextRunAt = ComputeNextRun(schedule.CronExpression, now);
                await repo.UpdateAsync(schedule, ct);

                // Drift detection
                if (previousJobId is not null)
                {
                    await DetectDriftAsync(scanJobRepo, previousJobId.Value, result.Id, device, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running scheduled scan {Id}", schedule.Id);
                schedule.NextRunAt = ComputeNextRun(schedule.CronExpression, now);
                await repo.UpdateAsync(schedule, ct);
            }
        }
    }

    private async Task DetectDriftAsync(
        IScanJobRepository scanJobRepo,
        Guid previousJobId,
        Guid newJobId,
        Device device,
        CancellationToken ct)
    {
        try
        {
            var previousFindings = await scanJobRepo.GetFindingsAsync(previousJobId, ct);
            var newFindings = await scanJobRepo.GetFindingsAsync(newJobId, ct);

            var previousCritical = previousFindings.Count(f => f.Severity == ScanFindingSeverity.Critical);
            var newCritical = newFindings.Count(f => f.Severity == ScanFindingSeverity.Critical);

            if (newCritical > previousCritical)
            {
                var delta = newCritical - previousCritical;
                await _alertingService.SendAlertAsync(
                    $"New Critical Findings on {device.Label ?? device.IpAddress}",
                    $"Scheduled scan detected {delta} new Critical finding(s) on device {device.IpAddress}. " +
                    $"Previous: {previousCritical} Critical, Current: {newCritical} Critical.",
                    AlertSeverity.Critical,
                    ct);
            }
            else if (newFindings.Count > previousFindings.Count + 5)
            {
                await _alertingService.SendAlertAsync(
                    $"Finding Drift on {device.Label ?? device.IpAddress}",
                    $"Scheduled scan detected {newFindings.Count - previousFindings.Count} new findings on device {device.IpAddress}.",
                    AlertSeverity.Warning,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Drift detection failed for job {NewJobId}", newJobId);
        }
    }

    private DateTimeOffset? ComputeNextRun(string cronExpression, DateTimeOffset from)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
            var next = cron.GetNextOccurrence(from.UtcDateTime, TimeZoneInfo.Utc);
            return next.HasValue ? new DateTimeOffset(next.Value, TimeSpan.Zero) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression '{Cron}' — schedule will not fire", cronExpression);
            return null;
        }
    }
}
