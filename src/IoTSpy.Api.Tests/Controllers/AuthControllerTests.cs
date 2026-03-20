using IoTSpy.Api.Controllers;
using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

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

    [Fact]
    public async Task Status_WhenPasswordNotSet_ReturnsFalse()
    {
        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = string.Empty });

        var controller = new AuthController(CreateAuthService(), settingsRepo);
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

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = hash });

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("true", json);
    }

    [Fact]
    public async Task Setup_WhenPasswordNotSet_SetsPasswordAndReturnsOk()
    {
        var auth = CreateAuthService();
        var settings = new ProxySettings { PasswordHash = string.Empty };

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(settings);
        settingsRepo.SaveAsync(Arg.Any<ProxySettings>()).Returns(Task.FromResult(settings));

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Setup(new AuthController.SetupRequest("newpassword"));

        Assert.IsType<OkObjectResult>(result);
        await settingsRepo.Received(1).SaveAsync(Arg.Is<ProxySettings>(s => !string.IsNullOrEmpty(s.PasswordHash)));
    }

    [Fact]
    public async Task Setup_WhenPasswordAlreadySet_ReturnsConflict()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("existing");

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = hash });

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Setup(new AuthController.SetupRequest("newpassword"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("correctpassword");

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = hash });

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Login(new AuthController.LoginRequest("admin", "correctpassword"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("correctpassword");

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = hash });

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Login(new AuthController.LoginRequest("admin", "wrongpassword"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongUsername_ReturnsUnauthorized()
    {
        var auth = CreateAuthService();
        var hash = auth.HashPassword("password");

        var settingsRepo = Substitute.For<IProxySettingsRepository>();
        settingsRepo.GetAsync().Returns(new ProxySettings { PasswordHash = hash });

        var controller = new AuthController(auth, settingsRepo);
        var result = await controller.Login(new AuthController.LoginRequest("notadmin", "password"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
