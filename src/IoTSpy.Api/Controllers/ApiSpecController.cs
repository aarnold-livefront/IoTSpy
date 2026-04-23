using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/apispec")]
public class ApiSpecController(
    IApiSpecService apiSpecService,
    IApiSpecRepository specRepo,
    ReplacementPreviewService previewService) : ControllerBase
{
    private static readonly string AssetsDirectory =
        Path.Combine(AppContext.BaseDirectory, "data", "assets");

    // ── Spec CRUD ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await specRepo.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var doc = await specRepo.GetByIdAsync(id);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] ApiSpecGenerationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Host))
            return BadRequest("Host is required");

        var doc = await apiSpecService.GenerateFromTrafficAsync(request, ct);
        return Created($"/api/apispec/{doc.Id}", doc);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportSpecDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.OpenApiJson))
            return BadRequest("OpenApiJson is required");

        try
        {
            var doc = await apiSpecService.ImportAsync(dto.OpenApiJson, dto.Name, ct);
            return Created($"/api/apispec/{doc.Id}", doc);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("Invalid JSON");
        }
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        try
        {
            var json = await apiSpecService.ExportAsync(id, ct);
            return File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                $"openapi-spec-{id}.json");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSpecDto dto, CancellationToken ct)
    {
        var doc = await specRepo.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        if (dto.Name is not null) doc.Name = dto.Name;
        if (dto.Description is not null) doc.Description = dto.Description;
        if (dto.Host is not null) doc.Host = dto.Host;
        if (dto.Version is not null) doc.Version = dto.Version;
        if (dto.MockEnabled.HasValue) doc.MockEnabled = dto.MockEnabled.Value;
        if (dto.PassthroughFirst.HasValue) doc.PassthroughFirst = dto.PassthroughFirst.Value;
        if (dto.Status.HasValue) doc.Status = dto.Status.Value;

        await specRepo.UpdateAsync(doc, ct);

        // Invalidate spec cache when mock settings change
        if (dto.MockEnabled.HasValue || dto.Status.HasValue)
            InvalidateSpecCache(doc.Host);

        return Ok(doc);
    }

    [HttpPatch("{id:guid}/openapi")]
    public async Task<IActionResult> UpdateOpenApi(Guid id, [FromBody] UpdateOpenApiDto dto, CancellationToken ct)
    {
        var doc = await specRepo.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        doc.OpenApiJson = dto.OpenApiJson;
        await specRepo.UpdateAsync(doc, ct);
        return Ok(doc);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await specRepo.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        await specRepo.DeleteAsync(id, ct);
        InvalidateSpecCache(doc.Host);
        return NoContent();
    }

    [HttpPost("{id:guid}/refine")]
    public async Task<IActionResult> Refine(Guid id, CancellationToken ct)
    {
        try
        {
            var doc = await apiSpecService.RefineWithLlmAsync(id, ct);
            return Ok(doc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Mock Control ──────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var doc = await specRepo.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        doc.Status = ApiSpecStatus.Active;
        doc.MockEnabled = true;
        await specRepo.UpdateAsync(doc, ct);
        InvalidateSpecCache(doc.Host);
        return Ok(doc);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var doc = await specRepo.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        doc.MockEnabled = false;
        await specRepo.UpdateAsync(doc, ct);
        InvalidateSpecCache(doc.Host);
        return Ok(doc);
    }

    // ── Content Replacement Rules ─────────────────────────────────────────────

    [HttpGet("{specId:guid}/rules")]
    public async Task<IActionResult> ListRules(Guid specId, CancellationToken ct) =>
        Ok(await specRepo.GetReplacementRulesAsync(specId, ct));

    [HttpPost("{specId:guid}/rules")]
    public async Task<IActionResult> CreateRule(Guid specId, [FromBody] CreateReplacementRuleDto dto, CancellationToken ct)
    {
        var spec = await specRepo.GetByIdAsync(specId, ct);
        if (spec is null) return NotFound();

        var rule = new ContentReplacementRule
        {
            ApiSpecDocumentId = specId,
            Name = dto.Name,
            Enabled = dto.Enabled ?? true,
            MatchType = dto.MatchType,
            MatchPattern = dto.MatchPattern,
            Action = dto.Action,
            ReplacementValue = dto.ReplacementValue,
            ReplacementFilePath = dto.ReplacementFilePath,
            ReplacementContentType = dto.ReplacementContentType,
            HostPattern = dto.HostPattern,
            PathPattern = dto.PathPattern,
            Priority = dto.Priority ?? 0,
            SseInterEventDelayMs = dto.SseInterEventDelayMs,
            SseLoop = dto.SseLoop,
        };

        await specRepo.AddReplacementRuleAsync(rule, ct);
        InvalidateSpecCache(spec.Host);
        return Created($"/api/apispec/{specId}/rules/{rule.Id}", rule);
    }

    [HttpPut("{specId:guid}/rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateRule(Guid specId, Guid ruleId, [FromBody] UpdateReplacementRuleDto dto, CancellationToken ct)
    {
        var rules = await specRepo.GetReplacementRulesAsync(specId, ct);
        var rule = rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return NotFound();

        if (dto.Name is not null) rule.Name = dto.Name;
        if (dto.Enabled.HasValue) rule.Enabled = dto.Enabled.Value;
        if (dto.MatchType.HasValue) rule.MatchType = dto.MatchType.Value;
        if (dto.MatchPattern is not null) rule.MatchPattern = dto.MatchPattern;
        if (dto.Action.HasValue) rule.Action = dto.Action.Value;
        if (dto.ReplacementValue is not null) rule.ReplacementValue = dto.ReplacementValue;
        if (dto.ReplacementFilePath is not null) rule.ReplacementFilePath = dto.ReplacementFilePath;
        if (dto.ReplacementContentType is not null) rule.ReplacementContentType = dto.ReplacementContentType;
        if (dto.HostPattern is not null) rule.HostPattern = dto.HostPattern;
        if (dto.PathPattern is not null) rule.PathPattern = dto.PathPattern;
        if (dto.Priority.HasValue) rule.Priority = dto.Priority.Value;
        if (dto.SseInterEventDelayMs.HasValue) rule.SseInterEventDelayMs = dto.SseInterEventDelayMs.Value;
        if (dto.SseLoop.HasValue) rule.SseLoop = dto.SseLoop.Value;

        await specRepo.UpdateReplacementRuleAsync(rule, ct);

        var spec = await specRepo.GetByIdAsync(specId, ct);
        if (spec is not null) InvalidateSpecCache(spec.Host);

        return Ok(rule);
    }

    [HttpDelete("{specId:guid}/rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(Guid specId, Guid ruleId, CancellationToken ct)
    {
        await specRepo.DeleteReplacementRuleAsync(ruleId, ct);

        var spec = await specRepo.GetByIdAsync(specId, ct);
        if (spec is not null) InvalidateSpecCache(spec.Host);

        return NoContent();
    }

    // ── Asset Upload ──────────────────────────────────────────────────────────

    [HttpPost("assets")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> UploadAsset(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded");

        Directory.CreateDirectory(AssetsDirectory);

        var safeFileName = Path.GetFileName(file.FileName);
        var storedName = $"{Guid.NewGuid():N}_{safeFileName}";
        var filePath = Path.Combine(AssetsDirectory, storedName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(new AssetUploadResult(filePath, storedName, file.ContentType, file.Length));
    }

    [HttpGet("assets")]
    public IActionResult ListAssets()
    {
        if (!Directory.Exists(AssetsDirectory))
            return Ok(Array.Empty<AssetInfo>());

        var files = Directory.GetFiles(AssetsDirectory)
            .Select(f =>
            {
                var info = new FileInfo(f);
                return new AssetInfo(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc);
            })
            .OrderByDescending(a => a.LastModified)
            .ToList();

        return Ok(files);
    }

    [HttpDelete("assets/{filename}")]
    public IActionResult DeleteAsset(string filename)
    {
        var safeName = Path.GetFileName(filename);
        var filePath = Path.Combine(AssetsDirectory, safeName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        System.IO.File.Delete(filePath);
        return NoContent();
    }

    // ── Rule Preview ──────────────────────────────────────────────────────────

    [HttpPost("{specId:guid}/rules/{ruleId:guid}/preview")]
    public async Task<IActionResult> PreviewRule(
        Guid specId, Guid ruleId, [FromBody] PreviewRequest request, CancellationToken ct)
    {
        var result = await previewService.PreviewAsync(specId, ruleId, request, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // ── Asset Content (public read-only) ──────────────────────────────────────

    [HttpGet("assets/{filename}/content")]
    [AllowAnonymous]
    public IActionResult GetAssetContent(string filename)
    {
        var safe = Path.GetFileName(filename);
        if (safe != filename || string.IsNullOrWhiteSpace(safe))
            return BadRequest("Invalid filename");

        var filePath = Path.Combine(AssetsDirectory, safe);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var ext = Path.GetExtension(safe);
        var mime = InferMime(ext);
        return PhysicalFile(filePath, mime, enableRangeProcessing: true);
    }

    private static string InferMime(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".json" => "application/json",
        ".html" or ".htm" => "text/html",
        ".txt" => "text/plain",
        ".sse" => "text/event-stream",
        ".ndjson" => "application/x-ndjson",
        _ => "application/octet-stream",
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void InvalidateSpecCache(string host)
    {
        if (apiSpecService is ApiSpecMockService mockService)
            mockService.InvalidateSpecCache(host);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public record ImportSpecDto(string OpenApiJson, string? Name = null);
    public record UpdateSpecDto(
        string? Name = null,
        string? Description = null,
        string? Host = null,
        string? Version = null,
        bool? MockEnabled = null,
        bool? PassthroughFirst = null,
        ApiSpecStatus? Status = null);
    public record UpdateOpenApiDto(string OpenApiJson);

    public record CreateReplacementRuleDto(
        string Name,
        ContentMatchType MatchType,
        string MatchPattern,
        ContentReplacementAction Action,
        bool? Enabled = null,
        string? ReplacementValue = null,
        string? ReplacementFilePath = null,
        string? ReplacementContentType = null,
        string? HostPattern = null,
        string? PathPattern = null,
        int? Priority = null,
        int? SseInterEventDelayMs = null,
        bool? SseLoop = null);

    public record UpdateReplacementRuleDto(
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

    public record AssetUploadResult(string FilePath, string FileName, string ContentType, long Size);
    public record AssetInfo(string FilePath, string FileName, long Size, DateTime LastModified);
}
