using System.Net;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

/// <summary>
/// Integration tests for the /health and /ready endpoints added in Phase 8.
/// </summary>
public class HealthCheckEndpointTests(IoTSpyWebApplicationFactory factory)
    : IClassFixture<IoTSpyWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsJsonWithHealthyStatus()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Healthy", status);
    }

    [Fact]
    public async Task GetReady_ReturnsOk()
    {
        var response = await _client.GetAsync("/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReady_ReturnsJsonWithChecks()
    {
        var response = await _client.GetAsync("/ready", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetHealth_DoesNotRequireAuthentication()
    {
        // Health endpoints must be reachable by load balancers without a JWT
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
