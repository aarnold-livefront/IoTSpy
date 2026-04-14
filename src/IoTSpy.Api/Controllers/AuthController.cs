using IoTSpy.Api.Services;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthService auth,
    IProxySettingsRepository settingsRepo,
    IUserRepository userRepo,
    IAuditRepository auditRepo) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record SetupRequest(string Password);
    public record CreateUserRequest(string Username, string Password, string? DisplayName, UserRole Role = UserRole.Viewer);
    public record UpdateUserRequest(string? DisplayName, UserRole? Role, bool? IsEnabled, string? Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Try multi-user login first
        var user = await userRepo.GetByUsernameAsync(req.Username);
        if (user is not null)
        {
            if (!user.IsEnabled)
                return Unauthorized(new { error = "Account is disabled" });
            if (!auth.VerifyPassword(req.Password, user.PasswordHash))
                return Unauthorized(new { error = "Invalid credentials" });

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await userRepo.UpdateAsync(user);

            var token = auth.GenerateToken(user);

            await auditRepo.AddAsync(new AuditEntry
            {
                UserId = user.Id,
                Username = user.Username,
                Action = "Login",
                EntityType = "User",
                EntityId = user.Id.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            });

            return Ok(new { token, user = new { user.Id, user.Username, user.DisplayName, role = user.Role.ToString().ToLowerInvariant() } });
        }

        // Fall back to legacy single-user auth (ProxySettings.PasswordHash)
        var settings = await settingsRepo.GetAsync();
        var legacyToken = auth.GenerateToken(req.Username, req.Password, settings.PasswordHash);
        if (legacyToken is null)
            return Unauthorized(new { error = "Invalid credentials" });

        return Ok(new { token = legacyToken, user = new { Id = Guid.Empty, Username = req.Username, DisplayName = req.Username, role = "admin" } });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest req)
    {
        // Check if any users exist or legacy password is set
        var userCount = await userRepo.CountAsync();
        var settings = await settingsRepo.GetAsync();

        if (userCount > 0 || auth.IsPasswordSet(settings.PasswordHash))
            return Conflict(new { error = "Password already configured" });

        // Create the first admin user
        var adminUser = new User
        {
            Username = "admin",
            PasswordHash = auth.HashPassword(req.Password),
            DisplayName = "Administrator",
            Role = UserRole.Admin,
            IsEnabled = true
        };
        await userRepo.CreateAsync(adminUser);

        // Also set legacy password for backward compatibility
        settings.PasswordHash = auth.HashPassword(req.Password);
        await settingsRepo.SaveAsync(settings);

        return Ok(new { message = "Password set" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var username = User.Identity?.Name;
        if (username is null) return Unauthorized();

        var user = await userRepo.GetByUsernameAsync(username);
        if (user is not null)
            return Ok(new { user.Id, user.Username, user.DisplayName, role = user.Role.ToString().ToLowerInvariant() });

        // Legacy user — treat as admin
        return Ok(new { Id = Guid.Empty, Username = username, DisplayName = username, role = "admin" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userCount = await userRepo.CountAsync();
        var settings = await settingsRepo.GetAsync();
        return Ok(new
        {
            passwordSet = userCount > 0 || auth.IsPasswordSet(settings.PasswordHash),
            multiUser = userCount > 0
        });
    }

    // ── User Management (Admin only) ─────────────────────────────────

    [Authorize(Roles = "admin")]
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers()
    {
        var users = await userRepo.GetAllAsync();
        return Ok(users.Select(u => new
        {
            u.Id, u.Username, u.DisplayName,
            role = u.Role.ToString().ToLowerInvariant(),
            u.IsEnabled, u.CreatedAt, u.LastLoginAt
        }));
    }

    [Authorize(Roles = "admin")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        var existing = await userRepo.GetByUsernameAsync(req.Username);
        if (existing is not null)
            return Conflict(new { error = $"Username '{req.Username}' already exists" });

        var user = new User
        {
            Username = req.Username,
            PasswordHash = auth.HashPassword(req.Password),
            DisplayName = req.DisplayName ?? req.Username,
            Role = req.Role,
            IsEnabled = true
        };
        await userRepo.CreateAsync(user);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "CreateUser",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Details = $"Created user '{user.Username}' with role {user.Role}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Created($"/api/auth/users/{user.Id}", new
        {
            user.Id, user.Username, user.DisplayName,
            role = user.Role.ToString().ToLowerInvariant(),
            user.IsEnabled, user.CreatedAt
        });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();

        if (req.Role.HasValue && req.Role.Value != UserRole.Admin && user.Role == UserRole.Admin)
        {
            var adminCount = (await userRepo.GetAllAsync()).Count(u => u.Role == UserRole.Admin && u.IsEnabled);
            if (adminCount <= 1)
                return BadRequest(new { error = "Cannot demote the last admin account" });
        }

        if (req.DisplayName is not null) user.DisplayName = req.DisplayName;
        if (req.Role.HasValue) user.Role = req.Role.Value;
        if (req.IsEnabled.HasValue) user.IsEnabled = req.IsEnabled.Value;
        if (!string.IsNullOrEmpty(req.Password)) user.PasswordHash = auth.HashPassword(req.Password);

        await userRepo.UpdateAsync(user);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "UpdateUser",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Details = $"Updated user '{user.Username}'",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new
        {
            user.Id, user.Username, user.DisplayName,
            role = user.Role.ToString().ToLowerInvariant(),
            user.IsEnabled, user.CreatedAt, user.LastLoginAt
        });
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();

        if (User.Identity?.Name == user.Username)
            return BadRequest(new { error = "Cannot delete your own account" });

        if (user.Role == UserRole.Admin)
        {
            var adminCount = (await userRepo.GetAllAsync()).Count(u => u.Role == UserRole.Admin && u.IsEnabled);
            if (adminCount <= 1)
                return BadRequest(new { error = "Cannot delete the last admin account" });
        }

        await userRepo.DeleteAsync(id);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "DeleteUser",
            EntityType = "User",
            EntityId = id.ToString(),
            Details = $"Deleted user '{user.Username}'",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return NoContent();
    }

    // ── Audit Log (Admin only) ─────────────────────────────────

    [Authorize(Roles = "admin")]
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog([FromQuery] int count = 100)
    {
        var entries = await auditRepo.GetRecentAsync(Math.Min(count, 500));
        return Ok(entries);
    }
}
