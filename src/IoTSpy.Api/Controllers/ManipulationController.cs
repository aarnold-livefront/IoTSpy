using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

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
    IApiSpecRepository apiSpecs,
    IAuditRepository auditRepo,
    IAiMockService? aiMockService = null) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    private Guid? CurrentUserId => Guid.TryParse(
        HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private string CurrentUsername => HttpContext.User.Identity?.Name ?? "system";
    private string CurrentIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

    // ── Rules ────────────────────────────────────────────────────────────────

    [HttpGet("rules")]
    public async Task<IActionResult> ListRules(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        var allItems = await rules.GetAllAsync(ct);
        var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { items, total = allItems.Count, page, pageSize, pages = (int)Math.Ceiling(allItems.Count / (double)pageSize) });
    }

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
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleDto dto, CancellationToken ct)
    {
        var rule = await rules.GetByIdAsync(id, ct);
        if (rule is null) return NotFound();

        var oldValue = JsonSerializer.Serialize(rule);

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

        await rules.UpdateAsync(rule, ct);
        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = CurrentUserId, Username = CurrentUsername,
            Action = "Update", EntityType = "ManipulationRule", EntityId = id.ToString(),
            OldValue = oldValue, NewValue = JsonSerializer.Serialize(rule), IpAddress = CurrentIp
        }, ct);
        return Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var rule = await rules.GetByIdAsync(id, ct);
        if (rule is not null)
            await auditRepo.AddAsync(new AuditEntry
            {
                UserId = CurrentUserId, Username = CurrentUsername,
                Action = "Delete", EntityType = "ManipulationRule", EntityId = id.ToString(),
                OldValue = JsonSerializer.Serialize(rule), IpAddress = CurrentIp
            }, ct);
        await rules.DeleteAsync(id, ct);
        return NoContent();
    }

    // ── Bulk rule ops ─────────────────────────────────────────────────────────

    [HttpPatch("rules/bulk")]
    public async Task<IActionResult> BulkUpdateRules([FromBody] BulkUpdateRulesDto dto, CancellationToken ct)
    {
        var updated = 0;
        foreach (var id in dto.Ids ?? [])
        {
            var rule = await rules.GetByIdAsync(id, ct);
            if (rule is null) continue;
            rule.Enabled = dto.Enabled;
            await rules.UpdateAsync(rule, ct);
            updated++;
        }
        return Ok(new { updated });
    }

    // ── Breakpoints ──────────────────────────────────────────────────────────

    [HttpGet("breakpoints")]
    public async Task<IActionResult> ListBreakpoints(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        var allItems = await breakpoints.GetAllAsync(ct);
        var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { items, total = allItems.Count, page, pageSize, pages = (int)Math.Ceiling(allItems.Count / (double)pageSize) });
    }

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
    public async Task<IActionResult> UpdateBreakpoint(Guid id, [FromBody] UpdateBreakpointDto dto, CancellationToken ct)
    {
        var bp = await breakpoints.GetByIdAsync(id, ct);
        if (bp is null) return NotFound();

        var oldValue = JsonSerializer.Serialize(bp);

        if (dto.Name is not null) bp.Name = dto.Name;
        if (dto.Enabled.HasValue) bp.Enabled = dto.Enabled.Value;
        if (dto.Language.HasValue) bp.Language = dto.Language.Value;
        if (dto.ScriptCode is not null) bp.ScriptCode = dto.ScriptCode;
        bp.HostPattern = dto.HostPattern ?? bp.HostPattern;
        bp.PathPattern = dto.PathPattern ?? bp.PathPattern;
        if (dto.Phase.HasValue) bp.Phase = dto.Phase.Value;

        await breakpoints.UpdateAsync(bp, ct);
        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = CurrentUserId, Username = CurrentUsername,
            Action = "Update", EntityType = "Breakpoint", EntityId = id.ToString(),
            OldValue = oldValue, NewValue = JsonSerializer.Serialize(bp), IpAddress = CurrentIp
        }, ct);
        return Ok(bp);
    }

    [HttpDelete("breakpoints/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteBreakpoint(Guid id, CancellationToken ct)
    {
        var bp = await breakpoints.GetByIdAsync(id, ct);
        if (bp is not null)
            await auditRepo.AddAsync(new AuditEntry
            {
                UserId = CurrentUserId, Username = CurrentUsername,
                Action = "Delete", EntityType = "Breakpoint", EntityId = id.ToString(),
                OldValue = JsonSerializer.Serialize(bp), IpAddress = CurrentIp
            }, ct);
        await breakpoints.DeleteAsync(id, ct);
        return NoContent();
    }

    // ── Replay ───────────────────────────────────────────────────────────────

    [HttpGet("replays")]
    public async Task<IActionResult> ListReplays(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var itemsTask = replaySessions.GetAllAsync(page, pageSize, ct);
        var totalTask = replaySessions.CountAsync(ct);
        var items = await itemsTask;
        var total = await totalTask;
        return Ok(new { items, total, page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize) });
    }

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
    public async Task<IActionResult> ListFuzzerJobs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var itemsTask = fuzzerJobs.GetAllAsync(page, pageSize, ct);
        var totalTask = fuzzerJobs.CountAsync(ct);
        var items = await itemsTask;
        var total = await totalTask;
        return Ok(new { items, total, page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize) });
    }

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

    [HttpGet("fuzzer/jobs/{id:guid}/export")]
    public async Task<IActionResult> ExportFuzzerResults(Guid id, CancellationToken ct)
    {
        var job = await fuzzerJobs.GetByIdAsync(id, ct);
        if (job is null) return NotFound();
        var results = await fuzzerJobs.GetResultsAsync(id, ct);
        var bundle = new { fuzzerId = id, exportedAt = DateTimeOffset.UtcNow, results };
        var json = JsonSerializer.Serialize(bundle, _jsonOpts);
        return File(Encoding.UTF8.GetBytes(json), "application/json", $"fuzzer-{id}.json");
    }

    // ── Ruleset bundle ────────────────────────────────────────────────────────

    [HttpGet("export")]
    public async Task<IActionResult> ExportRuleset([FromQuery] Guid? specId, CancellationToken ct)
    {
        var allRules = await rules.GetAllAsync(ct);
        var allBreakpoints = await breakpoints.GetAllAsync(ct);
        var standaloneContentRules = await apiSpecs.GetAllStandaloneRulesAsync(ct);
        var allSpecs = await apiSpecs.GetAllAsync(ct);
        var specsToExport = specId.HasValue ? allSpecs.Where(s => s.Id == specId.Value).ToList() : allSpecs;

        var specRuleMap = new Dictionary<Guid, List<ContentReplacementRule>>();
        foreach (var spec in specsToExport)
            specRuleMap[spec.Id] = await apiSpecs.GetReplacementRulesAsync(spec.Id, ct);

        var apiSpecBundles = specsToExport
            .Select(s => new { document = s, rules = specRuleMap[s.Id] })
            .ToList();

        var allContentRules = standaloneContentRules.Concat(specRuleMap.Values.SelectMany(r => r));
        var referencedAssets = allContentRules
            .Where(r => r.ReplacementFilePath is not null)
            .Select(r => Path.GetFileName(r.ReplacementFilePath!))
            .Distinct()
            .ToList();

        var bundle = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            trafficRules = allRules,
            breakpoints = allBreakpoints,
            contentReplacementRules = standaloneContentRules,
            apiSpecs = apiSpecBundles,
            referencedAssets
        };

        var json = JsonSerializer.Serialize(bundle, _jsonOpts);
        return File(Encoding.UTF8.GetBytes(json), "application/json", "ruleset.json");
    }

    // ── Ruleset import ────────────────────────────────────────────────────────

    [HttpPost("import")]
    public async Task<IActionResult> ImportRuleset([FromBody] ImportRulesetDto dto, CancellationToken ct)
    {
        int rulesImported = 0, bpsImported = 0, contentRulesImported = 0, specsImported = 0;

        foreach (var r in dto.TrafficRules ?? [])
        {
            r.Id = Guid.NewGuid();
            await rules.AddAsync(r, ct);
            rulesImported++;
        }

        foreach (var bp in dto.Breakpoints ?? [])
        {
            bp.Id = Guid.NewGuid();
            await breakpoints.AddAsync(bp, ct);
            bpsImported++;
        }

        foreach (var rule in dto.ContentReplacementRules ?? [])
        {
            rule.Id = Guid.NewGuid();
            rule.ApiSpecDocumentId = null;
            rule.ApiSpecDocument = null;
            await apiSpecs.AddReplacementRuleAsync(rule, ct);
            contentRulesImported++;
        }

        foreach (var bundle in dto.ApiSpecs ?? [])
        {
            if (bundle.Document is null) continue;
            var spec = bundle.Document;
            spec.Id = Guid.NewGuid();
            spec.ReplacementRules = [];
            var created = await apiSpecs.CreateAsync(spec, ct);
            foreach (var rule in bundle.Rules ?? [])
            {
                rule.Id = Guid.NewGuid();
                rule.ApiSpecDocumentId = created.Id;
                rule.ApiSpecDocument = null;
                await apiSpecs.AddReplacementRuleAsync(rule, ct);
            }
            specsImported++;
        }

        return Ok(new { rulesImported, breakpointsImported = bpsImported, contentRulesImported, apiSpecsImported = specsImported });
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

public record BulkUpdateRulesDto(
    List<Guid>? Ids = null,
    bool Enabled = true
);

public record ImportRulesetDto(
    List<ManipulationRule>? TrafficRules = null,
    List<Breakpoint>? Breakpoints = null,
    List<ContentReplacementRule>? ContentReplacementRules = null,
    List<ImportSpecBundleDto>? ApiSpecs = null
);

public record ImportSpecBundleDto(
    ApiSpecDocument? Document = null,
    List<ContentReplacementRule>? Rules = null
);
