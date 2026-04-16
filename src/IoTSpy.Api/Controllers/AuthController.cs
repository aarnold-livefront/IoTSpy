using System.Security.Cryptography;
using IoTSpy.Api.Authentication;
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
    IAuditRepository auditRepo,
    IApiKeyRepository apiKeyRepo) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record SetupRequest(string Password);
    public record CreateUserRequest(string Username, string Password, string? DisplayName, UserRole Role = UserRole.Viewer);
    public record UpdateUserRequest(string? DisplayName, UserRole? Role, bool? IsEnabled, string? Password);
    public record CreateApiKeyRequest(string Name, string Scopes, DateTimeOffset? ExpiresAt);

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

        return Ok(new { token = legacyToken, user = new { Id = Guid.Empty, req.Username, DisplayName = req.Username, role = "admin" } });
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

    // ── API Key Management (Admin / Operator) ──────────────────

    [Authorize(Roles = "admin,operator")]
    [HttpGet("api-keys")]
    public async Task<IActionResult> ListApiKeys()
    {
        // Admins see all keys; operators see only their own.
        var isAdmin = User.IsInRole("admin");
        List<ApiKey> keys;
        if (isAdmin)
        {
            keys = await apiKeyRepo.GetAllAsync();
        }
        else
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();
            keys = await apiKeyRepo.GetByOwnerAsync(userId);
        }

        return Ok(keys.Select(k => new
        {
            k.Id, k.Name,
            scopes = k.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            k.ExpiresAt, k.LastUsedAt, k.OwnerId, k.IsRevoked, k.CreatedAt
        }));
    }

    [Authorize(Roles = "admin,operator")]
    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest req)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { error = "Cannot resolve current user ID" });

        // Generate a 32-byte random key with the "iotspy_" prefix for easy identification.
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = $"iotspy_{Convert.ToHexString(rawBytes).ToLowerInvariant()}";
        var hash = ApiKeyAuthenticationHandler.HashKey(plaintext);

        var key = new ApiKey
        {
            Name = req.Name,
            KeyHash = hash,
            Scopes = req.Scopes,
            ExpiresAt = req.ExpiresAt,
            OwnerId = userId,
        };
        await apiKeyRepo.CreateAsync(key);

        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = userId,
            Username = User.Identity?.Name ?? "system",
            Action = "CreateApiKey",
            EntityType = "ApiKey",
            EntityId = key.Id.ToString(),
            Details = $"Created API key '{key.Name}' with scopes [{key.Scopes}]",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Created($"/api/auth/api-keys/{key.Id}", new
        {
            key.Id, key.Name,
            // Return the plaintext key ONCE — it is never retrievable after this response.
            key = plaintext,
            scopes = key.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            key.ExpiresAt, key.OwnerId, key.CreatedAt
        });
    }

    [Authorize(Roles = "admin,operator")]
    [HttpDelete("api-keys/{id:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid id)
    {
        var existing = await apiKeyRepo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        // Operators can only revoke their own keys.
        if (!User.IsInRole("admin") && existing.OwnerId != GetCurrentUserId())
            return Forbid();

        existing.IsRevoked = true;
        await apiKeyRepo.UpdateAsync(existing);

        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = GetCurrentUserId(),
            Username = User.Identity?.Name ?? "system",
            Action = "RevokeApiKey",
            EntityType = "ApiKey",
            EntityId = id.ToString(),
            Details = $"Revoked API key '{existing.Name}'",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return NoContent();
    }

    [Authorize(Roles = "admin,operator")]
    [HttpPost("api-keys/{id:guid}/rotate")]
    public async Task<IActionResult> RotateApiKey(Guid id)
    {
        var existing = await apiKeyRepo.GetByIdAsync(id);
        if (existing is null) return NotFound();

        // Operators can only rotate their own keys.
        if (!User.IsInRole("admin") && existing.OwnerId != GetCurrentUserId())
            return Forbid();

        // Revoke the old key.
        existing.IsRevoked = true;
        await apiKeyRepo.UpdateAsync(existing);

        // Issue a new key with the same name, scopes, and expiry as the old one.
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = $"iotspy_{Convert.ToHexString(rawBytes).ToLowerInvariant()}";
        var hash = ApiKeyAuthenticationHandler.HashKey(plaintext);

        var replacement = new ApiKey
        {
            Name = existing.Name,
            KeyHash = hash,
            Scopes = existing.Scopes,
            ExpiresAt = existing.ExpiresAt,
            OwnerId = existing.OwnerId,
        };
        await apiKeyRepo.CreateAsync(replacement);

        await auditRepo.AddAsync(new AuditEntry
        {
            UserId = GetCurrentUserId(),
            Username = User.Identity?.Name ?? "system",
            Action = "RotateApiKey",
            EntityType = "ApiKey",
            EntityId = id.ToString(),
            Details = $"Rotated API key '{existing.Name}' → new key {replacement.Id}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new
        {
            replacement.Id, replacement.Name,
            key = plaintext,
            scopes = replacement.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            replacement.ExpiresAt, replacement.OwnerId, replacement.CreatedAt,
            revokedId = existing.Id
        });
    }

    // ── Helpers ─────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
    }
}
