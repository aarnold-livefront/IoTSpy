using System.Security.Claims;
using IoTSpy.Analytics.Jobs;
using IoTSpy.Analytics.Services;
using IoTSpy.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public class AnalyticsController(
    ITrafficInsightRepository insightRepo,
    IInsightService insightService,
    InsightBatchJob? batchJob = null) : ControllerBase
{
    [HttpGet("triage")]
    public async Task<IActionResult> GetTriageQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool unreviewedOnly = true,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var items = await insightRepo.GetTriageQueueAsync(page, pageSize, unreviewedOnly, ct);
        var total = await insightRepo.CountTriageQueueAsync(unreviewedOnly, ct);

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            pages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("insights/{captureId:guid}")]
    public async Task<IActionResult> GetInsight(Guid captureId, CancellationToken ct = default)
    {
        var insight = await insightRepo.GetByCaptureIdAsync(captureId, ct);
        if (insight is null) return NotFound();
        return Ok(insight);
    }

    [HttpPost("insights/bulk")]
    public async Task<IActionResult> GetBulkInsights(
        [FromBody] BulkInsightRequest body, CancellationToken ct = default)
    {
        if (body.CaptureIds is null || body.CaptureIds.Count == 0)
            return Ok(new Dictionary<Guid, object>());

        var insights = await insightRepo.GetByCaptureIdsAsync(body.CaptureIds, ct);
        var map = insights.ToDictionary(i => i.CaptureId);
        return Ok(map);
    }

    [HttpPost("insights/{id:guid}/review")]
    public async Task<IActionResult> ReviewInsight(
        Guid id,
        [FromBody] ReviewInsightRequest body,
        CancellationToken ct = default)
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        await insightRepo.MarkReviewedAsync(id, userId, body.Dismissed, body.Note, ct);
        return NoContent();
    }

    [HttpPost("score/{captureId:guid}")]
    public async Task<IActionResult> ScoreCapture(Guid captureId, CancellationToken ct = default)
    {
        try
        {
            var insight = await insightService.ScoreByCaptureIdAsync(captureId, ct);
            return Ok(insight);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Capture {captureId} not found.");
        }
    }

    [HttpPost("batch-score")]
    [Authorize(Roles = "admin")]
    public IActionResult TriggerBatchScore()
    {
        if (batchJob is null)
            return ServiceUnavailable("Analytics batch job is not enabled.");

        batchJob.TriggerNow();
        return Accepted(new { message = "Batch scoring triggered." });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        var total = await insightRepo.CountTriageQueueAsync(unreviewedOnly: false, ct);
        var unreviewed = await insightRepo.CountTriageQueueAsync(unreviewedOnly: true, ct);
        return Ok(new { total, unreviewed, reviewed = total - unreviewed });
    }

    private IActionResult ServiceUnavailable(string message) =>
        StatusCode(503, new { message });
}

public record BulkInsightRequest(List<Guid>? CaptureIds);
public record ReviewInsightRequest(bool Dismissed, string? Note);
