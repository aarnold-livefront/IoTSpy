using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/proxy")]
public class ProxyController(IProxyService proxy) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        isRunning = proxy.IsRunning,
        port = proxy.Port,
        settings = proxy.GetSettings()
    });

    [HttpPost("start")]
    public async Task<IActionResult> Start()
    {
        await proxy.StartAsync();
        return Ok(new { isRunning = proxy.IsRunning, port = proxy.Port });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await proxy.StopAsync();
        return Ok(new { isRunning = proxy.IsRunning });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] ProxySettings settings)
    {
        await proxy.UpdateSettingsAsync(settings);
        return Ok(proxy.GetSettings());
    }
}
