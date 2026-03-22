using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/captures")]
public class CapturesController(ICaptureRepository captures) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? host,
        [FromQuery] string? method,
        [FromQuery] int? statusCode,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var filter = new CaptureFilter(deviceId, host, method, statusCode, from, to, q);
        var rawItems = await captures.GetPagedAsync(filter, page, pageSize);
        var total = await captures.CountAsync(filter);
        var items = rawItems.Select(c => new
        {
            c.Id,
            c.DeviceId,
            c.Method,
            c.Scheme,
            c.Host,
            c.Port,
            c.Path,
            c.Query,
            c.RequestHeaders,
            c.RequestBodySize,
            c.StatusCode,
            c.StatusMessage,
            c.ResponseHeaders,
            c.ResponseBodySize,
            c.IsTls,
            c.TlsVersion,
            c.TlsCipherSuite,
            c.Protocol,
            c.Timestamp,
            c.DurationMs,
            c.ClientIp,
            c.IsModified,
            c.Notes,
        }).ToList();
        return Ok(new { items, total, page, pageSize, pages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var capture = await captures.GetByIdAsync(id);
        return capture is null ? NotFound() : Ok(capture);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await captures.DeleteAsync(id);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] Guid? deviceId)
    {
        await captures.ClearAsync(deviceId);
        return NoContent();
    }

    // ── Export endpoints (Phase 9.2) ─────────────────────────────────────────

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var items = await GetExportItems(deviceId, from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,Method,Url,StatusCode,RequestSize,ResponseSize,IsModified,Protocol");
        foreach (var r in items)
        {
            var url = $"{r.Scheme}://{r.Host}{r.Path}{r.Query}";
            sb.AppendLine($"{r.Id},{r.Timestamp:O},{CsvEscape(r.Method)},{CsvEscape(url)},{r.StatusCode},{r.RequestBodySize},{r.ResponseBodySize},{r.IsModified},{r.Protocol}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "captures.csv");
    }

    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson(
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var items = await GetExportItems(deviceId, from, to, ct);
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", "captures.json");
    }

    [HttpGet("export/har")]
    public async Task<IActionResult> ExportHar(
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var items = await GetExportItems(deviceId, from, to, ct);
        var har = BuildHar(items);
        var json = JsonSerializer.Serialize(har, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", "captures.har");
    }

    private Task<List<CapturedRequest>> GetExportItems(
        Guid? deviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var filter = new CaptureFilter(deviceId, null, null, null, from, to, null);
        return captures.GetPagedAsync(filter, 1, 10_000, ct);
    }

    private static object BuildHar(List<CapturedRequest> items) => new
    {
        log = new
        {
            version = "1.2",
            creator = new { name = "IoTSpy", version = "1.0" },
            entries = items.Select(r => new
            {
                startedDateTime = r.Timestamp.ToString("O"),
                time = r.DurationMs,
                request = new
                {
                    method = r.Method,
                    url = $"{r.Scheme}://{r.Host}{r.Path}{r.Query}",
                    httpVersion = "HTTP/1.1",
                    headers = Array.Empty<object>(),
                    queryString = Array.Empty<object>(),
                    cookies = Array.Empty<object>(),
                    headersSize = -1,
                    bodySize = r.RequestBodySize
                },
                response = new
                {
                    status = r.StatusCode,
                    statusText = r.StatusMessage,
                    httpVersion = "HTTP/1.1",
                    headers = Array.Empty<object>(),
                    cookies = Array.Empty<object>(),
                    content = new { size = r.ResponseBodySize, mimeType = "application/octet-stream" },
                    redirectURL = "",
                    headersSize = -1,
                    bodySize = r.ResponseBodySize
                },
                cache = new { },
                timings = new { send = 0, wait = r.DurationMs, receive = 0 }
            })
        }
    };

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
