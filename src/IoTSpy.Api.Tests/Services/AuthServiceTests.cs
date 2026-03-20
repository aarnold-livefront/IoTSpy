using IoTSpy.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IoTSpy.Api.Tests.Services;

public class AuthServiceTests
{
    private static AuthService CreateService(string secret = "test-secret-that-is-long-enough-for-jwt")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = secret
            })
            .Build();
        return new AuthService(config);
    }

    [Fact]
    public void HashPassword_ProducesDifferentHashEachTime()
    {
        var auth = CreateService();
        var h1 = auth.HashPassword("password");
        var h2 = auth.HashPassword("password");

        // Different salts each time
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void GenerateToken_WithValidCredentials_ReturnsNonNullToken()
    {
        var auth = CreateService();
        var hash = auth.HashPassword("correctpassword");

        var token = auth.GenerateToken("admin", "correctpassword", hash);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_WithWrongPassword_ReturnsNull()
    {
        var auth = CreateService();
        var hash = auth.HashPassword("correctpassword");

        var token = auth.GenerateToken("admin", "wrongpassword", hash);

        Assert.Null(token);
    }

    [Fact]
    public void GenerateToken_WithWrongUsername_ReturnsNull()
    {
        var auth = CreateService();
        var hash = auth.HashPassword("password");

        var token = auth.GenerateToken("notadmin", "password", hash);

        Assert.Null(token);
    }

    [Fact]
    public void IsPasswordSet_WithEmptyHash_ReturnsFalse()
    {
        var auth = CreateService();

        Assert.False(auth.IsPasswordSet(string.Empty));
    }

    [Fact]
    public void IsPasswordSet_WithHash_ReturnsTrue()
    {
        var auth = CreateService();
        var hash = auth.HashPassword("pass");

        Assert.True(auth.IsPasswordSet(hash));
    }

    [Fact]
    public void GenerateToken_ProducesValidJwtFormat()
    {
        var auth = CreateService();
        var hash = auth.HashPassword("password");
        var token = auth.GenerateToken("admin", "password", hash);

        // JWT has 3 parts separated by dots
        Assert.NotNull(token);
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }
}
