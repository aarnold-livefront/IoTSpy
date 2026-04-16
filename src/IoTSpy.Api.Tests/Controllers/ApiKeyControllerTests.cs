using System.Security.Claims;
using IoTSpy.Api.Authentication;
using IoTSpy.Api.Controllers;
using IoTSpy.Api.Services;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ApiKeyControllerTests
{
    private static AuthService CreateAuthService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "test-secret-that-is-long-enough-for-jwt"
            })
            .Build());

    private static (AuthController controller, IApiKeyRepository keyRepo, IUserRepository userRepo, IAuditRepository auditRepo)
        CreateController(User? currentUser = null)
    {
        var auth = CreateAuthService();
        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(new ProxySettings());

        var userRepo = Substitute.For<IUserRepository>();
        var auditRepo = Substitute.For<IAuditRepository>();
        var keyRepo = Substitute.For<IApiKeyRepository>();

        currentUser ??= new User { Id = Guid.NewGuid(), Username = "admin", Role = UserRole.Admin };

        userRepo.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(currentUser);
        userRepo.CountAsync(Arg.Any<CancellationToken>()).Returns(1);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString()),
            new Claim(ClaimTypes.Name, currentUser.Username),
            new Claim(ClaimTypes.Role, currentUser.Role.ToString().ToLowerInvariant()),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var controller = new AuthController(auth, settingsRepo, userRepo, auditRepo, keyRepo)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };

        return (controller, keyRepo, userRepo, auditRepo);
    }

    // ── ListApiKeys ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListApiKeys_AsAdmin_ReturnsAllKeys()
    {
        var (controller, keyRepo, _, _) = CreateController();
        var keys = new List<ApiKey>
        {
            new() { Name = "ci-key", Scopes = "captures:read" },
            new() { Name = "ops-key", Scopes = "scanner:write" },
        };
        keyRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(keys);

        var result = await controller.ListApiKeys() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("ci-key", json);
        Assert.Contains("ops-key", json);
    }

    [Fact]
    public async Task ListApiKeys_AsOperator_ReturnsOwnKeysOnly()
    {
        var operatorUser = new User { Id = Guid.NewGuid(), Username = "ops", Role = UserRole.Operator };
        var (controller, keyRepo, _, _) = CreateController(operatorUser);
        keyRepo.GetByOwnerAsync(operatorUser.Id, Arg.Any<CancellationToken>()).Returns(new List<ApiKey>
        {
            new() { Name = "my-key", Scopes = "scanner:read", OwnerId = operatorUser.Id }
        });

        var result = await controller.ListApiKeys() as OkObjectResult;

        Assert.NotNull(result);
        await keyRepo.Received(1).GetByOwnerAsync(operatorUser.Id, Arg.Any<CancellationToken>());
    }

    // ── CreateApiKey ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateApiKey_ValidRequest_Returns201WithPlaintextKey()
    {
        var (controller, keyRepo, _, _) = CreateController();
        keyRepo.CreateAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ApiKey>());

        var result = await controller.CreateApiKey(
            new AuthController.CreateApiKeyRequest("test-key", "captures:read scanner:read", null))
            as CreatedResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("iotspy_", json);   // plaintext key included in response
        Assert.Contains("test-key", json);
    }

    [Fact]
    public async Task CreateApiKey_KeyHashIsStoredNotPlaintext()
    {
        var (controller, keyRepo, _, _) = CreateController();
        ApiKey? stored = null;
        keyRepo.CreateAsync(Arg.Do<ApiKey>(k => stored = k), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ApiKey>());

        await controller.CreateApiKey(
            new AuthController.CreateApiKeyRequest("hash-test", "captures:read", null));

        Assert.NotNull(stored);
        Assert.DoesNotContain("iotspy_", stored!.KeyHash);
    }

    // ── RevokeApiKey ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeApiKey_ExistingKey_SetsIsRevokedAndReturns204()
    {
        var (controller, keyRepo, _, _) = CreateController();
        var key = new ApiKey { Id = Guid.NewGuid(), Name = "old-key", IsRevoked = false };
        keyRepo.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);
        keyRepo.UpdateAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>()).Returns(key);

        var result = await controller.RevokeApiKey(key.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.True(key.IsRevoked);
    }

    [Fact]
    public async Task RevokeApiKey_NotFound_Returns404()
    {
        var (controller, keyRepo, _, _) = CreateController();
        keyRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ApiKey?)null);

        var result = await controller.RevokeApiKey(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // ── RotateApiKey ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RotateApiKey_ExistingKey_RevokesOldAndCreatesNew()
    {
        var (controller, keyRepo, _, _) = CreateController();
        var old = new ApiKey { Id = Guid.NewGuid(), Name = "key", Scopes = "scanner:read", OwnerId = Guid.NewGuid() };
        keyRepo.GetByIdAsync(old.Id, Arg.Any<CancellationToken>()).Returns(old);
        keyRepo.UpdateAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>()).Returns(old);
        keyRepo.CreateAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ApiKey>());

        var result = await controller.RotateApiKey(old.Id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.True(old.IsRevoked);
        await keyRepo.Received(1).CreateAsync(
            Arg.Is<ApiKey>(k => k.Name == old.Name && k.Scopes == old.Scopes),
            Arg.Any<CancellationToken>());
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("iotspy_", json); // new plaintext key returned
    }

    // ── ApiKeyAuthenticationHandler.HashKey ───────────────────────────────────

    [Fact]
    public void HashKey_SameInput_ProducesSameOutput()
    {
        var h1 = ApiKeyAuthenticationHandler.HashKey("iotspy_abc123");
        var h2 = ApiKeyAuthenticationHandler.HashKey("iotspy_abc123");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void HashKey_DifferentInput_ProducesDifferentOutput()
    {
        var h1 = ApiKeyAuthenticationHandler.HashKey("iotspy_abc123");
        var h2 = ApiKeyAuthenticationHandler.HashKey("iotspy_xyz999");
        Assert.NotEqual(h1, h2);
    }
}
