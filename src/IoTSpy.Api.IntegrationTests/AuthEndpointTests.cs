using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class AuthEndpointTests : IClassFixture<IoTSpyWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(IoTSpyWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAuthStatus_ReturnsOkWithPasswordSetFalse()
    {
        var response = await _client.GetAsync("/api/auth/status", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("passwordSet", body);
    }

    [Fact]
    public async Task PostSetup_SetsInitialPassword_ReturnsOk()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/setup", new { password = "initialpass123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostLogin_WithoutSetup_ReturnsUnauthorized()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "anything" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSetup_ThenLogin_WithCorrectPassword_ReturnsToken()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        // Set up password
        var setupResp = await client.PostAsJsonAsync("/api/auth/setup", new { password = "securepass123" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, setupResp.StatusCode);

        // Login with correct credentials
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "securepass123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("token", body);
    }

    [Fact]
    public async Task PostSetup_Twice_ReturnsConflict()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/setup", new { password = "firstpass123" }, TestContext.Current.CancellationToken);
        var second = await client.PostAsJsonAsync("/api/auth/setup", new { password = "secondpass" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
