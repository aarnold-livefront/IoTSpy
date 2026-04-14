using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
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
        var oldestCapture = await db.Captures.AnyAsync(ct)
            ? await db.Captures.MinAsync(c => (DateTimeOffset?)c.Timestamp, ct)
            : null;
        var oldestPacket = await db.Packets.AnyAsync(ct)
            ? await db.Packets.MinAsync(p => (DateTimeOffset?)p.Timestamp, ct)
            : null;

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

        var toDelete = await query.ToListAsync(ct);
        db.Captures.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgeCaptures",
            EntityType = "CapturedRequest",
            Details = $"Purged {toDelete.Count} captures",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        }, ct);

        return Ok(new { deleted = toDelete.Count });
    }

    [HttpDelete("packets")]
    public async Task<IActionResult> PurgePackets(
        [FromQuery] int? olderThanDays,
        [FromQuery] bool purgeAll = false,
        CancellationToken ct = default)
    {
        if (!purgeAll && !olderThanDays.HasValue)
            return BadRequest(new { error = "Specify olderThanDays, or use purgeAll=true" });

        var query = db.Packets.AsQueryable();
        if (!purgeAll && olderThanDays.HasValue)
            query = query.Where(p => p.Timestamp < DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value));

        var toDelete = await query.ToListAsync(ct);
        db.Packets.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgePackets",
            EntityType = "CapturedPacket",
            Details = $"Purged {toDelete.Count} packets",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        }, ct);

        return Ok(new { deleted = toDelete.Count });
    }

    [HttpGet("export/logs")]
    public async Task<IActionResult> ExportLogs([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var captures = await db.Captures.AsNoTracking()
            .Include(c => c.Device)
            .OrderBy(c => c.Timestamp)
            .ToListAsync(ct);

        if (format == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Method,Host,Path,StatusCode,RequestSize,ResponseSize,Device");
            foreach (var c in captures)
                csv.AppendLine($"{c.Timestamp:O},{Csv(c.Method)},{Csv(c.Host)},{Csv(c.Path)},{c.StatusCode},{c.RequestBodySize},{c.ResponseBodySize},{Csv(c.Device?.Label ?? c.Device?.Hostname ?? "")}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "captures.csv");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(captures,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "captures.json");
    }

    [HttpGet("export/packets")]
    public async Task<IActionResult> ExportPackets([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var packets = await db.Packets.AsNoTracking()
            .OrderBy(p => p.Timestamp)
            .ToListAsync(ct);

        if (format == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Protocol,SourceIp,DestinationIp,SourcePort,DestinationPort,Length");
            foreach (var p in packets)
                csv.AppendLine($"{p.Timestamp:O},{Csv(p.Protocol)},{Csv(p.SourceIp)},{Csv(p.DestinationIp)},{p.SourcePort},{p.DestinationPort},{p.Length}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "packets.csv");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(packets,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "packets.json");
    }

    [HttpGet("export/config")]
    public async Task<IActionResult> ExportConfig(CancellationToken ct = default)
    {
        var config = new
        {
            manipulationRules = await db.ManipulationRules.AsNoTracking().ToListAsync(ct),
            breakpoints = await db.Breakpoints.AsNoTracking().ToListAsync(ct),
            fuzzerJobs = await db.FuzzerJobs.AsNoTracking().ToListAsync(ct),
            scheduledScans = await db.ScheduledScans.AsNoTracking().ToListAsync(ct),
            openRtbPolicies = await db.OpenRtbPiiPolicies.AsNoTracking().ToListAsync(ct),
            apiSpecDocuments = await db.ApiSpecDocuments.AsNoTracking()
                .Include(d => d.ReplacementRules)
                .ToListAsync(ct),
            exportedAt = DateTimeOffset.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "iotspy-config.json");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
