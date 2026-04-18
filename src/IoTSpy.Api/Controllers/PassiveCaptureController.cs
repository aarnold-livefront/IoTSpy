using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/passive")]
public class PassiveCaptureController(
    IPassiveProxyBuffer buffer,
    IPassiveCaptureSessionRepository sessionRepo) : ControllerBase
{
    // ── Buffer status & summary ──────────────────────────────────────────────

    [HttpGet("summary")]
    public IActionResult GetSummary() => Ok(buffer.GetSummary());

    [HttpGet("captures")]
    public IActionResult GetBufferedCaptures(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var snapshot = buffer.Snapshot();
        var total = snapshot.Count;
        var items = snapshot
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Ok(new { items, total, page, pageSize });
    }

    [HttpDelete("captures")]
    public IActionResult ClearBuffer()
    {
        buffer.Clear();
        return NoContent();
    }

    // ── Device filter ────────────────────────────────────────────────────────

    [HttpGet("filter")]
    public IActionResult GetFilter() =>
        Ok(new { deviceIps = buffer.DeviceFilter.ToList() });

    [HttpPut("filter")]
    public IActionResult SetFilter([FromBody] SetDeviceFilterDto dto)
    {
        if (dto.DeviceIps is null || dto.DeviceIps.Count == 0)
            buffer.ClearDeviceFilter();
        else
            buffer.SetDeviceFilter(dto.DeviceIps);
        return Ok(new { deviceIps = buffer.DeviceFilter.ToList() });
    }

    [HttpDelete("filter")]
    public IActionResult ClearFilter()
    {
        buffer.ClearDeviceFilter();
        return NoContent();
    }

    // ── Session persistence ──────────────────────────────────────────────────

    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions(CancellationToken ct) =>
        Ok(await sessionRepo.ListAsync(ct));

    [HttpPost("sessions")]
    public async Task<IActionResult> SaveSession([FromBody] SavePassiveSessionDto dto, CancellationToken ct)
    {
        var snapshot = buffer.Snapshot();
        if (snapshot.Count == 0)
            return BadRequest("No buffered captures to save.");

        // Optionally filter the snapshot to specific device IPs for this session
        IEnumerable<CapturedRequest> captures = snapshot;
        if (dto.DeviceIps is { Count: > 0 })
            captures = snapshot.Where(c => dto.DeviceIps.Contains(c.ClientIp, StringComparer.OrdinalIgnoreCase));

        var session = new PassiveCaptureSession
        {
            Name = dto.Name,
            Description = dto.Description,
            DeviceFilter = dto.DeviceIps?.Count > 0 ? string.Join(',', dto.DeviceIps) : null
        };

        var saved = await sessionRepo.SaveSessionAsync(session, captures, ct);

        if (dto.ClearBufferAfterSave)
            buffer.Clear();

        return Ok(saved);
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var session = await sessionRepo.GetAsync(id, ct);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("sessions/{id:guid}/captures")]
    public async Task<IActionResult> GetSessionCaptures(Guid id, CancellationToken ct) =>
        Ok(await sessionRepo.GetCapturesAsync(id, ct));

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken ct)
    {
        await sessionRepo.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record SetDeviceFilterDto(List<string>? DeviceIps);

public record SavePassiveSessionDto(
    string Name,
    string? Description = null,
    List<string>? DeviceIps = null,
    bool ClearBufferAfterSave = false);
