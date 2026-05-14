using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/scopes")]
public class ScanScopeController(IScanScopeRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await repo.GetAllAsync(ct);
        return Ok(new { items, total = items.Count });
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateScanScopeDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        if (!IsValidCidr(dto.Cidr))
            return BadRequest($"'{dto.Cidr}' is not a valid CIDR block (e.g. 192.168.1.0/24).");

        var userId = GetCurrentUserId();
        var username = User.Identity?.Name ?? "unknown";

        var scope = new ScanScope
        {
            Name = dto.Name.Trim(),
            Cidr = dto.Cidr.Trim(),
            IsActive = true,
            CreatedByUserId = userId,
            CreatedByUsername = username
        };

        var created = await repo.AddAsync(scope, ct);
        return Created($"/api/scopes/{created.Id}", created);
    }

    [HttpPatch("{id:guid}/toggle")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var scope = await repo.GetByIdAsync(id, ct);
        if (scope is null) return NotFound();

        scope.IsActive = !scope.IsActive;
        await repo.UpdateAsync(scope, ct);
        return Ok(scope);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var scope = await repo.GetByIdAsync(id, ct);
        if (scope is null) return NotFound();

        await repo.DeleteAsync(id, ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static bool IsValidCidr(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr)) return false;
        var slash = cidr.IndexOf('/');
        if (slash < 0)
            return System.Net.IPAddress.TryParse(cidr, out _);

        if (!System.Net.IPAddress.TryParse(cidr[..slash], out var addr)) return false;
        if (!int.TryParse(cidr[(slash + 1)..], out var prefix)) return false;
        int maxPrefix = addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
        return prefix >= 0 && prefix <= maxPrefix;
    }
}

public record CreateScanScopeDto(string Name, string Cidr);
