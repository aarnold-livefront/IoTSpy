using Cronos;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduled-scans")]
public class ScheduledScanController(
    IScheduledScanRepository scheduledScans,
    IDeviceRepository devices) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await scheduledScans.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var scan = await scheduledScans.GetByIdAsync(id, ct);
        return scan is null ? NotFound() : Ok(scan);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduledScanDto dto, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(dto.DeviceId, ct);
        if (device is null) return NotFound("Device not found");

        // Validate cron expression
        try
        {
            CronExpression.Parse(dto.CronExpression, CronFormat.Standard);
        }
        catch
        {
            return BadRequest("Invalid cron expression");
        }

        var scan = new ScheduledScan
        {
            DeviceId = dto.DeviceId,
            CronExpression = dto.CronExpression,
            IsEnabled = true
        };

        // Compute next run
        try
        {
            var cron = CronExpression.Parse(dto.CronExpression, CronFormat.Standard);
            var next = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            if (next.HasValue) scan.NextRunAt = new DateTimeOffset(next.Value, TimeSpan.Zero);
        }
        catch { /* ignore */ }

        var result = await scheduledScans.AddAsync(scan, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduledScanDto dto, CancellationToken ct)
    {
        var scan = await scheduledScans.GetByIdAsync(id, ct);
        if (scan is null) return NotFound();

        if (dto.IsEnabled.HasValue) scan.IsEnabled = dto.IsEnabled.Value;

        if (!string.IsNullOrWhiteSpace(dto.CronExpression))
        {
            try
            {
                CronExpression.Parse(dto.CronExpression, CronFormat.Standard);
            }
            catch
            {
                return BadRequest("Invalid cron expression");
            }
            scan.CronExpression = dto.CronExpression;

            // Recompute next run
            try
            {
                var cron = CronExpression.Parse(dto.CronExpression, CronFormat.Standard);
                var next = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
                if (next.HasValue) scan.NextRunAt = new DateTimeOffset(next.Value, TimeSpan.Zero);
            }
            catch { /* ignore */ }
        }

        return Ok(await scheduledScans.UpdateAsync(scan, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await scheduledScans.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record CreateScheduledScanDto(Guid DeviceId, string CronExpression = "0 * * * *");

public record UpdateScheduledScanDto(bool? IsEnabled = null, string? CronExpression = null);
