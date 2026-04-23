using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contentrules")]
public class ContentRulesController(
    IApiSpecRepository repo,
    ReplacementPreviewService previewService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? host, CancellationToken ct)
    {
        var rules = host is not null
            ? await repo.GetStandaloneRulesForHostAsync(host, ct)
            : await repo.GetAllStandaloneRulesAsync(ct);
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContentRuleDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Host)) return BadRequest("Host is required");
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");
        if (string.IsNullOrWhiteSpace(dto.MatchPattern)) return BadRequest("MatchPattern is required");

        var rule = new ContentReplacementRule
        {
            Host = dto.Host.Trim(),
            Name = dto.Name,
            MatchType = dto.MatchType,
            MatchPattern = dto.MatchPattern,
            Action = dto.Action,
            Enabled = dto.Enabled ?? true,
            ReplacementValue = dto.ReplacementValue,
            ReplacementFilePath = dto.ReplacementFilePath,
            ReplacementContentType = dto.ReplacementContentType,
            HostPattern = dto.HostPattern,
            PathPattern = dto.PathPattern,
            Priority = dto.Priority ?? 0,
            SseInterEventDelayMs = dto.SseInterEventDelayMs,
            SseLoop = dto.SseLoop,
        };

        var created = await repo.AddReplacementRuleAsync(rule, ct);
        return Created($"/api/contentrules/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContentRuleDto dto, CancellationToken ct)
    {
        var rules = await repo.GetAllStandaloneRulesAsync(ct);
        var rule = rules.FirstOrDefault(r => r.Id == id);
        if (rule is null) return NotFound();

        if (dto.Host is not null) rule.Host = dto.Host.Trim();
        if (dto.Name is not null) rule.Name = dto.Name;
        if (dto.Enabled is not null) rule.Enabled = dto.Enabled.Value;
        if (dto.MatchType is not null) rule.MatchType = dto.MatchType.Value;
        if (dto.MatchPattern is not null) rule.MatchPattern = dto.MatchPattern;
        if (dto.Action is not null) rule.Action = dto.Action.Value;
        if (dto.ReplacementValue is not null) rule.ReplacementValue = dto.ReplacementValue;
        if (dto.ReplacementFilePath is not null) rule.ReplacementFilePath = dto.ReplacementFilePath;
        if (dto.ReplacementContentType is not null) rule.ReplacementContentType = dto.ReplacementContentType;
        if (dto.HostPattern is not null) rule.HostPattern = dto.HostPattern;
        if (dto.PathPattern is not null) rule.PathPattern = dto.PathPattern;
        if (dto.Priority is not null) rule.Priority = dto.Priority.Value;
        if (dto.SseInterEventDelayMs is not null) rule.SseInterEventDelayMs = dto.SseInterEventDelayMs;
        if (dto.SseLoop is not null) rule.SseLoop = dto.SseLoop;

        var updated = await repo.UpdateReplacementRuleAsync(rule, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await repo.DeleteReplacementRuleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromBody] PreviewRequest dto, CancellationToken ct)
    {
        var result = await previewService.PreviewAsync(Guid.Empty, id, dto, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

public record CreateContentRuleDto(
    string Host,
    string Name,
    ContentMatchType MatchType,
    string MatchPattern,
    ContentReplacementAction Action,
    bool? Enabled = true,
    string? ReplacementValue = null,
    string? ReplacementFilePath = null,
    string? ReplacementContentType = null,
    string? HostPattern = null,
    string? PathPattern = null,
    int? Priority = 0,
    int? SseInterEventDelayMs = null,
    bool? SseLoop = null);

public record UpdateContentRuleDto(
    string? Host = null,
    string? Name = null,
    bool? Enabled = null,
    ContentMatchType? MatchType = null,
    string? MatchPattern = null,
    ContentReplacementAction? Action = null,
    string? ReplacementValue = null,
    string? ReplacementFilePath = null,
    string? ReplacementContentType = null,
    string? HostPattern = null,
    string? PathPattern = null,
    int? Priority = null,
    int? SseInterEventDelayMs = null,
    bool? SseLoop = null);
