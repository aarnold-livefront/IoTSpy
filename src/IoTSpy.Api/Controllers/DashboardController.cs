using System.Security.Claims;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardLayoutRepository layoutRepo) : ControllerBase
{
    public record CreateLayoutRequest(string Name, string? LayoutJson, string? FiltersJson);
    public record UpdateLayoutRequest(string? Name, string? LayoutJson, string? FiltersJson, bool? IsDefault);

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    [HttpGet("layouts")]
    public async Task<IActionResult> GetLayouts()
    {
        var userId = GetUserId();
        var layouts = await layoutRepo.GetByUserAsync(userId);
        return Ok(layouts);
    }

    [HttpPost("layouts")]
    public async Task<IActionResult> CreateLayout([FromBody] CreateLayoutRequest req)
    {
        var userId = GetUserId();
        var layout = new DashboardLayout
        {
            UserId = userId,
            Name = req.Name,
            LayoutJson = req.LayoutJson ?? "{}",
            FiltersJson = req.FiltersJson ?? "{}"
        };
        await layoutRepo.CreateAsync(layout);
        return Created($"/api/dashboard/layouts/{layout.Id}", layout);
    }

    [HttpPut("layouts/{id:guid}")]
    public async Task<IActionResult> UpdateLayout(Guid id, [FromBody] UpdateLayoutRequest req)
    {
        var layout = await layoutRepo.GetByIdAsync(id);
        if (layout is null) return NotFound();

        var userId = GetUserId();
        if (layout.UserId != userId) return Forbid();

        if (req.Name is not null) layout.Name = req.Name;
        if (req.LayoutJson is not null) layout.LayoutJson = req.LayoutJson;
        if (req.FiltersJson is not null) layout.FiltersJson = req.FiltersJson;
        if (req.IsDefault.HasValue) layout.IsDefault = req.IsDefault.Value;

        await layoutRepo.UpdateAsync(layout);
        return Ok(layout);
    }

    [HttpDelete("layouts/{id:guid}")]
    public async Task<IActionResult> DeleteLayout(Guid id)
    {
        var layout = await layoutRepo.GetByIdAsync(id);
        if (layout is null) return NotFound();

        var userId = GetUserId();
        if (layout.UserId != userId) return Forbid();

        await layoutRepo.DeleteAsync(id);
        return NoContent();
    }
}
