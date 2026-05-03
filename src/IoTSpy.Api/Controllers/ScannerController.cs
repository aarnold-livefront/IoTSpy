using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/scanner")]
public class ScannerController(
    IScannerService scanner,
    IScanJobRepository scanJobs,
    IDeviceRepository devices) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> StartScan([FromBody] StartScanDto dto)
    {
        var device = await devices.GetByIdAsync(dto.DeviceId);
        if (device is null) return NotFound("Device not found");

        var job = new ScanJob
        {
            DeviceId = dto.DeviceId,
            TargetIp = device.IpAddress,
            PortRange = dto.PortRange ?? "1-1024",
            MaxConcurrency = dto.MaxConcurrency ?? 100,
            TimeoutMs = dto.TimeoutMs ?? 3000,
            EnableFingerprinting = dto.EnableFingerprinting ?? true,
            EnableCredentialTest = dto.EnableCredentialTest ?? true,
            EnableCveLookup = dto.EnableCveLookup ?? true,
            EnableConfigAudit = dto.EnableConfigAudit ?? true
        };

        var result = await scanner.StartScanAsync(job);
        return Ok(result);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ScanStatus? status = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] DateTimeOffset? createdAfter = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = await scanJobs.GetAllAsync(page, pageSize, status, deviceId, createdAfter, ct);
        var total = await scanJobs.CountAsync(status, deviceId, createdAfter, ct);
        return Ok(new { items, total, page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await scanJobs.GetByIdAsync(id);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("jobs/{id:guid}/findings")]
    public async Task<IActionResult> GetFindings(Guid id)
    {
        var job = await scanJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        return Ok(await scanJobs.GetFindingsAsync(id));
    }

    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult> GetByDevice(Guid deviceId) =>
        Ok(await scanJobs.GetByDeviceIdAsync(deviceId));

    [HttpGet("jobs/{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var job = await scanJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        return Ok(new
        {
            job.Id,
            job.Status,
            job.TotalFindings,
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage,
            IsRunning = scanner.IsScanRunning(id)
        });
    }

    [HttpPost("jobs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelScan(Guid id)
    {
        var job = await scanJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        await scanner.CancelScanAsync(id);
        return Ok();
    }

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        await scanJobs.DeleteAsync(id);
        return NoContent();
    }

    [HttpDelete("jobs/bulk")]
    public async Task<IActionResult> BulkDeleteJobs([FromBody] BulkDeleteJobsDto dto, CancellationToken ct)
    {
        var deleted = await scanJobs.DeleteByFilterAsync(dto.Status, dto.CompletedBefore, ct);
        return Ok(new { deleted });
    }

    [HttpPost("jobs/cancel-all")]
    public async Task<IActionResult> CancelAllScans(CancellationToken ct)
    {
        var allJobs = await scanJobs.GetAllAsync(1, 1000, ct: ct);
        var runningIds = allJobs.Where(j => scanner.IsScanRunning(j.Id)).Select(j => j.Id).ToList();
        foreach (var id in runningIds)
            await scanner.CancelScanAsync(id);
        return Ok(new { cancelled = runningIds.Count });
    }

    [HttpGet("jobs/{id:guid}/export")]
    public async Task<IActionResult> ExportFindings(Guid id, CancellationToken ct)
    {
        var job = await scanJobs.GetByIdAsync(id, ct);
        if (job is null) return NotFound();
        var findings = await scanJobs.GetFindingsAsync(id, ct);
        var bundle = new { jobId = id, deviceId = job.DeviceId, exportedAt = DateTimeOffset.UtcNow, findings };
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", $"scan-{id}.json");
    }
}

public record StartScanDto(
    Guid DeviceId,
    string? PortRange = null,
    int? MaxConcurrency = null,
    int? TimeoutMs = null,
    bool? EnableFingerprinting = null,
    bool? EnableCredentialTest = null,
    bool? EnableCveLookup = null,
    bool? EnableConfigAudit = null
);

public record BulkDeleteJobsDto(
    ScanStatus? Status = null,
    DateTimeOffset? CompletedBefore = null
);
