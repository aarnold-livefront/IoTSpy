using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class AdminControllerTests
{
    private static async Task<HttpClient> CreateAdminClientAsync()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/setup", new { password = "adminpass123" });
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "adminpass123" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetStats_AsAdmin_ReturnsOkWithCounts()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("captures", json);
        Assert.Contains("packets", json);
        Assert.Contains("scanFindings", json);
    }

    [Fact]
    public async Task GetStats_Unauthenticated_Returns401()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteCaptures_WithNoCriteria_Returns400()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/captures");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteCaptures_WithPurgeAll_Returns200WithDeletedCount()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/captures?purgeAll=true");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("deleted", json);
    }

    [Fact]
    public async Task DeletePackets_WithNoCriteria_Returns400()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/packets");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeletePackets_WithPurgeAll_Returns200()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/packets?purgeAll=true");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ExportLogs_Json_ReturnsJsonFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/logs?format=json");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportLogs_Csv_ReturnsCsvFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/logs?format=csv");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("Timestamp,Method,Host,Path,StatusCode,RequestSize,ResponseSize,Device", content);
    }

    [Fact]
    public async Task ExportPackets_Csv_ReturnsCsvFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/packets?format=csv");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("Timestamp,Protocol,SourceIp,DestinationIp,SourcePort,DestinationPort,Length", content);
    }

    [Fact]
    public async Task ExportConfig_ReturnsJsonWithExpectedKeys()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/config");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("manipulationRules", content);
        Assert.Contains("scheduledScans", content);
        Assert.Contains("exportedAt", content);
    }
}
