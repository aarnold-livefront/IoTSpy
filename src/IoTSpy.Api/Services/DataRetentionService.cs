using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSpy.Api.Services;

/// <summary>
/// Background service that periodically deletes records older than the configured TTL.
/// Runs at startup and then on the configured interval.
/// </summary>
public class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<DataRetentionOptions> options,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("Data retention is disabled");
            return;
        }

        logger.LogInformation(
            "Data retention enabled — captures TTL={CaptureDays}d, packets TTL={PacketDays}d, scan jobs TTL={ScanDays}d, audit archive TTL={AuditDays}d, interval={IntervalHours}h",
            opts.CaptureRetentionDays, opts.PacketRetentionDays, opts.ScanJobRetentionDays,
            opts.AuditRetentionDays, opts.RunIntervalHours);

        // Run once at startup, then on the configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionPassAsync(opts, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Data retention pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(opts.RunIntervalHours), stoppingToken);
        }
    }

    private async Task RunRetentionPassAsync(DataRetentionOptions opts, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpyDbContext>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditRepository>();

        var now = DateTimeOffset.UtcNow;
        var totalDeleted = 0;

        if (opts.CaptureRetentionDays > 0)
        {
            var cutoff = now.AddDays(-opts.CaptureRetentionDays);
            var deleted = await db.Captures
                .Where(c => c.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} captures older than {Cutoff}", deleted, cutoff);
        }

        if (opts.PacketRetentionDays > 0)
        {
            var cutoff = now.AddDays(-opts.PacketRetentionDays);
            var deleted = await db.Packets
                .Where(p => p.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} packets older than {Cutoff}", deleted, cutoff);
        }

        if (opts.ScanJobRetentionDays > 0)
        {
            var cutoff = now.AddDays(-opts.ScanJobRetentionDays);
            var deleted = await db.ScanJobs
                .Where(j => j.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} scan jobs older than {Cutoff}", deleted, cutoff);
        }

        if (opts.OpenRtbEventRetentionDays > 0)
        {
            var cutoff = now.AddDays(-opts.OpenRtbEventRetentionDays);
            var deleted = await db.OpenRtbEvents
                .Where(e => e.DetectedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} OpenRTB events older than {Cutoff}", deleted, cutoff);
        }

        if (opts.AuditRetentionDays > 0)
        {
            var cutoff = now.AddDays(-opts.AuditRetentionDays);
            var archived = await auditRepo.ArchiveOlderThanAsync(cutoff, ct);
            totalDeleted += archived;
            if (archived > 0)
                logger.LogInformation("Archived {Count} audit entries older than {Cutoff}", archived, cutoff);
        }

        if (opts.AuditArchivePurgeDays > 0)
        {
            var cutoff = now.AddDays(-opts.AuditArchivePurgeDays);
            var purged = await auditRepo.PurgeArchiveOlderThanAsync(cutoff, ct);
            totalDeleted += purged;
            if (purged > 0)
                logger.LogInformation("Purged {Count} archived audit entries older than {Cutoff}", purged, cutoff);
        }

        if (totalDeleted > 0)
            logger.LogInformation("Data retention pass complete — total {Total} records deleted/archived", totalDeleted);
        else
            logger.LogDebug("Data retention pass complete — nothing to purge");
    }
}

/// <summary>
/// Configures data retention TTLs and the cleanup schedule.
/// Bind from the "DataRetention" section in appsettings.json.
/// </summary>
public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    /// <summary>Whether data retention is active. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Delete HTTP captures older than this many days. 0 = never.</summary>
    public int CaptureRetentionDays { get; set; } = 30;

    /// <summary>Delete captured packets older than this many days. 0 = never.</summary>
    public int PacketRetentionDays { get; set; } = 7;

    /// <summary>Delete scan jobs older than this many days. 0 = never.</summary>
    public int ScanJobRetentionDays { get; set; } = 90;

    /// <summary>Delete OpenRTB events older than this many days. 0 = never.</summary>
    public int OpenRtbEventRetentionDays { get; set; } = 14;

    /// <summary>Move audit entries older than this many days to AuditArchive. 0 = never archive.</summary>
    public int AuditRetentionDays { get; set; } = 0;

    /// <summary>Hard-delete AuditArchive rows older than this many days. 0 = keep forever.</summary>
    public int AuditArchivePurgeDays { get; set; } = 0;

    /// <summary>How often to run the retention pass, in hours. Default: 24.</summary>
    public double RunIntervalHours { get; set; } = 24;
}
