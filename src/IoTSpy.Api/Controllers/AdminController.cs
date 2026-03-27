using IoTSpy.Core.Models;
using IoTSpy.Core.Interfaces;
using IoTSpy.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin")]
public class AdminController(
    IoTSpyDbContext db,
    IAuditRepository auditRepo) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var captureCount = await db.Captures.CountAsync(ct);
        var packetCount = await db.Packets.CountAsync(ct);
        var scanFindingCount = await db.ScanFindings.CountAsync(ct);
        var oldestCapture = await db.Captures.MinAsync(c => (DateTimeOffset?)c.Timestamp, ct);
        var oldestPacket = await db.Packets.MinAsync(p => (DateTimeOffset?)p.Timestamp, ct);

        return Ok(new
        {
            captures = new
            {
                count = captureCount,
                estimatedSizeBytes = captureCount * 2048L,
                oldestTimestamp = oldestCapture
            },
            packets = new
            {
                count = packetCount,
                estimatedSizeBytes = packetCount * 512L,
                oldestTimestamp = oldestPacket
            },
            scanFindings = new { count = scanFindingCount }
        });
    }

    [HttpDelete("captures")]
    public async Task<IActionResult> PurgeCaptures(
        [FromQuery] int? olderThanDays,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? host,
        [FromQuery] bool purgeAll = false,
        CancellationToken ct = default)
    {
        if (!purgeAll && !olderThanDays.HasValue && !deviceId.HasValue && string.IsNullOrEmpty(host))
            return BadRequest(new { error = "Specify at least one filter, or use purgeAll=true" });

        if (olderThanDays.HasValue && olderThanDays.Value <= 0)
            return BadRequest(new { error = "olderThanDays must be a positive integer" });

        var query = db.Captures.AsQueryable();
        if (!purgeAll)
        {
            if (olderThanDays.HasValue)
                query = query.Where(c => c.Timestamp < DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value));
            if (deviceId.HasValue)
                query = query.Where(c => c.DeviceId == deviceId);
            if (!string.IsNullOrEmpty(host))
                query = query.Where(c => c.Host == host);
        }

        var deleted = await query.ExecuteDeleteAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgeCaptures",
            EntityType = "CapturedRequest",
            Details = $"Purged {deleted} captures",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { deleted });
    }

    [HttpDelete("packets")]
    public async Task<IActionResult> PurgePackets(
        [FromQuery] int? olderThanDays,
        [FromQuery] bool purgeAll = false,
        CancellationToken ct = default)
    {
        if (!purgeAll && !olderThanDays.HasValue)
            return BadRequest(new { error = "Specify olderThanDays, or use purgeAll=true" });

        if (olderThanDays.HasValue && olderThanDays.Value <= 0)
            return BadRequest(new { error = "olderThanDays must be a positive integer" });

        var query = db.Packets.AsQueryable();
        if (!purgeAll && olderThanDays.HasValue)
            query = query.Where(p => p.Timestamp < DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value));

        var deleted = await query.ExecuteDeleteAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgePackets",
            EntityType = "CapturedPacket",
            Details = $"Purged {deleted} packets",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { deleted });
    }
}
