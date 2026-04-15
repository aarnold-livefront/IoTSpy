using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class CertificatesControllerTests
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
    public async Task RegenerateCa_AsAdmin_Returns200()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.PostAsync("/api/certificates/root-ca/regenerate", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("message", content);
    }

    [Fact]
    public async Task RegenerateCa_Unauthenticated_Returns401()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/certificates/root-ca/regenerate", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
