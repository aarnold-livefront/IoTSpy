using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/plugins")]
[Authorize]
public class PluginsController(IPluginRegistry registry) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<PluginInfo>> GetAll() => Ok(registry.LoadedPlugins);

    [HttpGet("{protocol}")]
    public ActionResult<PluginInfo> GetByProtocol(string protocol)
    {
        var plugin = registry.LoadedPlugins
            .FirstOrDefault(p => p.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
        return plugin is null ? NotFound() : Ok(plugin);
    }

    [HttpPost("reload")]
    [Authorize(Roles = "Admin")]
    public IActionResult Reload()
    {
        registry.Reload();
        return Ok(new { reloaded = registry.LoadedPlugins.Count, plugins = registry.LoadedPlugins });
    }

    [HttpPost("decode/{protocol}")]
    public ActionResult<PluginDecodeResult> Decode(string protocol, [FromBody] DecodeRequest request)
    {
        var decoder = registry.GetDecoder(protocol);
        if (decoder is null)
            return NotFound(new { error = $"No plugin decoder registered for protocol '{protocol}'" });

        try
        {
            var bytes = Convert.FromBase64String(request.PayloadBase64);
            var result = decoder.Decode(bytes, request.ContentType);
            return Ok(result);
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "PayloadBase64 is not valid base-64" });
        }
    }
}

public sealed class DecodeRequest
{
    public string PayloadBase64 { get; set; } = "";
    public string? ContentType { get; set; }
}
