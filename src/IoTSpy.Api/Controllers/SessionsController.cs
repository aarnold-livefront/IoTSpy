using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using IoTSpy.Api.Hubs;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController(
    IInvestigationSessionRepository sessionRepo,
    ICaptureAnnotationRepository annotationRepo,
    ISessionActivityRepository activityRepo,
    IAuditRepository auditRepo,
    CollaborationPublisher publisher) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private string CurrentUsername =>
        User.Identity?.Name ?? User.FindFirstValue("sub") ?? "unknown";

    private bool IsViewer => User.IsInRole(UserRole.Viewer.ToString());

    // ── Session CRUD ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/sessions — list all active investigation sessions.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var sessions = await sessionRepo.GetAllAsync(includeInactive);
        return Ok(sessions.Select(SessionToDto));
    }

    /// <summary>GET /api/sessions/{id} — get a single session with its activity feed.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();
        return Ok(SessionToDto(session));
    }

    /// <summary>POST /api/sessions — create a new investigation session.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
    {
        if (IsViewer) return Forbid();

        var session = new InvestigationSession
        {
            Name = req.Name,
            Description = req.Description,
            CreatedByUserId = CurrentUserId,
            CreatedByUsername = CurrentUsername
        };
        await sessionRepo.CreateAsync(session);

        var activity = new SessionActivity
        {
            SessionId = session.Id,
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Action = "created session"
        };
        await activityRepo.AddAsync(activity);

        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Action = "CreateSession",
            EntityType = "InvestigationSession",
            EntityId = session.Id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return CreatedAtAction(nameof(GetById), new { id = session.Id }, SessionToDto(session));
    }

    /// <summary>PUT /api/sessions/{id} — update session name/description.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionRequest req)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        if (req.Name is not null) session.Name = req.Name;
        if (req.Description is not null) session.Description = req.Description;
        await sessionRepo.UpdateAsync(session);

        return Ok(SessionToDto(session));
    }

    /// <summary>DELETE /api/sessions/{id} — close/deactivate a session (Admin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        session.IsActive = false;
        session.ClosedAt = DateTimeOffset.UtcNow;
        await sessionRepo.UpdateAsync(session);

        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Action = "CloseSession",
            EntityType = "InvestigationSession",
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return NoContent();
    }

    // ── Session captures ─────────────────────────────────────────────────────────

    /// <summary>GET /api/sessions/{id}/captures — list all captures in this session.</summary>
    [HttpGet("{id:guid}/captures")]
    public async Task<IActionResult> GetCaptures(Guid id)
    {
        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        var scs = await sessionRepo.GetSessionCapturesAsync(id);
        return Ok(scs.Select(sc => new
        {
            sc.Id,
            sc.SessionId,
            sc.CaptureId,
            sc.AddedAt,
            sc.AddedByUserId,
            capture = sc.Capture is null ? null : new
            {
                sc.Capture.Id,
                sc.Capture.Method,
                sc.Capture.Host,
                sc.Capture.Path,
                sc.Capture.StatusCode,
                sc.Capture.Protocol,
                sc.Capture.Timestamp
            }
        }));
    }

    /// <summary>POST /api/sessions/{id}/captures — add a capture to the session.</summary>
    [HttpPost("{id:guid}/captures")]
    public async Task<IActionResult> AddCapture(Guid id, [FromBody] AddCaptureRequest req)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        if (await sessionRepo.ContainsCaptureAsync(id, req.CaptureId))
            return Conflict(new { error = "Capture already in session" });

        var sc = new SessionCapture
        {
            SessionId = id,
            CaptureId = req.CaptureId,
            AddedByUserId = CurrentUserId
        };
        await sessionRepo.AddCaptureAsync(sc);

        // Activity log + broadcast
        var activity = new SessionActivity
        {
            SessionId = id,
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Action = "added capture",
            Details = req.CaptureId.ToString()
        };
        await activityRepo.AddAsync(activity);
        await publisher.PublishActivityAsync(activity);

        return Ok(new { sc.Id, sc.SessionId, sc.CaptureId, sc.AddedAt });
    }

    /// <summary>DELETE /api/sessions/{id}/captures/{captureId} — remove a capture from the session.</summary>
    [HttpDelete("{id:guid}/captures/{captureId:guid}")]
    public async Task<IActionResult> RemoveCapture(Guid id, Guid captureId)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        await sessionRepo.RemoveCaptureAsync(id, captureId);
        return NoContent();
    }

    // ── Annotations (15.2) — REST fallback for non-SignalR clients ───────────────

    /// <summary>GET /api/sessions/{id}/annotations — list annotations for a session.</summary>
    [HttpGet("{id:guid}/annotations")]
    public async Task<IActionResult> GetAnnotations(Guid id)
    {
        var annotations = await annotationRepo.GetBySessionAsync(id);
        return Ok(annotations.Select(AnnotationToDto));
    }

    /// <summary>POST /api/sessions/{id}/annotations — add an annotation (Operator/Admin).</summary>
    [HttpPost("{id:guid}/annotations")]
    public async Task<IActionResult> AddAnnotation(Guid id, [FromBody] AddAnnotationRequest req)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        var annotation = new CaptureAnnotation
        {
            SessionId = id,
            CaptureId = req.CaptureId,
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Note = req.Note,
            Tags = req.Tags
        };
        await annotationRepo.AddAsync(annotation);

        var activity = new SessionActivity
        {
            SessionId = id,
            UserId = CurrentUserId,
            Username = CurrentUsername,
            Action = "added annotation",
            Details = $"Capture {req.CaptureId}: {req.Note[..Math.Min(req.Note.Length, 60)]}"
        };
        await activityRepo.AddAsync(activity);
        await publisher.PublishActivityAsync(activity);

        return CreatedAtAction(nameof(GetAnnotations), new { id }, AnnotationToDto(annotation));
    }

    /// <summary>PUT /api/sessions/{id}/annotations/{annotationId} — update an annotation.</summary>
    [HttpPut("{id:guid}/annotations/{annotationId:guid}")]
    public async Task<IActionResult> UpdateAnnotation(Guid id, Guid annotationId, [FromBody] UpdateAnnotationRequest req)
    {
        if (IsViewer) return Forbid();

        var annotation = await annotationRepo.GetByIdAsync(annotationId);
        if (annotation is null || annotation.SessionId != id) return NotFound();

        annotation.Note = req.Note;
        annotation.Tags = req.Tags;
        annotation.UpdatedAt = DateTimeOffset.UtcNow;
        await annotationRepo.UpdateAsync(annotation);

        return Ok(AnnotationToDto(annotation));
    }

    /// <summary>DELETE /api/sessions/{id}/annotations/{annotationId} — delete an annotation.</summary>
    [HttpDelete("{id:guid}/annotations/{annotationId:guid}")]
    public async Task<IActionResult> DeleteAnnotation(Guid id, Guid annotationId)
    {
        if (IsViewer) return Forbid();

        var annotation = await annotationRepo.GetByIdAsync(annotationId);
        if (annotation is null || annotation.SessionId != id) return NotFound();

        await annotationRepo.DeleteAsync(annotationId);
        return NoContent();
    }

    // ── Activity feed (15.5) ─────────────────────────────────────────────────────

    /// <summary>GET /api/sessions/{id}/activity — recent activity feed for a session.</summary>
    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid id, [FromQuery] int count = 100)
    {
        var activities = await activityRepo.GetBySessionAsync(id, count);
        return Ok(activities.Select(ActivityToDto));
    }

    // ── Session export (15.6) ────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/sessions/{id}/export — download a ZIP archive of the investigation session
    /// containing session metadata, captures, and annotations as JSON.
    /// </summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id)
    {
        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        var sessionCaptures = await sessionRepo.GetSessionCapturesAsync(id);
        var annotations = await annotationRepo.GetBySessionAsync(id);
        var activities = await activityRepo.GetBySessionAsync(id, 1000);

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };

            WriteJson(zip, "session.json", SessionToDto(session), opts);
            WriteJson(zip, "captures.json", sessionCaptures.Select(sc => new
            {
                sc.CaptureId,
                sc.AddedAt,
                sc.AddedByUserId,
                capture = sc.Capture is null ? null : new
                {
                    sc.Capture.Id,
                    sc.Capture.Method,
                    sc.Capture.Scheme,
                    sc.Capture.Host,
                    sc.Capture.Port,
                    sc.Capture.Path,
                    sc.Capture.Query,
                    sc.Capture.StatusCode,
                    sc.Capture.Protocol,
                    sc.Capture.Timestamp,
                    sc.Capture.DurationMs,
                    sc.Capture.ClientIp
                }
            }), opts);
            WriteJson(zip, "annotations.json", annotations.Select(AnnotationToDto), opts);
            WriteJson(zip, "activity.json", activities.Select(ActivityToDto), opts);
        }

        ms.Seek(0, SeekOrigin.Begin);
        var filename = $"session-{session.Name.Replace(" ", "_")}-{DateTime.UtcNow:yyyyMMdd}.zip";
        return File(ms, "application/zip", filename);
    }

    // ── AirDrop / URL sharing (15.7) ─────────────────────────────────────────────

    /// <summary>POST /api/sessions/{id}/share — generate a share token for AirDrop / URL sharing.</summary>
    [HttpPost("{id:guid}/share")]
    public async Task<IActionResult> GenerateShareToken(Guid id)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        // Regenerate token if already exists
        session.ShareToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await sessionRepo.UpdateAsync(session);

        var shareUrl = $"{Request.Scheme}://{Request.Host}/api/sessions/share/{session.ShareToken}";
        return Ok(new { token = session.ShareToken, url = shareUrl });
    }

    /// <summary>DELETE /api/sessions/{id}/share — revoke the share token.</summary>
    [HttpDelete("{id:guid}/share")]
    public async Task<IActionResult> RevokeShareToken(Guid id)
    {
        if (IsViewer) return Forbid();

        var session = await sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        session.ShareToken = null;
        await sessionRepo.UpdateAsync(session);
        return NoContent();
    }

    /// <summary>
    /// GET /api/sessions/share/{token} — public endpoint used by AirDrop / shared URLs.
    /// Returns session data in a portable JSON format (no auth required so device can open it).
    /// </summary>
    [HttpGet("share/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByShareToken(string token)
    {
        var session = await sessionRepo.GetByShareTokenAsync(token);
        if (session is null) return NotFound();

        var sessionCaptures = await sessionRepo.GetSessionCapturesAsync(session.Id);
        var annotations = await annotationRepo.GetBySessionAsync(session.Id);

        return Ok(new
        {
            session = SessionToDto(session),
            captures = sessionCaptures.Select(sc => sc.Capture is null ? null : new
            {
                sc.Capture.Id,
                sc.Capture.Method,
                sc.Capture.Scheme,
                sc.Capture.Host,
                sc.Capture.Path,
                sc.Capture.StatusCode,
                sc.Capture.Protocol,
                sc.Capture.Timestamp
            }),
            annotations = annotations.Select(AnnotationToDto),
            exportedAt = DateTimeOffset.UtcNow,
            format = "iotspy-session/v1"
        });
    }

    // ── DTO helpers ──────────────────────────────────────────────────────────────

    private static object SessionToDto(InvestigationSession s) => new
    {
        s.Id,
        s.Name,
        s.Description,
        s.CreatedByUserId,
        s.CreatedByUsername,
        s.CreatedAt,
        s.ClosedAt,
        s.IsActive,
        hasShareToken = s.ShareToken is not null
    };

    private static object AnnotationToDto(CaptureAnnotation a) => new
    {
        a.Id,
        a.SessionId,
        a.CaptureId,
        a.UserId,
        a.Username,
        a.Note,
        a.Tags,
        a.CreatedAt,
        a.UpdatedAt
    };

    private static object ActivityToDto(SessionActivity a) => new
    {
        a.Id,
        a.SessionId,
        a.UserId,
        a.Username,
        a.Action,
        a.Details,
        a.Timestamp
    };

    private static void WriteJson(ZipArchive zip, string entryName, object data, JsonSerializerOptions opts)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, data, opts);
    }

    // ── Request records ──────────────────────────────────────────────────────────

    public record CreateSessionRequest(string Name, string? Description);
    public record UpdateSessionRequest(string? Name, string? Description);
    public record AddCaptureRequest(Guid CaptureId);
    public record AddAnnotationRequest(Guid CaptureId, string Note, string? Tags);
    public record UpdateAnnotationRequest(string Note, string? Tags);
}
