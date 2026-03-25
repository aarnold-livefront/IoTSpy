using System.Collections.Concurrent;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public class ScannerService(
    IServiceScopeFactory scopeFactory,
    PortScanner portScanner,
    ServiceFingerprinter fingerprinter,
    CredentialTester credentialTester,
    CveLookupService cveLookup,
    ConfigAuditor configAuditor,
    ILogger<ScannerService> logger) : IScannerService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningScans = new();

    public async Task<ScanJob> StartScanAsync(ScanJob job, CancellationToken ct = default)
    {
        // Persist the job
        using (var scope = scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IScanJobRepository>();
            await repo.AddAsync(job, ct);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _runningScans[job.Id] = cts;

        // Run scan in background
        _ = Task.Run(() => ExecuteScanAsync(job.Id, cts.Token), cts.Token);

        return job;
    }

    public Task CancelScanAsync(Guid scanJobId)
    {
        if (_runningScans.TryRemove(scanJobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        return Task.CompletedTask;
    }

    public bool IsScanRunning(Guid scanJobId) =>
        _runningScans.ContainsKey(scanJobId);

    private async Task ExecuteScanAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScanJobRepository>();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var job = await repo.GetByIdAsync(jobId, ct);
            if (job is null) return;

            job.Status = ScanStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
            await repo.UpdateAsync(job, ct);

            logger.LogInformation("Starting scan {JobId} on {Target}", jobId, job.TargetIp);

            // 3.1 — Port scan
            var openPorts = await portScanner.ScanAsync(
                job.TargetIp, job.PortRange, job.MaxConcurrency, job.TimeoutMs, ct);

            foreach (var finding in openPorts)
                finding.ScanJobId = jobId;
            await repo.AddFindingsAsync(openPorts, ct);

            // 3.2 — Service fingerprinting
            List<ScanFinding> fingerprints = [];
            if (job.EnableFingerprinting && openPorts.Count > 0)
            {
                fingerprints = await fingerprinter.FingerprintAsync(
                    job.TargetIp, openPorts, job.TimeoutMs, ct);

                foreach (var finding in fingerprints)
                    finding.ScanJobId = jobId;
                await repo.AddFindingsAsync(fingerprints, ct);
            }

            // 3.3 — Default credential testing
            if (job.EnableCredentialTest && openPorts.Count > 0)
            {
                var credFindings = await credentialTester.TestAsync(
                    job.TargetIp, openPorts, job.TimeoutMs, ct);

                foreach (var finding in credFindings)
                    finding.ScanJobId = jobId;
                await repo.AddFindingsAsync(credFindings, ct);
            }

            // 3.4 — CVE lookup
            if (job.EnableCveLookup && fingerprints.Count > 0)
            {
                var cveFindings = await cveLookup.LookupAsync(fingerprints, ct);
                foreach (var finding in cveFindings)
                    finding.ScanJobId = jobId;
                await repo.AddFindingsAsync(cveFindings, ct);
            }

            // 3.5 — Config audit
            if (job.EnableConfigAudit && openPorts.Count > 0)
            {
                var configFindings = await configAuditor.AuditAsync(
                    job.TargetIp, openPorts, job.TimeoutMs, ct);

                foreach (var finding in configFindings)
                    finding.ScanJobId = jobId;
                await repo.AddFindingsAsync(configFindings, ct);
            }

            // Complete the job
            job = await repo.GetByIdAsync(jobId, ct);
            if (job is null) return;

            var allFindings = await repo.GetFindingsAsync(jobId, ct);
            job.TotalFindings = allFindings.Count;
            job.Status = ScanStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await repo.UpdateAsync(job, ct);

            // Update device security score
            await UpdateSecurityScoreAsync(job, allFindings, deviceRepo, ct);

            logger.LogInformation("Scan {JobId} completed: {Count} findings", jobId, job.TotalFindings);
        }
        catch (OperationCanceledException)
        {
            await SetJobStatusAsync(jobId, ScanStatus.Cancelled);
            logger.LogInformation("Scan {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan {JobId} failed", jobId);
            await SetJobStatusAsync(jobId, ScanStatus.Failed, ex.Message);
        }
        finally
        {
            if (_runningScans.TryRemove(jobId, out var cts))
                cts.Dispose();
        }
    }

    private async Task SetJobStatusAsync(Guid jobId, ScanStatus status, string? error = null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScanJobRepository>();
            var job = await repo.GetByIdAsync(jobId);
            if (job is null) return;

            job.Status = status;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = error;
            await repo.UpdateAsync(job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update scan job {JobId} status to {Status}", jobId, status);
        }
    }

    private static async Task UpdateSecurityScoreAsync(
        ScanJob job, List<ScanFinding> findings, IDeviceRepository deviceRepo, CancellationToken ct)
    {
        var device = await deviceRepo.GetByIdAsync(job.DeviceId, ct);
        if (device is null) return;

        // Score: start at 100, deduct per finding severity
        var score = 100;
        foreach (var finding in findings)
        {
            score -= finding.Severity switch
            {
                ScanFindingSeverity.Critical => 25,
                ScanFindingSeverity.High => 15,
                ScanFindingSeverity.Medium => 10,
                ScanFindingSeverity.Low => 5,
                _ => 0
            };
        }

        device.SecurityScore = Math.Max(0, score);
        await deviceRepo.UpdateAsync(device, ct);
    }
}
