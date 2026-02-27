using IoTSpy.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var items = await captures.GetPagedAsync(filter, page, pageSize);
        var total = await captures.CountAsync(filter);
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
}
