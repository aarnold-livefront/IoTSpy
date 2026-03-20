using IoTSpy.Core.Models;
using IoTSpy.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class ReportEndpointTests : IAsyncLifetime
{
    private readonly IoTSpyWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private string _token = "";

    public async ValueTask InitializeAsync()
    {
        await _factory.InitializeDbAsync();
        _client = _factory.CreateClient();

        // Set up password and get token
        await _client.PostAsJsonAsync("/api/auth/setup", new { password = "reporttest123" });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "reporttest123" });
        var body = await loginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        _token = body.GetProperty("token").GetString() ?? "";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> CreateDeviceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoTSpyDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            IpAddress = $"10.0.0.{Random.Shared.Next(2, 254)}",
            Label = "ReportTestDevice"
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }

    [Fact]
    public async Task GetHtmlReport_ExistingDevice_Returns200WithHtmlContentType()
    {
        var deviceId = await CreateDeviceAsync();

        var response = await _client.GetAsync($"/api/reports/devices/{deviceId}/html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetPdfReport_ExistingDevice_Returns200WithPdfContentType()
    {
        var deviceId = await CreateDeviceAsync();

        var response = await _client.GetAsync($"/api/reports/devices/{deviceId}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetHtmlReport_NonExistentDevice_Returns404()
    {
        var response = await _client.GetAsync($"/api/reports/devices/{Guid.NewGuid()}/html");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPdfReport_NonExistentDevice_Returns404()
    {
        var response = await _client.GetAsync($"/api/reports/devices/{Guid.NewGuid()}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}
