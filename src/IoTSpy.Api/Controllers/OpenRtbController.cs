using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/openrtb")]
public class OpenRtbController(
    IOpenRtbEventRepository events,
    IPiiStrippingLogRepository auditLogs,
    IOpenRtbPiiPolicyRepository policies) : ControllerBase
{
    // ── Events ─────────────────────────────────────────────────────────────

    [HttpGet("events")]
    public async Task<IActionResult> ListEvents(
        [FromQuery] string? host,
        [FromQuery] OpenRtbMessageType? messageType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool? hasPii,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = new OpenRtbEventFilter(host, messageType, from, to, hasPii);
        var items = await events.GetPagedAsync(filter, page, pageSize);
        var total = await events.CountAsync(filter);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("events/{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id)
    {
        var evt = await events.GetByIdAsync(id);
        return evt is null ? NotFound() : Ok(evt);
    }

    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        await events.DeleteAsync(id);
        return NoContent();
    }

    // ── PII Policies ───────────────────────────────────────────────────────

    [HttpGet("policies")]
    public async Task<IActionResult> ListPolicies() =>
        Ok(await policies.GetAllAsync());

    [HttpGet("policies/{id:guid}")]
    public async Task<IActionResult> GetPolicy(Guid id)
    {
        var policy = await policies.GetByIdAsync(id);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy([FromBody] CreatePiiPolicyDto dto)
    {
        var policy = new OpenRtbPiiPolicy
        {
            FieldPath = dto.FieldPath,
            Strategy = dto.Strategy,
            Enabled = dto.Enabled ?? true,
            HostPattern = dto.HostPattern,
            Priority = dto.Priority ?? 0
        };

        await policies.AddAsync(policy);
        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, policy);
    }

    [HttpPut("policies/{id:guid}")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] UpdatePiiPolicyDto dto)
    {
        var policy = await policies.GetByIdAsync(id);
        if (policy is null) return NotFound();

        if (dto.FieldPath is not null) policy.FieldPath = dto.FieldPath;
        if (dto.Strategy.HasValue) policy.Strategy = dto.Strategy.Value;
        if (dto.Enabled.HasValue) policy.Enabled = dto.Enabled.Value;
        policy.HostPattern = dto.HostPattern ?? policy.HostPattern;
        if (dto.Priority.HasValue) policy.Priority = dto.Priority.Value;

        await policies.UpdateAsync(policy);
        return Ok(policy);
    }

    [HttpDelete("policies/{id:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        await policies.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("policies/reset-defaults")]
    public async Task<IActionResult> ResetDefaults()
    {
        await policies.ClearAsync();
        var defaults = OpenRtbPiiService.GetDefaultPolicies();
        foreach (var policy in defaults)
            await policies.AddAsync(policy);
        return Ok(defaults);
    }

    // ── Audit Log ──────────────────────────────────────────────────────────

    [HttpGet("audit-log")]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] string? host,
        [FromQuery] string? fieldPath,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = new PiiLogFilter(host, fieldPath, from, to);
        var items = await auditLogs.GetPagedAsync(filter, page, pageSize);
        var total = await auditLogs.CountAsync(filter);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("audit-log/capture/{captureId:guid}")]
    public async Task<IActionResult> GetAuditLogForCapture(Guid captureId) =>
        Ok(await auditLogs.GetByCaptureIdAsync(captureId));

    [HttpGet("audit-log/stats")]
    public async Task<IActionResult> GetAuditStats(
        [FromQuery] string? host,
        [FromQuery] string? fieldPath,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var filter = new PiiLogFilter(host, fieldPath, from, to);
        return Ok(await auditLogs.GetStatsAsync(filter));
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public record CreatePiiPolicyDto(
    string FieldPath,
    PiiRedactionStrategy Strategy,
    bool? Enabled = true,
    string? HostPattern = null,
    int? Priority = 0);

public record UpdatePiiPolicyDto(
    string? FieldPath = null,
    PiiRedactionStrategy? Strategy = null,
    bool? Enabled = null,
    string? HostPattern = null,
    int? Priority = null);
