using IoTSpy.Api.Controllers;
using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using IoTSpy.Core.Enums;

namespace IoTSpy.Api.Tests.Controllers;

public class AuthControllerTests
{
    private static AuthService CreateAuthService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "test-secret-that-is-long-enough-for-jwt"
            })
            .Build();
        return new AuthService(config);
    }

    private static (AuthController controller, IProxySettingsRepository settingsRepo, IUserRepository userRepo, IAuditRepository auditRepo)
        CreateController(AuthService? auth = null, ProxySettings? settings = null)
    {
        auth ??= CreateAuthService();
        settings ??= new ProxySettings { PasswordHash = string.Empty };

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        settingsRepo.SaveAsync(Arg.Any<ProxySettings>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(settings));

        var userRepo = Substitute.For<IUserRepository>();
        userRepo.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        userRepo.CountAsync(Arg.Any<CancellationToken>()).Returns(0);

        var auditRepo = Substitute.For<IAuditRepository>();
        var apiKeyRepo = Substitute.For<IApiKeyRepository>();

        var controller = new AuthController(auth, settingsRepo, userRepo, auditRepo, apiKeyRepo);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return (controller, settingsRepo, userRepo, auditRepo);
    }

    [Fact]
    public async Task Status_WhenPasswordNotSet_ReturnsFalse()
    {
        var (controller, _, _, _) = CreateController();
        var result = await controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("false", json);
    }

    [Fact]
    public async Task Status_WhenPasswordSet_ReturnsTrue()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("password123");
        var (controller, _, _, _) = CreateController(auth, new ProxySettings { PasswordHash = hash });

        var result = await controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("true", json);
    }

    [Fact]
    public async Task Setup_WhenPasswordNotSet_SetsPasswordAndReturnsOk()
    {
        var (controller, settingsRepo, userRepo, _) = CreateController();
        var result = await controller.Setup(new AuthController.SetupRequest("newpassword"));

        Assert.IsType<OkObjectResult>(result);
        await userRepo.Received(1).CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Setup_WhenPasswordAlreadySet_ReturnsConflict()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("existing");
        var (controller, _, _, _) = CreateController(auth, new ProxySettings { PasswordHash = hash });

        var result = await controller.Setup(new AuthController.SetupRequest("newpassword"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithValidLegacyCredentials_ReturnsToken()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("correctpassword");
        var (controller, _, _, _) = CreateController(auth, new ProxySettings { PasswordHash = hash });

        var result = await controller.Login(new AuthController.LoginRequest("admin", "correctpassword"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("correctpassword");
        var (controller, _, _, _) = CreateController(auth, new ProxySettings { PasswordHash = hash });

        var result = await controller.Login(new AuthController.LoginRequest("admin", "wrongpassword"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongUsername_ReturnsUnauthorized()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("password");
        var (controller, _, _, _) = CreateController(auth, new ProxySettings { PasswordHash = hash });

        var result = await controller.Login(new AuthController.LoginRequest("notadmin", "password"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithMultiUserCredentials_ReturnsTokenAndUserInfo()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("userpassword");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = hash,
            DisplayName = "Test User",
            Role = Core.Enums.UserRole.Operator,
            IsEnabled = true
        };

        var (controller, _, userRepo, auditRepo) = CreateController(auth);
        userRepo.GetByUsernameAsync("testuser", Arg.Any<CancellationToken>()).Returns(user);

        var result = await controller.Login(new AuthController.LoginRequest("testuser", "userpassword"));

        Assert.IsType<OkObjectResult>(result);
        await auditRepo.Received(1).AddAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_WithDisabledUser_ReturnsUnauthorized()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("password");
        var user = new User
        {
            Username = "disabled",
            PasswordHash = hash,
            IsEnabled = false
        };

        var (controller, _, userRepo, _) = CreateController(auth);
        userRepo.GetByUsernameAsync("disabled", Arg.Any<CancellationToken>()).Returns(user);

        var result = await controller.Login(new AuthController.LoginRequest("disabled", "password"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
