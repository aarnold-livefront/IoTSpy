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
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("captures").GetProperty("count").GetInt32() >= 0);
        Assert.True(json.GetProperty("packets").GetProperty("count").GetInt32() >= 0);
        Assert.True(json.GetProperty("scanFindings").GetProperty("count").GetInt32() >= 0);
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
    public async Task GetStats_AsViewer_Returns403()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        // Set up admin and create a viewer
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "adminpass123" });
        var adminLoginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "adminpass123" });
        var adminBody = await adminLoginResp.Content.ReadFromJsonAsync<JsonElement>();
        var adminToken = adminBody.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Create a viewer account
        await client.PostAsJsonAsync("/api/auth/users",
            new { username = "viewer1", password = "viewpass123", role = "viewer" });

        // Log in as viewer
        var viewerClient = factory.CreateClient();
        var viewerLoginResp = await viewerClient.PostAsJsonAsync("/api/auth/login",
            new { username = "viewer1", password = "viewpass123" });
        var viewerBody = await viewerLoginResp.Content.ReadFromJsonAsync<JsonElement>();
        var viewerToken = viewerBody.GetProperty("token").GetString()!;
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        var resp = await viewerClient.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
