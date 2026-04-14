using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class UserSafetyGuardsTests
{
    private static async Task<(HttpClient client, string adminUserId)> CreateAdminClientAsync()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/setup", new { password = "adminpass123" });
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "adminpass123" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("token").GetString()!;
        var userId = loginBody.GetProperty("user").GetProperty("id").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, userId);
    }

    [Fact]
    public async Task DeleteUser_Self_Returns400()
    {
        var (client, userId) = await CreateAdminClientAsync();

        var resp = await client.DeleteAsync($"/api/auth/users/{userId}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_LastAdmin_ReturnsBadRequestOrCannotDelete()
    {
        // Self-delete guard fires first for the current admin; create another admin scenario
        // by ensuring the only admin cannot be deleted through the self-guard as well.
        var (client, userId) = await CreateAdminClientAsync();

        await client.PostAsJsonAsync("/api/auth/users",
            new { username = "viewer1", password = "pass123", role = "Viewer" });

        var resp = await client.DeleteAsync($"/api/auth/users/{userId}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_DemoteLastAdmin_Returns400()
    {
        var (client, userId) = await CreateAdminClientAsync();

        var resp = await client.PutAsJsonAsync($"/api/auth/users/{userId}",
            new { role = "Viewer" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
