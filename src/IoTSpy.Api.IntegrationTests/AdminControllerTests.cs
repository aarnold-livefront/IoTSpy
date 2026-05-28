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

        var resp = await client.GetAsync("/api/admin/stats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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

        var resp = await client.GetAsync("/api/admin/stats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteCaptures_WithNoCriteria_Returns400()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/captures", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteCaptures_WithPurgeAll_Returns200WithDeletedCount()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/captures?purgeAll=true", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("deleted", json);
    }

    [Fact]
    public async Task DeletePackets_WithNoCriteria_Returns400()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/packets", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeletePackets_WithPurgeAll_Returns200()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync("/api/admin/packets?purgeAll=true", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ExportLogs_Json_ReturnsJsonFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/logs?format=json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportLogs_Csv_ReturnsCsvFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/logs?format=csv", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);
        var content = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("Timestamp,Method,Host,Path,StatusCode,RequestSize,ResponseSize,Device", content);
    }

    [Fact]
    public async Task ExportPackets_Csv_ReturnsCsvFile()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/packets?format=csv", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("Timestamp,Protocol,SourceIp,DestinationIp,SourcePort,DestinationPort,Length", content);
    }

    [Fact]
    public async Task ExportConfig_ReturnsJsonWithExpectedKeys()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/export/config", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("manipulationRules", content);
        Assert.Contains("scheduledScans", content);
        Assert.Contains("exportedAt", content);
    }

    [Fact]
    public async Task GetRetentionSettings_AsAdmin_ReturnsCurrentOptions()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.GetAsync("/api/admin/retention", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("enabled", json);
        Assert.Contains("captureRetentionDays", json);
        Assert.Contains("runIntervalHours", json);
    }

    [Fact]
    public async Task PutRetentionSettings_AsAdmin_UpdatesAndReturnsOk()
    {
        var client = await CreateAdminClientAsync();

        var payload = new
        {
            enabled = true,
            captureRetentionDays = 60,
            packetRetentionDays = 14,
            scanJobRetentionDays = 180,
            openRtbEventRetentionDays = 30,
            auditRetentionDays = 365,
            auditArchivePurgeDays = 730,
            runIntervalHours = 12.0
        };
        var putResp = await client.PutAsJsonAsync("/api/admin/retention", payload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Verify the GET reflects the update
        var getResp = await client.GetAsync("/api/admin/retention", TestContext.Current.CancellationToken);
        var json = await getResp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("60", json);   // captureRetentionDays
        Assert.Contains("12", json);   // runIntervalHours
    }

    [Fact]
    public async Task PutRetentionSettings_InvalidInterval_Returns400()
    {
        var client = await CreateAdminClientAsync();

        var payload = new
        {
            enabled = true,
            captureRetentionDays = 30,
            packetRetentionDays = 7,
            scanJobRetentionDays = 90,
            openRtbEventRetentionDays = 14,
            auditRetentionDays = 0,
            auditArchivePurgeDays = 0,
            runIntervalHours = 0.0
        };
        var resp = await client.PutAsJsonAsync("/api/admin/retention", payload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
