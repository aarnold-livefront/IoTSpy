using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace IoTSpy.Api.Services;

public class AuthService(IConfiguration config)
{
    private const string AdminUser = "admin";

    public string? GenerateToken(string username, string password)
    {
        var settings = config.GetSection("Auth");
        var storedHash = settings["PasswordHash"] ?? string.Empty;

        if (username != AdminUser || !VerifyPassword(password, storedHash))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "iotspy",
            audience: "iotspy",
            claims: [new Claim(ClaimTypes.Name, AdminUser), new Claim(ClaimTypes.Role, "admin")],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool IsPasswordSet(string storedHash) => !string.IsNullOrEmpty(storedHash);

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public string GetJwtSecret()
    {
        var secret = config["Auth:JwtSecret"];
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Auth:JwtSecret is not configured.");
        return secret;
    }
}
