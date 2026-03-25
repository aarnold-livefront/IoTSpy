using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/manipulation")]
public class ManipulationController(
    IManipulationService manipulationService,
    IManipulationRuleRepository rules,
    IBreakpointRepository breakpoints,
    IReplaySessionRepository replaySessions,
    IFuzzerJobRepository fuzzerJobs,
    ICaptureRepository captures,
    IAiMockService? aiMockService = null) : ControllerBase
{
    // ── Rules ────────────────────────────────────────────────────────────────

    [HttpGet("rules")]
    public async Task<IActionResult> ListRules() =>
        Ok(await rules.GetAllAsync());

    [HttpGet("rules/{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id)
    {
        var rule = await rules.GetByIdAsync(id);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleDto dto)
    {
        var rule = new ManipulationRule
        {
            Name = dto.Name,
            Enabled = dto.Enabled ?? true,
            Priority = dto.Priority ?? 0,
            HostPattern = dto.HostPattern,
            PathPattern = dto.PathPattern,
            MethodPattern = dto.MethodPattern,
            Phase = dto.Phase ?? ManipulationPhase.Request,
            Action = dto.Action,
            HeaderName = dto.HeaderName,
            HeaderValue = dto.HeaderValue,
            BodyReplace = dto.BodyReplace,
            BodyReplaceWith = dto.BodyReplaceWith,
            OverrideStatusCode = dto.OverrideStatusCode,
            DelayMs = dto.DelayMs
        };

        await rules.AddAsync(rule);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleDto dto)
    {
        var rule = await rules.GetByIdAsync(id);
        if (rule is null) return NotFound();

        if (dto.Name is not null) rule.Name = dto.Name;
        if (dto.Enabled.HasValue) rule.Enabled = dto.Enabled.Value;
        if (dto.Priority.HasValue) rule.Priority = dto.Priority.Value;
        rule.HostPattern = dto.HostPattern ?? rule.HostPattern;
        rule.PathPattern = dto.PathPattern ?? rule.PathPattern;
        rule.MethodPattern = dto.MethodPattern ?? rule.MethodPattern;
        if (dto.Phase.HasValue) rule.Phase = dto.Phase.Value;
        if (dto.Action.HasValue) rule.Action = dto.Action.Value;
        rule.HeaderName = dto.HeaderName ?? rule.HeaderName;
        rule.HeaderValue = dto.HeaderValue ?? rule.HeaderValue;
        rule.BodyReplace = dto.BodyReplace ?? rule.BodyReplace;
        rule.BodyReplaceWith = dto.BodyReplaceWith ?? rule.BodyReplaceWith;
        rule.OverrideStatusCode = dto.OverrideStatusCode ?? rule.OverrideStatusCode;
        rule.DelayMs = dto.DelayMs ?? rule.DelayMs;

        await rules.UpdateAsync(rule);
        return Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        await rules.DeleteAsync(id);
        return NoContent();
    }

    // ── Breakpoints ──────────────────────────────────────────────────────────

    [HttpGet("breakpoints")]
    public async Task<IActionResult> ListBreakpoints() =>
        Ok(await breakpoints.GetAllAsync());

    [HttpGet("breakpoints/{id:guid}")]
    public async Task<IActionResult> GetBreakpoint(Guid id)
    {
        var bp = await breakpoints.GetByIdAsync(id);
        return bp is null ? NotFound() : Ok(bp);
    }

    [HttpPost("breakpoints")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateBreakpoint([FromBody] CreateBreakpointDto dto)
    {
        var bp = new Breakpoint
        {
            Name = dto.Name,
            Enabled = dto.Enabled ?? true,
            Language = dto.Language,
            ScriptCode = dto.ScriptCode,
            HostPattern = dto.HostPattern,
            PathPattern = dto.PathPattern,
            Phase = dto.Phase ?? ManipulationPhase.Request
        };

        await breakpoints.AddAsync(bp);
        return CreatedAtAction(nameof(GetBreakpoint), new { id = bp.Id }, bp);
    }

    [HttpPut("breakpoints/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateBreakpoint(Guid id, [FromBody] UpdateBreakpointDto dto)
    {
        var bp = await breakpoints.GetByIdAsync(id);
        if (bp is null) return NotFound();

        if (dto.Name is not null) bp.Name = dto.Name;
        if (dto.Enabled.HasValue) bp.Enabled = dto.Enabled.Value;
        if (dto.Language.HasValue) bp.Language = dto.Language.Value;
        if (dto.ScriptCode is not null) bp.ScriptCode = dto.ScriptCode;
        bp.HostPattern = dto.HostPattern ?? bp.HostPattern;
        bp.PathPattern = dto.PathPattern ?? bp.PathPattern;
        if (dto.Phase.HasValue) bp.Phase = dto.Phase.Value;

        await breakpoints.UpdateAsync(bp);
        return Ok(bp);
    }

    [HttpDelete("breakpoints/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteBreakpoint(Guid id)
    {
        await breakpoints.DeleteAsync(id);
        return NoContent();
    }

    // ── Replay ───────────────────────────────────────────────────────────────

    [HttpGet("replays")]
    public async Task<IActionResult> ListReplays([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await replaySessions.GetAllAsync(page, pageSize));

    [HttpGet("replays/{id:guid}")]
    public async Task<IActionResult> GetReplay(Guid id)
    {
        var session = await replaySessions.GetByIdAsync(id);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("replays/capture/{captureId:guid}")]
    public async Task<IActionResult> GetReplaysByCapture(Guid captureId) =>
        Ok(await replaySessions.GetByCaptureIdAsync(captureId));

    [HttpPost("replays")]
    public async Task<IActionResult> StartReplay([FromBody] StartReplayDto dto)
    {
        var capture = await captures.GetByIdAsync(dto.CaptureId);
        if (capture is null) return NotFound("Capture not found");

        var session = new ReplaySession
        {
            OriginalCaptureId = dto.CaptureId,
            RequestMethod = dto.Method ?? capture.Method,
            RequestScheme = capture.Scheme,
            RequestHost = dto.Host ?? capture.Host,
            RequestPort = dto.Port ?? capture.Port,
            RequestPath = dto.Path ?? capture.Path,
            RequestQuery = dto.Query ?? capture.Query,
            RequestHeaders = dto.RequestHeaders ?? capture.RequestHeaders,
            RequestBody = dto.RequestBody ?? capture.RequestBody
        };

        var result = await manipulationService.ReplayAsync(session);
        return Ok(result);
    }

    [HttpDelete("replays/{id:guid}")]
    public async Task<IActionResult> DeleteReplay(Guid id)
    {
        await replaySessions.DeleteAsync(id);
        return NoContent();
    }

    // ── Fuzzer ───────────────────────────────────────────────────────────────

    [HttpGet("fuzzer/jobs")]
    public async Task<IActionResult> ListFuzzerJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await fuzzerJobs.GetAllAsync(page, pageSize));

    [HttpGet("fuzzer/jobs/{id:guid}")]
    public async Task<IActionResult> GetFuzzerJob(Guid id)
    {
        var job = await fuzzerJobs.GetByIdAsync(id);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("fuzzer/jobs/{id:guid}/results")]
    public async Task<IActionResult> GetFuzzerResults(Guid id)
    {
        var job = await fuzzerJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        return Ok(await fuzzerJobs.GetResultsAsync(id));
    }

    [HttpPost("fuzzer")]
    public async Task<IActionResult> StartFuzzer([FromBody] StartFuzzerDto dto)
    {
        var capture = await captures.GetByIdAsync(dto.CaptureId);
        if (capture is null) return NotFound("Capture not found");

        var job = new FuzzerJob
        {
            BaseCaptureId = dto.CaptureId,
            Strategy = dto.Strategy ?? FuzzerStrategy.Random,
            MutationCount = dto.MutationCount ?? 50,
            ConcurrentRequests = dto.ConcurrentRequests ?? 5
        };

        var result = await manipulationService.StartFuzzerAsync(job);
        return Ok(result);
    }

    [HttpGet("fuzzer/jobs/{id:guid}/status")]
    public async Task<IActionResult> GetFuzzerStatus(Guid id)
    {
        var job = await fuzzerJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        return Ok(new
        {
            job.Id,
            job.Status,
            job.CompletedMutations,
            job.MutationCount,
            job.Anomalies,
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage,
            IsRunning = manipulationService.IsFuzzerRunning(id)
        });
    }

    [HttpPost("fuzzer/jobs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelFuzzer(Guid id)
    {
        var job = await fuzzerJobs.GetByIdAsync(id);
        if (job is null) return NotFound();
        await manipulationService.CancelFuzzerAsync(id);
        return Ok();
    }

    [HttpDelete("fuzzer/jobs/{id:guid}")]
    public async Task<IActionResult> DeleteFuzzerJob(Guid id)
    {
        await fuzzerJobs.DeleteAsync(id);
        return NoContent();
    }

    // ── AI Mock ──────────────────────────────────────────────────────────────

    [HttpPost("ai-mock/generate")]
    public async Task<IActionResult> GenerateAiMock([FromBody] AiMockGenerateDto dto)
    {
        if (aiMockService is null)
            return BadRequest("AI mock engine is not configured. Add an AiMock section to configuration.");

        var result = await aiMockService.GenerateResponseAsync(dto.Host, dto.Method, dto.Path, dto.RequestBody ?? string.Empty);
        return Ok(result);
    }

    [HttpPost("ai-mock/invalidate/{host}")]
    public async Task<IActionResult> InvalidateAiMockCache(string host)
    {
        if (aiMockService is null)
            return BadRequest("AI mock engine is not configured. Add an AiMock section to configuration.");

        await aiMockService.InvalidateSchemaCacheAsync(host);
        return Ok();
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record CreateRuleDto(
    string Name,
    ManipulationRuleAction Action,
    bool? Enabled = null,
    int? Priority = null,
    string? HostPattern = null,
    string? PathPattern = null,
    string? MethodPattern = null,
    ManipulationPhase? Phase = null,
    string? HeaderName = null,
    string? HeaderValue = null,
    string? BodyReplace = null,
    string? BodyReplaceWith = null,
    int? OverrideStatusCode = null,
    int? DelayMs = null
);

public record UpdateRuleDto(
    string? Name = null,
    bool? Enabled = null,
    int? Priority = null,
    string? HostPattern = null,
    string? PathPattern = null,
    string? MethodPattern = null,
    ManipulationPhase? Phase = null,
    ManipulationRuleAction? Action = null,
    string? HeaderName = null,
    string? HeaderValue = null,
    string? BodyReplace = null,
    string? BodyReplaceWith = null,
    int? OverrideStatusCode = null,
    int? DelayMs = null
);

public record CreateBreakpointDto(
    string Name,
    ScriptLanguage Language,
    string ScriptCode,
    bool? Enabled = null,
    string? HostPattern = null,
    string? PathPattern = null,
    ManipulationPhase? Phase = null
);

public record UpdateBreakpointDto(
    string? Name = null,
    bool? Enabled = null,
    ScriptLanguage? Language = null,
    string? ScriptCode = null,
    string? HostPattern = null,
    string? PathPattern = null,
    ManipulationPhase? Phase = null
);

public record StartReplayDto(
    Guid CaptureId,
    string? Method = null,
    string? Host = null,
    int? Port = null,
    string? Path = null,
    string? Query = null,
    string? RequestHeaders = null,
    string? RequestBody = null
);

public record StartFuzzerDto(
    Guid CaptureId,
    FuzzerStrategy? Strategy = null,
    int? MutationCount = null,
    int? ConcurrentRequests = null
);

public record AiMockGenerateDto(
    string Host,
    string Method,
    string Path,
    string? RequestBody = null
);
