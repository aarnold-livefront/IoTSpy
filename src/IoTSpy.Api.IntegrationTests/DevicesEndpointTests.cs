using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class DevicesEndpointTests : IAsyncLifetime
{
    private readonly IoTSpyWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private string _token = string.Empty;

    public async Task InitializeAsync()
    {
        await _factory.InitializeDbAsync();
        _client = _factory.CreateClient();

        // Set up auth
        await _client.PostAsJsonAsync("/api/auth/setup", new { password = "testpass123" });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "testpass123" });
        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        _token = loginJson.GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetDevices_WithAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_WithoutAuth_ReturnsUnauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_ReturnsEmptyListInitially()
    {
        var response = await _client.GetAsync("/api/devices");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task GetDevice_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/devices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProxyStatus_WithAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/proxy/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
