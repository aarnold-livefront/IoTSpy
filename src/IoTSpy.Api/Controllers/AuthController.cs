using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, IProxySettingsRepository settingsRepo) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record SetupRequest(string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var token = auth.GenerateToken(req.Username, req.Password);
        if (token is null) return Unauthorized(new { error = "Invalid credentials" });
        return Ok(new { token });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest req)
    {
        var settings = await settingsRepo.GetAsync();
        if (auth.IsPasswordSet(settings.PasswordHash))
            return Conflict(new { error = "Password already configured" });

        settings.PasswordHash = auth.HashPassword(req.Password);
        await settingsRepo.SaveAsync(settings);
        return Ok(new { message = "Password set" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var settings = await settingsRepo.GetAsync();
        return Ok(new { passwordSet = auth.IsPasswordSet(settings.PasswordHash) });
    }
}
