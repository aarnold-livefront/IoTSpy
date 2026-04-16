using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using IoTSpy.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IoTSpy.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Authenticates requests that carry an <c>X-Api-Key</c> header.
/// Resolves the key hash → <see cref="Core.Models.ApiKey"/> → owner <see cref="Core.Models.User"/>
/// and builds a <see cref="ClaimsPrincipal"/> with the owner's role plus the key's scope claims.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string ApiKeyIdClaimType = "api_key_id";
    public const string ScopeClaimType = "scope";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var rawValues) || rawValues.Count == 0)
            return AuthenticateResult.NoResult();

        var raw = rawValues.ToString().Trim();
        if (string.IsNullOrEmpty(raw))
            return AuthenticateResult.NoResult();

        var hash = HashKey(raw);

        await using var scope = scopeFactory.CreateAsyncScope();
        var keyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var key = await keyRepo.GetByHashAsync(hash);
        if (key is null)
            return AuthenticateResult.Fail("Invalid API key.");

        if (key.IsRevoked)
            return AuthenticateResult.Fail("API key has been revoked.");

        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("API key has expired.");

        var owner = await userRepo.GetByIdAsync(key.OwnerId);
        if (owner is null || !owner.IsEnabled)
            return AuthenticateResult.Fail("Key owner not found or disabled.");

        // Update last-used timestamp (fire-and-forget; don't stall the request)
        key.LastUsedAt = DateTimeOffset.UtcNow;
        _ = keyRepo.UpdateAsync(key);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.Id.ToString()),
            new(ClaimTypes.Name, owner.Username),
            new(ClaimTypes.Role, owner.Role.ToString().ToLowerInvariant()),
            new(ApiKeyIdClaimType, key.Id.ToString()),
        };

        foreach (var s in key.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            claims.Add(new Claim(ScopeClaimType, s));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    /// <summary>SHA-256 hash of the raw key, Base64-encoded (standard, no padding).</summary>
    public static string HashKey(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }
}
