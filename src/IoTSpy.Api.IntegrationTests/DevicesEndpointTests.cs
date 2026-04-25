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

    public async ValueTask InitializeAsync()
    {
        await _factory.InitializeDbAsync();
        _client = _factory.CreateClient();

        // Set up auth
        await _client.PostAsJsonAsync("/api/auth/setup", new { password = "testpass123" }, TestContext.Current.CancellationToken);
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "testpass123" }, TestContext.Current.CancellationToken);
        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        _token = loginJson.GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetDevices_WithAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/devices", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_WithoutAuth_ReturnsUnauthorized()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/devices", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_ReturnsEmptyListInitially()
    {
        var response = await _client.GetAsync("/api/devices", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task GetDevice_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/devices/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProxyStatus_WithAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/proxy/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
