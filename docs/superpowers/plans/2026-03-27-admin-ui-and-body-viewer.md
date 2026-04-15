# Admin UI & Body Viewer Stream Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `/admin` page with Database, Certificates, Audit Log, and Users tabs; plus add SSE/NDJSON collapsible-event rendering to the body viewer.

**Architecture:** Backend adds `AdminController` (stats/purge/export, Admin-role gated) and a `regenerate` endpoint on `CertificatesController`; safety guards added to existing user delete/update in `AuthController`. Frontend adds `AdminPage` (four-tab layout) accessible via a wrench icon in `Header`. Body viewer changes are frontend-only: `resolvePretty` gains a stream-detection pass that parses SSE/NDJSON bodies into collapsible per-event rows.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (SQLite), xUnit + `IoTSpyWebApplicationFactory` (backend tests); React 19 + TypeScript + Vite (frontend); existing CSS custom properties + BEM naming.

---

## File Map

### New backend files
```
src/IoTSpy.Api/Controllers/AdminController.cs
```

### Modified backend files
```
src/IoTSpy.Api/Controllers/CertificatesController.cs   — add regenerate endpoint
src/IoTSpy.Api/Controllers/AuthController.cs           — safety guards on delete/update user
src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs  (new test file)
src/IoTSpy.Api.IntegrationTests/CertificatesControllerTests.cs  (new test file)
```

### New frontend files
```
frontend/src/pages/AdminPage.tsx
frontend/src/components/admin/DatabaseTab.tsx
frontend/src/components/admin/CertificatesTab.tsx
frontend/src/components/admin/AuditLogTab.tsx
frontend/src/components/admin/UsersTab.tsx
frontend/src/styles/admin.css
```

### Modified frontend files
```
frontend/src/hooks/useAuth.ts              — expose useCurrentUser()
frontend/src/pages/LoginPage.tsx           — store user info on login
frontend/src/components/layout/Header.tsx  — wrench icon for admin
frontend/src/App.tsx                       — add /admin route
frontend/src/types/api.ts                  — AdminStats, UserSummary types
frontend/src/components/common/BodyViewer.tsx  — stream detection + collapsible events
frontend/src/styles/body-viewer.css            — event row styles
```

---

## Task 1: AdminController — stats and purge endpoints

**Files:**
- Create: `src/IoTSpy.Api/Controllers/AdminController.cs`
- Create: `src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.IntegrationTests;

public class AdminControllerTests
{
    // Helper: create a fresh factory, init DB, return an admin-authed client
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
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd /home/anna/git/codify/IoTSpy
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "AdminControllerTests" --no-build 2>&1 | tail -20
```

Expected: build error (AdminController doesn't exist yet).

- [ ] **Step 3: Create AdminController with stats and purge endpoints**

Create `src/IoTSpy.Api/Controllers/AdminController.cs`:

```csharp
using IoTSpy.Core.Models;
using IoTSpy.Core.Interfaces;
using IoTSpy.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin")]
public class AdminController(
    IoTSpyDbContext db,
    IAuditRepository auditRepo) : ControllerBase
{
    // ── Stats ────────────────────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var captureCount = await db.Captures.CountAsync(ct);
        var packetCount = await db.Packets.CountAsync(ct);
        var scanFindingCount = await db.ScanFindings.CountAsync(ct);
        var oldestCapture = await db.Captures.MinAsync(c => (DateTimeOffset?)c.Timestamp, ct);
        var oldestPacket = await db.Packets.MinAsync(p => (DateTimeOffset?)p.Timestamp, ct);

        return Ok(new
        {
            captures = new
            {
                count = captureCount,
                estimatedSizeBytes = captureCount * 2048L,
                oldestTimestamp = oldestCapture
            },
            packets = new
            {
                count = packetCount,
                estimatedSizeBytes = packetCount * 512L,
                oldestTimestamp = oldestPacket
            },
            scanFindings = new { count = scanFindingCount }
        });
    }

    // ── Purge captures ───────────────────────────────────────────────────────

    [HttpDelete("captures")]
    public async Task<IActionResult> PurgeCaptures(
        [FromQuery] int? olderThanDays,
        [FromQuery] Guid? deviceId,
        [FromQuery] string? host,
        [FromQuery] bool purgeAll = false,
        CancellationToken ct = default)
    {
        if (!purgeAll && !olderThanDays.HasValue && !deviceId.HasValue && string.IsNullOrEmpty(host))
            return BadRequest(new { error = "Specify at least one filter, or use purgeAll=true" });

        var query = db.Captures.AsQueryable();
        if (!purgeAll)
        {
            if (olderThanDays.HasValue)
                query = query.Where(c => c.Timestamp < DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value));
            if (deviceId.HasValue)
                query = query.Where(c => c.DeviceId == deviceId);
            if (!string.IsNullOrEmpty(host))
                query = query.Where(c => c.Host == host);
        }

        var toDelete = await query.ToListAsync(ct);
        db.Captures.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgeCaptures",
            EntityType = "CapturedRequest",
            Details = $"Purged {toDelete.Count} captures",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { deleted = toDelete.Count });
    }

    // ── Purge packets ────────────────────────────────────────────────────────

    [HttpDelete("packets")]
    public async Task<IActionResult> PurgePackets(
        [FromQuery] int? olderThanDays,
        [FromQuery] bool purgeAll = false,
        CancellationToken ct = default)
    {
        if (!purgeAll && !olderThanDays.HasValue)
            return BadRequest(new { error = "Specify olderThanDays, or use purgeAll=true" });

        var query = db.Packets.AsQueryable();
        if (!purgeAll && olderThanDays.HasValue)
            query = query.Where(p => p.Timestamp < DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value));

        var toDelete = await query.ToListAsync(ct);
        db.Packets.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "PurgePackets",
            EntityType = "CapturedPacket",
            Details = $"Purged {toDelete.Count} packets",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { deleted = toDelete.Count });
    }
}
```

- [ ] **Step 4: Run tests — confirm stats and purge tests pass**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "AdminControllerTests" 2>&1 | tail -20
```

Expected: all 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IoTSpy.Api/Controllers/AdminController.cs \
        src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs
git commit -m "feat: add AdminController with stats and purge endpoints"
```

---

## Task 2: AdminController — export endpoints

**Files:**
- Modify: `src/IoTSpy.Api/Controllers/AdminController.cs`
- Modify: `src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs`

- [ ] **Step 1: Add export tests to AdminControllerTests.cs**

Append to the test class in `AdminControllerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run new export tests to confirm they fail**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "ExportLogs|ExportPackets|ExportConfig" 2>&1 | tail -10
```

Expected: FAIL — routes not found (404).

- [ ] **Step 3: Add export endpoints to AdminController**

Add these methods to `AdminController.cs` (inside the class, after `PurgePackets`):

```csharp
    // ── Export ───────────────────────────────────────────────────────────────

    [HttpGet("export/logs")]
    public async Task<IActionResult> ExportLogs([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var captures = await db.Captures.AsNoTracking()
            .Include(c => c.Device)
            .OrderBy(c => c.Timestamp)
            .ToListAsync(ct);

        if (format == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Method,Host,Path,StatusCode,RequestSize,ResponseSize,Device");
            foreach (var c in captures)
                csv.AppendLine($"{c.Timestamp:O},{Csv(c.Method)},{Csv(c.Host)},{Csv(c.Path)},{c.StatusCode},{c.RequestBodySize},{c.ResponseBodySize},{Csv(c.Device?.Name ?? "")}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "captures.csv");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(captures,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "captures.json");
    }

    [HttpGet("export/packets")]
    public async Task<IActionResult> ExportPackets([FromQuery] string format = "json", CancellationToken ct = default)
    {
        var packets = await db.Packets.AsNoTracking()
            .OrderBy(p => p.Timestamp)
            .ToListAsync(ct);

        if (format == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Protocol,SourceIp,DestinationIp,SourcePort,DestinationPort,Length");
            foreach (var p in packets)
                csv.AppendLine($"{p.Timestamp:O},{Csv(p.Protocol)},{Csv(p.SourceIp)},{Csv(p.DestinationIp)},{p.SourcePort},{p.DestinationPort},{p.Length}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "packets.csv");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(packets,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "packets.json");
    }

    [HttpGet("export/config")]
    public async Task<IActionResult> ExportConfig(CancellationToken ct = default)
    {
        var config = new
        {
            manipulationRules = await db.ManipulationRules.AsNoTracking().ToListAsync(ct),
            breakpoints = await db.Breakpoints.AsNoTracking().ToListAsync(ct),
            fuzzerJobs = await db.FuzzerJobs.AsNoTracking().ToListAsync(ct),
            scheduledScans = await db.ScheduledScans.AsNoTracking().ToListAsync(ct),
            openRtbPolicies = await db.OpenRtbPiiPolicies.AsNoTracking().ToListAsync(ct),
            apiSpecDocuments = await db.ApiSpecDocuments.AsNoTracking()
                .Include(d => d.ReplacementRules)
                .ToListAsync(ct),
            exportedAt = DateTimeOffset.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "iotspy-config.json");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
```

- [ ] **Step 4: Run all admin tests**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "AdminControllerTests" 2>&1 | tail -20
```

Expected: all 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IoTSpy.Api/Controllers/AdminController.cs \
        src/IoTSpy.Api.IntegrationTests/AdminControllerTests.cs
git commit -m "feat: add AdminController export endpoints (logs, packets, config)"
```

---

## Task 3: CertificatesController — regenerate CA endpoint

**Files:**
- Modify: `src/IoTSpy.Api/Controllers/CertificatesController.cs`
- Create: `src/IoTSpy.Api.IntegrationTests/CertificatesControllerTests.cs`

- [ ] **Step 1: Write failing test**

Create `src/IoTSpy.Api.IntegrationTests/CertificatesControllerTests.cs`:

```csharp
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
    public async Task RegenerateCa_AsAdmin_Returns200WithNewCaInfo()
    {
        var client = await CreateAdminClientAsync();

        var resp = await client.PostAsync("/api/certificates/root-ca/regenerate", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("commonName", content);
    }

    [Fact]
    public async Task RegenerateCa_Unauthenticated_Returns401()
    {
        var factory = new IoTSpyWebApplicationFactory();
        await factory.InitializeDbAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/certificates/root-ca/regenerate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "CertificatesControllerTests" 2>&1 | tail -10
```

Expected: FAIL — 404 (route doesn't exist yet).

- [ ] **Step 3: Add `IAuditRepository` to `CertificatesController` and add regenerate endpoint**

Open `src/IoTSpy.Api/Controllers/CertificatesController.cs` and replace its content:

```csharp
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IoTSpy.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificates")]
public class CertificatesController(
    ICertificateAuthority ca,
    ICertificateRepository certs,
    IAuditRepository auditRepo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await certs.GetAllAsync());

    [HttpGet("root-ca")]
    public async Task<IActionResult> GetRootCa()
    {
        var entry = await ca.GetOrCreateRootCaAsync();
        return Ok(new
        {
            entry.Id,
            entry.CommonName,
            entry.SerialNumber,
            entry.NotBefore,
            entry.NotAfter,
            entry.IsRootCa,
            entry.CreatedAt,
            CertificatePem = entry.CertificatePem
        });
    }

    [HttpGet("root-ca/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadRootCaDer()
    {
        var der = await ca.ExportRootCaDerAsync();
        return File(der, "application/x-x509-ca-cert", "iotspy-ca.crt");
    }

    [HttpGet("root-ca/pem")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadRootCaPem()
    {
        var entry = await ca.GetOrCreateRootCaAsync();
        return File(
            System.Text.Encoding.UTF8.GetBytes(entry.CertificatePem),
            "application/x-pem-file",
            "iotspy-ca.pem");
    }

    [HttpPost("root-ca/regenerate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RegenerateRootCa()
    {
        // Purge all existing certificates (root + all leaf certs)
        var all = await certs.GetAllAsync();
        foreach (var cert in all)
            await certs.DeleteAsync(cert.Id);

        // Recreate root CA
        var newCa = await ca.GetOrCreateRootCaAsync();

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "RegenerateRootCA",
            EntityType = "CertificateEntry",
            EntityId = newCa.Id.ToString(),
            Details = "Regenerated root CA and purged all leaf certificates",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new { message = "Root CA regenerated", commonName = newCa.CommonName, id = newCa.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await certs.DeleteAsync(id);
        return NoContent();
    }

    [HttpDelete("purge-leaf-certs")]
    public async Task<IActionResult> PurgeLeafCerts()
    {
        var all = await certs.GetAllAsync();
        var leafCerts = all.Where(c => !c.IsRootCa).ToList();
        foreach (var cert in leafCerts)
            await certs.DeleteAsync(cert.Id);
        return Ok(new { deleted = leafCerts.Count });
    }
}
```

- [ ] **Step 4: Run certificate tests**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "CertificatesControllerTests" 2>&1 | tail -10
```

Expected: both tests pass.

- [ ] **Step 5: Run full backend test suite to confirm no regressions**

```bash
dotnet test src/ 2>&1 | tail -5
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/IoTSpy.Api/Controllers/CertificatesController.cs \
        src/IoTSpy.Api.IntegrationTests/CertificatesControllerTests.cs
git commit -m "feat: add regenerate root CA endpoint with audit logging"
```

---

## Task 4: AuthController — safety guards on user delete and update

**Files:**
- Modify: `src/IoTSpy.Api/Controllers/AuthController.cs`
- Modify: `src/IoTSpy.Api.IntegrationTests/AuthEndpointTests.cs`

- [ ] **Step 1: Add failing tests for safety guards**

Append to the test class in `src/IoTSpy.Api.IntegrationTests/AuthEndpointTests.cs`:

```csharp
    [Fact]
    public async Task DeleteUser_Self_Returns400()
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
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.DeleteAsync($"/api/auth/users/{userId}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_LastAdmin_Returns400()
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
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Create a second non-admin user, then try to delete the last admin
        await client.PostAsJsonAsync("/api/auth/users",
            new { username = "viewer1", password = "pass123", role = "viewer" });

        var resp = await client.DeleteAsync($"/api/auth/users/{userId}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_DemoteLastAdmin_Returns400()
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
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PutAsJsonAsync($"/api/auth/users/{userId}",
            new { role = "viewer" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
```

- [ ] **Step 2: Run new tests to confirm they fail**

```bash
dotnet test src/IoTSpy.Api.IntegrationTests/ --filter "DeleteUser_Self|DeleteUser_LastAdmin|UpdateUser_DemoteLastAdmin" 2>&1 | tail -15
```

Expected: all 3 FAIL (guards not yet implemented).

- [ ] **Step 3: Add safety guards to `DeleteUser` in AuthController**

In `src/IoTSpy.Api/Controllers/AuthController.cs`, replace the `DeleteUser` method body:

```csharp
    [Authorize(Roles = "admin")]
    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();

        // Cannot delete your own account
        if (User.Identity?.Name == user.Username)
            return BadRequest(new { error = "Cannot delete your own account" });

        // Cannot delete the last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = (await userRepo.GetAllAsync()).Count(u => u.Role == UserRole.Admin && u.IsEnabled);
            if (adminCount <= 1)
                return BadRequest(new { error = "Cannot delete the last admin account" });
        }

        await userRepo.DeleteAsync(id);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "DeleteUser",
            EntityType = "User",
            EntityId = id.ToString(),
            Details = $"Deleted user '{user.Username}'",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return NoContent();
    }
```

- [ ] **Step 4: Add safety guard to `UpdateUser` in AuthController**

In `AuthController.cs`, add a check at the top of the `UpdateUser` method body (before the existing property assignments):

```csharp
    [Authorize(Roles = "admin")]
    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound();

        // Cannot demote the last admin
        if (req.Role.HasValue && req.Role.Value != UserRole.Admin && user.Role == UserRole.Admin)
        {
            var adminCount = (await userRepo.GetAllAsync()).Count(u => u.Role == UserRole.Admin && u.IsEnabled);
            if (adminCount <= 1)
                return BadRequest(new { error = "Cannot demote the last admin account" });
        }

        if (req.DisplayName is not null) user.DisplayName = req.DisplayName;
        if (req.Role.HasValue) user.Role = req.Role.Value;
        if (req.IsEnabled.HasValue) user.IsEnabled = req.IsEnabled.Value;
        if (!string.IsNullOrEmpty(req.Password)) user.PasswordHash = auth.HashPassword(req.Password);

        await userRepo.UpdateAsync(user);

        await auditRepo.AddAsync(new AuditEntry
        {
            Username = User.Identity?.Name ?? "system",
            Action = "UpdateUser",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Details = $"Updated user '{user.Username}'",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
        });

        return Ok(new
        {
            user.Id, user.Username, user.DisplayName,
            role = user.Role.ToString().ToLowerInvariant(),
            user.IsEnabled, user.CreatedAt, user.LastLoginAt
        });
    }
```

- [ ] **Step 5: Run full test suite**

```bash
dotnet test src/ 2>&1 | tail -5
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/IoTSpy.Api/Controllers/AuthController.cs \
        src/IoTSpy.Api.IntegrationTests/AuthEndpointTests.cs
git commit -m "fix: prevent self-delete and last-admin demotion/deletion"
```

---

## Task 5: Frontend — types, useCurrentUser hook, login stores user info

**Files:**
- Modify: `frontend/src/types/api.ts`
- Modify: `frontend/src/hooks/useAuth.ts`
- Modify: `frontend/src/pages/LoginPage.tsx`

- [ ] **Step 1: Add AdminStats and UserSummary types to api.ts**

Open `frontend/src/types/api.ts` and append at the end of the file:

```typescript
// ── Admin ─────────────────────────────────────────────────────────────────────

export interface AdminDataStats {
  count: number
  estimatedSizeBytes: number
  oldestTimestamp: string | null
}

export interface AdminStats {
  captures: AdminDataStats
  packets: AdminDataStats
  scanFindings: { count: number }
}

export interface UserSummary {
  id: string
  username: string
  displayName: string
  role: string        // 'admin' | 'operator' | 'viewer'
  isEnabled: boolean
  createdAt: string
  lastLoginAt: string | null
}

export interface CurrentUser {
  id: string
  username: string
  displayName: string
  role: string
}
```

- [ ] **Step 2: Store user info on login in LoginPage.tsx**

Open `frontend/src/pages/LoginPage.tsx`. Find the code that calls the login API and handles a successful response (look for where the token is stored — typically `localStorage.setItem`). After the line that stores the token, add:

```typescript
// Store user info for role-based UI gating
if (data.user) {
  localStorage.setItem('iotspy-user', JSON.stringify(data.user))
}
```

Also ensure the logout handler clears it. Find `frontend/src/hooks/useAuth.ts` and in the logout function add:

```typescript
localStorage.removeItem('iotspy-user')
```

- [ ] **Step 3: Add useCurrentUser to useAuth.ts**

Open `frontend/src/hooks/useAuth.ts` and add this export (before or after the existing exports):

```typescript
import type { CurrentUser } from '../types/api'

export function useCurrentUser(): CurrentUser | null {
  const raw = localStorage.getItem('iotspy-user')
  if (!raw) return null
  try {
    return JSON.parse(raw) as CurrentUser
  } catch {
    return null
  }
}
```

- [ ] **Step 4: Verify frontend builds**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

Expected: build succeeds with no TypeScript errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/types/api.ts frontend/src/hooks/useAuth.ts frontend/src/pages/LoginPage.tsx
git commit -m "feat: store current user info on login, expose useCurrentUser hook"
```

---

## Task 6: AdminPage scaffold, Header link, /admin route

**Files:**
- Create: `frontend/src/pages/AdminPage.tsx`
- Create: `frontend/src/styles/admin.css`
- Modify: `frontend/src/components/layout/Header.tsx`
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: Create admin.css**

Create `frontend/src/styles/admin.css`:

```css
/* ── Admin page ─────────────────────────────────────────────────────────────── */

.admin-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.admin-header {
  padding: var(--space-3) var(--space-4);
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}

.admin-header h1 {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: 600;
  color: var(--color-text);
}

/* ── Tabs ───────────────────────────────────────────────────────────────────── */

.admin-tabs {
  display: flex;
  gap: 2px;
  padding: var(--space-2) var(--space-4) 0;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}

.admin-tab {
  padding: var(--space-1) var(--space-3);
  font-size: var(--font-size-sm);
  font-weight: 500;
  color: var(--color-text-muted);
  background: transparent;
  border: 1px solid transparent;
  border-bottom: none;
  border-radius: var(--radius-sm) var(--radius-sm) 0 0;
  cursor: pointer;
  transition: color 0.15s, background 0.15s;
}

.admin-tab:hover {
  color: var(--color-text);
  background: var(--color-surface-2);
}

.admin-tab--active {
  color: var(--color-text);
  background: var(--color-surface-2);
  border-color: var(--color-border);
}

.admin-content {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-4);
}

/* ── Stat cards ─────────────────────────────────────────────────────────────── */

.admin-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: var(--space-4);
}

.admin-card {
  background: var(--color-surface-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-4);
}

.admin-card__title {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text);
  margin: 0 0 var(--space-2);
}

.admin-card__stats {
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
  margin-bottom: var(--space-3);
  line-height: 1.6;
}

.admin-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

/* ── Buttons ───────────────────────────────────────────────────────────────── */

.admin-btn {
  padding: var(--space-1) var(--space-3);
  font-size: var(--font-size-xs);
  font-weight: 500;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text);
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
}

.admin-btn:hover {
  background: var(--color-surface-3);
  border-color: var(--color-text-muted);
}

.admin-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.admin-btn--danger {
  border-color: var(--color-error, #e53e3e);
  color: var(--color-error, #e53e3e);
}

.admin-btn--danger:hover {
  background: rgba(229, 62, 62, 0.1);
}

.admin-btn--primary {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: #fff;
}

.admin-btn--primary:hover {
  opacity: 0.9;
}

/* ── Tables ────────────────────────────────────────────────────────────────── */

.admin-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.admin-table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--font-size-xs);
}

.admin-table th {
  padding: var(--space-2) var(--space-3);
  text-align: left;
  font-weight: 600;
  color: var(--color-text-muted);
  background: var(--color-surface-2);
  border-bottom: 1px solid var(--color-border);
  white-space: nowrap;
}

.admin-table td {
  padding: var(--space-2) var(--space-3);
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text);
  font-family: var(--font-mono);
}

.admin-table tr:last-child td {
  border-bottom: none;
}

.admin-table tr:hover td {
  background: var(--color-surface-2);
}

/* ── Role badges ───────────────────────────────────────────────────────────── */

.role-badge {
  display: inline-block;
  padding: 1px var(--space-2);
  font-size: 10px;
  font-weight: 600;
  border-radius: var(--radius-sm);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.role-badge--admin   { background: rgba(66, 153, 225, 0.15); color: #4299e1; }
.role-badge--operator { background: rgba(72, 187, 120, 0.15); color: #48bb78; }
.role-badge--viewer  { background: var(--color-surface-3); color: var(--color-text-muted); }

/* ── Section headings ──────────────────────────────────────────────────────── */

.admin-section-title {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--color-text);
  margin: 0 0 var(--space-3);
}

.admin-section + .admin-section {
  margin-top: var(--space-6);
}

/* ── Pagination ────────────────────────────────────────────────────────────── */

.admin-pagination {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-3);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
}

/* ── Confirm dialog overlay ────────────────────────────────────────────────── */

.admin-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;
}

.admin-dialog {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-5);
  max-width: 440px;
  width: 90%;
}

.admin-dialog h3 {
  margin: 0 0 var(--space-2);
  font-size: var(--font-size-md);
}

.admin-dialog p {
  margin: 0 0 var(--space-4);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.admin-dialog__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
}
```

- [ ] **Step 2: Create AdminPage.tsx with tab scaffold**

Create `frontend/src/pages/AdminPage.tsx`:

```tsx
import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import AppShell from '../components/layout/AppShell'
import Header from '../components/layout/Header'
import DatabaseTab from '../components/admin/DatabaseTab'
import CertificatesTab from '../components/admin/CertificatesTab'
import AuditLogTab from '../components/admin/AuditLogTab'
import UsersTab from '../components/admin/UsersTab'
import { useCurrentUser } from '../hooks/useAuth'
import { useProxy } from '../hooks/useProxy'
import { useTheme } from '../hooks/useTheme'
import '../styles/admin.css'

type AdminTab = 'database' | 'certificates' | 'audit' | 'users'

const TABS: { key: AdminTab; label: string }[] = [
  { key: 'database',     label: 'Database' },
  { key: 'certificates', label: 'Certificates' },
  { key: 'audit',        label: 'Audit Log' },
  { key: 'users',        label: 'Users' },
]

export default function AdminPage() {
  const currentUser = useCurrentUser()
  const [activeTab, setActiveTab] = useState<AdminTab>('database')
  const proxy = useProxy()
  const { theme, toggle: toggleTheme } = useTheme()

  // Redirect non-admins back to dashboard
  if (!currentUser || currentUser.role !== 'admin') {
    return <Navigate to="/" replace />
  }

  const proxyStatus = proxy.status
  const isRunning = proxyStatus?.isRunning ?? false
  const port = proxyStatus?.port ?? proxyStatus?.settings?.proxyPort ?? 8888

  return (
    <AppShell
      header={
        <Header
          isRunning={isRunning}
          port={port}
          settings={proxyStatus?.settings ?? null}
          signalRConnected={false}
          loading={proxy.loading}
          theme={theme}
          onStart={proxy.start}
          onStop={proxy.stop}
          onSaveSettings={proxy.saveSettings}
          onToggleTheme={toggleTheme}
        />
      }
    >
      <div className="admin-page">
        <div className="admin-header">
          <h1>System Administration</h1>
        </div>

        <div className="admin-tabs">
          {TABS.map(tab => (
            <button
              key={tab.key}
              className={`admin-tab${activeTab === tab.key ? ' admin-tab--active' : ''}`}
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="admin-content">
          {activeTab === 'database'     && <DatabaseTab />}
          {activeTab === 'certificates' && <CertificatesTab />}
          {activeTab === 'audit'        && <AuditLogTab />}
          {activeTab === 'users'        && <UsersTab currentUsername={currentUser.username} />}
        </div>
      </div>
    </AppShell>
  )
}
```

- [ ] **Step 3: Create stub components so the page compiles**

Create `frontend/src/components/admin/DatabaseTab.tsx`:
```tsx
export default function DatabaseTab() {
  return <div>Database tab — coming soon</div>
}
```

Create `frontend/src/components/admin/CertificatesTab.tsx`:
```tsx
export default function CertificatesTab() {
  return <div>Certificates tab — coming soon</div>
}
```

Create `frontend/src/components/admin/AuditLogTab.tsx`:
```tsx
export default function AuditLogTab() {
  return <div>Audit log tab — coming soon</div>
}
```

Create `frontend/src/components/admin/UsersTab.tsx`:
```tsx
interface Props { currentUsername: string }
export default function UsersTab({ currentUsername: _ }: Props) {
  return <div>Users tab — coming soon</div>
}
```

- [ ] **Step 4: Add /admin route to App.tsx**

Open `frontend/src/App.tsx`. Import `AdminPage` lazily alongside the other lazy imports:

```tsx
const AdminPage = lazy(() => import('./pages/AdminPage'))
```

Add the route before the catch-all `/*` route:

```tsx
<Route path="/admin" element={<AdminPage />} />
<Route path="/*" element={<DashboardPage />} />
```

- [ ] **Step 5: Add admin link to Header.tsx**

Open `frontend/src/components/layout/Header.tsx`. Import the hook and `Link`:

```tsx
import { Link } from 'react-router-dom'
import { useCurrentUser } from '../../hooks/useAuth'
```

Inside the component, call the hook near the top (after `useLogout`):

```tsx
const currentUser = useCurrentUser()
```

In the `header__actions` div, add the admin link before the theme toggle button, shown only for admins:

```tsx
{currentUser?.role === 'admin' && (
  <Link
    className="header__btn header__btn--icon"
    to="/admin"
    title="System administration"
    aria-label="Admin"
  >
    &#x1F527;{/* 🔧 */}
  </Link>
)}
```

- [ ] **Step 6: Build to confirm no TypeScript errors**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/pages/AdminPage.tsx \
        frontend/src/components/admin/ \
        frontend/src/styles/admin.css \
        frontend/src/App.tsx \
        frontend/src/components/layout/Header.tsx
git commit -m "feat: add /admin route with tab scaffold and header link"
```

---

## Task 7: DatabaseTab component

**Files:**
- Modify: `frontend/src/components/admin/DatabaseTab.tsx`

- [ ] **Step 1: Implement DatabaseTab**

Replace `frontend/src/components/admin/DatabaseTab.tsx` with:

```tsx
import { useState, useEffect, useCallback } from 'react'
import type { AdminStats } from '../../types/api'
import { apiGet, apiDelete } from '../../api/client'

interface ConfirmState {
  title: string
  message: string
  onConfirm: () => Promise<void>
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString()
}

export default function DatabaseTab() {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<ConfirmState | null>(null)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  // Purge form state
  const [captureDays, setCaptureDays] = useState(30)
  const [captureHost, setCaptureHost] = useState('')
  const [packetDays, setPacketDays] = useState(30)

  const loadStats = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await apiGet<AdminStats>('/api/admin/stats')
      setStats(data)
    } catch {
      setError('Failed to load stats')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { loadStats() }, [loadStats])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const runWithConfirm = (title: string, message: string, action: () => Promise<void>) => {
    setConfirm({ title, message, onConfirm: action })
  }

  const purgeCaptures = async (params: string) => {
    setBusy(true)
    try {
      const result = await apiDelete<{ deleted: number }>(`/api/admin/captures?${params}`)
      showToast(`Deleted ${result.deleted} captures`)
      await loadStats()
    } finally {
      setBusy(false)
    }
  }

  const purgePackets = async (params: string) => {
    setBusy(true)
    try {
      const result = await apiDelete<{ deleted: number }>(`/api/admin/packets?${params}`)
      showToast(`Deleted ${result.deleted} packets`)
      await loadStats()
    } finally {
      setBusy(false)
    }
  }

  const downloadExport = (url: string, filename: string) => {
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    a.click()
  }

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading stats…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      {toast && (
        <div style={{
          position: 'fixed', top: 'var(--space-4)', right: 'var(--space-4)',
          background: 'var(--color-surface-2)', border: '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)', padding: 'var(--space-2) var(--space-4)',
          zIndex: 300, fontSize: 'var(--font-size-sm)'
        }}>
          {toast}
        </div>
      )}

      <div className="admin-cards">
        {/* ── Captures & Logs ── */}
        <div className="admin-card">
          <div className="admin-card__title">Captures &amp; Logs</div>
          <div className="admin-card__stats">
            {stats!.captures.count.toLocaleString()} rows
            &nbsp;·&nbsp; ~{formatBytes(stats!.captures.estimatedSizeBytes)}
            &nbsp;·&nbsp; oldest: {formatDate(stats!.captures.oldestTimestamp)}
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge older than {captureDays} days
            </label>
            <input type="range" min={1} max={365} value={captureDays}
              onChange={e => setCaptureDays(+e.target.value)}
              style={{ width: '100%', marginBottom: 4 }} />
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge old captures',
                `Delete all captures older than ${captureDays} days?`,
                () => purgeCaptures(`olderThanDays=${captureDays}`)
              )}>Purge by age</button>
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge by host
            </label>
            <div style={{ display: 'flex', gap: 'var(--space-1)' }}>
              <input className="mock-input" placeholder="e.g. api.example.com" value={captureHost}
                onChange={e => setCaptureHost(e.target.value)}
                style={{ flex: 1, fontSize: 'var(--font-size-xs)' }} />
              <button className="admin-btn admin-btn--danger" disabled={busy || !captureHost.trim()} onClick={() =>
                runWithConfirm(
                  'Purge captures by host',
                  `Delete all captures for host "${captureHost}"?`,
                  () => purgeCaptures(`host=${encodeURIComponent(captureHost)}`)
                )}>Purge</button>
            </div>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all captures',
                `Delete ALL ${stats!.captures.count.toLocaleString()} captures? This cannot be undone.`,
                () => purgeCaptures('purgeAll=true')
              )}>Purge all</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/logs?format=json', 'captures.json')}>Export JSON</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/logs?format=csv', 'captures.csv')}>Export CSV</button>
          </div>
        </div>

        {/* ── Packets ── */}
        <div className="admin-card">
          <div className="admin-card__title">Packets</div>
          <div className="admin-card__stats">
            {stats!.packets.count.toLocaleString()} rows
            &nbsp;·&nbsp; ~{formatBytes(stats!.packets.estimatedSizeBytes)}
            &nbsp;·&nbsp; oldest: {formatDate(stats!.packets.oldestTimestamp)}
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge older than {packetDays} days
            </label>
            <input type="range" min={1} max={365} value={packetDays}
              onChange={e => setPacketDays(+e.target.value)}
              style={{ width: '100%', marginBottom: 4 }} />
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge old packets',
                `Delete all packets older than ${packetDays} days?`,
                () => purgePackets(`olderThanDays=${packetDays}`)
              )}>Purge by age</button>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all packets',
                `Delete ALL ${stats!.packets.count.toLocaleString()} packets?`,
                () => purgePackets('purgeAll=true')
              )}>Purge all</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/packets?format=json', 'packets.json')}>Export JSON</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/packets?format=csv', 'packets.csv')}>Export CSV</button>
          </div>
        </div>

        {/* ── Configuration ── */}
        <div className="admin-card">
          <div className="admin-card__title">Configuration</div>
          <div className="admin-card__stats">
            Rules, breakpoints, scheduled scans, OpenRTB policies, API specs
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--primary"
              onClick={() => downloadExport('/api/admin/export/config', 'iotspy-config.json')}>
              Export JSON
            </button>
          </div>
        </div>
      </div>

      {/* Confirm dialog */}
      {confirm && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>{confirm.title}</h3>
            <p>{confirm.message}</p>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirm(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={async () => {
                await confirm.onConfirm()
                setConfirm(null)
              }}>Confirm</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
```

> **Note:** `apiGet`, `apiDelete` are assumed to be the existing API client helpers. If the project uses a different pattern (e.g. direct `fetch` with auth headers), match that pattern. Look at how other components call the API — typically in `frontend/src/api/client.ts` or equivalent — and use the same approach.

- [ ] **Step 2: Build**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/admin/DatabaseTab.tsx
git commit -m "feat: implement DatabaseTab with stats, purge, and export"
```

---

## Task 8: CertificatesTab component

**Files:**
- Modify: `frontend/src/components/admin/CertificatesTab.tsx`

- [ ] **Step 1: Implement CertificatesTab**

Replace `frontend/src/components/admin/CertificatesTab.tsx` with:

```tsx
import { useState, useEffect } from 'react'
import type { CertificateEntry } from '../../types/api'

interface RootCaInfo {
  id: string
  commonName: string
  serialNumber: string
  notBefore: string
  notAfter: string
  certificatePem: string
}

export default function CertificatesTab() {
  const [rootCa, setRootCa] = useState<RootCaInfo | null>(null)
  const [leafCerts, setLeafCerts] = useState<CertificateEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<'regenerate' | 'purge-leaf' | null>(null)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  const token = localStorage.getItem('iotspy-token') ?? ''
  const headers = { Authorization: `Bearer ${token}` }

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const [caResp, allResp] = await Promise.all([
        fetch('/api/certificates/root-ca', { headers }),
        fetch('/api/certificates', { headers }),
      ])
      if (!caResp.ok) throw new Error('Failed to load CA')
      const ca = await caResp.json()
      const all: CertificateEntry[] = await allResp.json()
      setRootCa(ca)
      setLeafCerts(all.filter((c: CertificateEntry) => !c.isRootCa).slice(0, 50))
    } catch {
      setError('Failed to load certificate data')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const regenerateCa = async () => {
    setBusy(true)
    try {
      const resp = await fetch('/api/certificates/root-ca/regenerate', { method: 'POST', headers })
      if (!resp.ok) throw new Error('Regenerate failed')
      showToast('Root CA regenerated successfully')
      await load()
    } catch {
      showToast('Failed to regenerate CA')
    } finally {
      setBusy(false)
    }
  }

  const purgeLeaf = async () => {
    setBusy(true)
    try {
      const resp = await fetch('/api/certificates/purge-leaf-certs', { method: 'DELETE', headers })
      const data = await resp.json()
      showToast(`Purged ${data.deleted} leaf certificates`)
      await load()
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  const sha256 = (pem: string) => {
    // Display fingerprint hint from PEM serial only (full SHA-256 requires crypto)
    return rootCa?.serialNumber ?? '—'
  }

  return (
    <>
      {toast && (
        <div style={{
          position: 'fixed', top: 'var(--space-4)', right: 'var(--space-4)',
          background: 'var(--color-surface-2)', border: '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)', padding: 'var(--space-2) var(--space-4)',
          zIndex: 300, fontSize: 'var(--font-size-sm)'
        }}>{toast}</div>
      )}

      {/* Root CA section */}
      <div className="admin-section">
        <div className="admin-section-title">Root Certificate Authority</div>
        <div className="admin-card" style={{ maxWidth: 560 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--font-size-xs)' }}>
            <tbody>
              {[
                ['Common Name', rootCa?.commonName],
                ['Serial Number', rootCa?.serialNumber],
                ['Valid From', rootCa ? new Date(rootCa.notBefore).toLocaleString() : '—'],
                ['Expires', rootCa ? new Date(rootCa.notAfter).toLocaleString() : '—'],
              ].map(([label, value]) => (
                <tr key={label as string}>
                  <td style={{ padding: '4px 0', color: 'var(--color-text-muted)', width: 120 }}>{label}</td>
                  <td style={{ padding: '4px 0', fontFamily: 'var(--font-mono)' }}>{value}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="admin-card__actions" style={{ marginTop: 'var(--space-3)' }}>
            <a className="admin-btn" href="/api/certificates/root-ca/download" download>Download DER</a>
            <a className="admin-btn" href="/api/certificates/root-ca/pem" download>Download PEM</a>
            <button className="admin-btn admin-btn--danger" disabled={busy}
              onClick={() => setConfirm('regenerate')}>
              Regenerate CA
            </button>
          </div>
        </div>
      </div>

      {/* Leaf certs section */}
      <div className="admin-section">
        <div className="admin-section-title">
          Leaf Certificates
          <span style={{ marginLeft: 'var(--space-2)', fontWeight: 400, color: 'var(--color-text-muted)' }}>
            ({leafCerts.length} shown)
          </span>
        </div>
        <div style={{ marginBottom: 'var(--space-3)' }}>
          <button className="admin-btn admin-btn--danger" disabled={busy}
            onClick={() => setConfirm('purge-leaf')}>
            Purge all leaf certs
          </button>
        </div>
        {leafCerts.length > 0 && (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Host</th>
                  <th>Issued</th>
                  <th>Expires</th>
                </tr>
              </thead>
              <tbody>
                {leafCerts.map(c => (
                  <tr key={c.id}>
                    <td>{c.commonName}</td>
                    <td>{new Date(c.notBefore).toLocaleDateString()}</td>
                    <td>{new Date(c.notAfter).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Confirm dialog */}
      {confirm && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            {confirm === 'regenerate' ? (
              <>
                <h3>Regenerate Root CA?</h3>
                <p>This will invalidate all existing leaf certificates and require re-installing the root CA on all devices.</p>
              </>
            ) : (
              <>
                <h3>Purge all leaf certificates?</h3>
                <p>All {leafCerts.length} leaf certificates will be deleted. They will be regenerated on next use.</p>
              </>
            )}
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirm(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={async () => {
                if (confirm === 'regenerate') await regenerateCa()
                else await purgeLeaf()
                setConfirm(null)
              }}>Confirm</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
```

- [ ] **Step 2: Build**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/admin/CertificatesTab.tsx
git commit -m "feat: implement CertificatesTab with CA details, download, regenerate"
```

---

## Task 9: AuditLogTab component

**Files:**
- Modify: `frontend/src/components/admin/AuditLogTab.tsx`

- [ ] **Step 1: Implement AuditLogTab**

Replace `frontend/src/components/admin/AuditLogTab.tsx` with:

```tsx
import { useState, useEffect, useCallback } from 'react'
import type { AuditEntry } from '../../types/api'

const PAGE_SIZE = 50

export default function AuditLogTab() {
  const [entries, setEntries] = useState<AuditEntry[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const token = localStorage.getItem('iotspy-token') ?? ''

  const load = useCallback(async (p: number) => {
    setLoading(true)
    setError(null)
    try {
      const resp = await fetch(`/api/auth/audit?count=${PAGE_SIZE * p}`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (!resp.ok) throw new Error('Failed to load audit log')
      const all: AuditEntry[] = await resp.json()
      setTotal(all.length)
      // Paginate client-side (audit endpoint returns most recent N entries)
      const start = (p - 1) * PAGE_SIZE
      setEntries(all.slice(start, start + PAGE_SIZE))
    } catch {
      setError('Failed to load audit log')
    } finally {
      setLoading(false)
    }
  }, [token])

  useEffect(() => { load(page) }, [load, page])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Timestamp</th>
              <th>User</th>
              <th>Action</th>
              <th>Entity</th>
              <th>Details</th>
              <th>IP Address</th>
            </tr>
          </thead>
          <tbody>
            {entries.length === 0 ? (
              <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--color-text-faint)' }}>No entries</td></tr>
            ) : entries.map(e => (
              <tr key={e.id}>
                <td>{new Date(e.timestamp).toLocaleString()}</td>
                <td>{e.username}</td>
                <td>{e.action}</td>
                <td>{e.entityType}{e.entityId ? ` / ${e.entityId.slice(0, 8)}…` : ''}</td>
                <td style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {e.details ?? '—'}
                </td>
                <td>{e.ipAddress}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-pagination">
        <button className="admin-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Prev</button>
        <span>Page {page} of {totalPages}</span>
        <button className="admin-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next →</button>
      </div>
    </>
  )
}
```

> **Note:** `AuditEntry` must have `id`, `timestamp`, `username`, `action`, `entityType`, `entityId`, `details`, `ipAddress` fields. Check `frontend/src/types/api.ts` — if `AuditEntry` is missing any of these, add them.

- [ ] **Step 2: Build**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/admin/AuditLogTab.tsx
git commit -m "feat: implement AuditLogTab with paginated audit entries"
```

---

## Task 10: UsersTab component

**Files:**
- Modify: `frontend/src/components/admin/UsersTab.tsx`

- [ ] **Step 1: Implement UsersTab**

Replace `frontend/src/components/admin/UsersTab.tsx` with:

```tsx
import { useState, useEffect, useCallback } from 'react'
import type { UserSummary } from '../../types/api'

interface Props {
  currentUsername: string
}

const ROLES = ['admin', 'operator', 'viewer'] as const

export default function UsersTab({ currentUsername }: Props) {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<UserSummary | null>(null)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [newUser, setNewUser] = useState({ username: '', password: '', displayName: '', role: 'viewer' as string })

  const token = localStorage.getItem('iotspy-token') ?? ''
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const resp = await fetch('/api/auth/users', { headers: { Authorization: `Bearer ${token}` } })
      if (!resp.ok) throw new Error('Failed to load users')
      setUsers(await resp.json())
    } catch {
      setError('Failed to load users')
    } finally {
      setLoading(false)
    }
  }, [token])

  useEffect(() => { load() }, [load])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const updateRole = async (user: UserSummary, role: string) => {
    setBusy(true)
    try {
      const resp = await fetch(`/api/auth/users/${user.id}`, {
        method: 'PUT',
        headers,
        body: JSON.stringify({ role }),
      })
      if (!resp.ok) {
        const err = await resp.json()
        showToast(err.error ?? 'Failed to update role')
      } else {
        showToast(`Updated ${user.username} to ${role}`)
        await load()
      }
    } finally {
      setBusy(false)
    }
  }

  const deleteUser = async (user: UserSummary) => {
    setBusy(true)
    try {
      const resp = await fetch(`/api/auth/users/${user.id}`, { method: 'DELETE', headers })
      if (!resp.ok) {
        const err = await resp.json()
        showToast(err.error ?? 'Failed to delete user')
      } else {
        showToast(`Deleted user ${user.username}`)
        await load()
      }
    } finally {
      setBusy(false)
      setConfirmDelete(null)
    }
  }

  const createUser = async () => {
    setBusy(true)
    try {
      const resp = await fetch('/api/auth/users', {
        method: 'POST',
        headers,
        body: JSON.stringify(newUser),
      })
      if (!resp.ok) {
        const err = await resp.json()
        showToast(err.error ?? 'Failed to create user')
      } else {
        showToast(`Created user ${newUser.username}`)
        setShowCreate(false)
        setNewUser({ username: '', password: '', displayName: '', role: 'viewer' })
        await load()
      }
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      {toast && (
        <div style={{
          position: 'fixed', top: 'var(--space-4)', right: 'var(--space-4)',
          background: 'var(--color-surface-2)', border: '1px solid var(--color-border)',
          borderRadius: 'var(--radius-md)', padding: 'var(--space-2) var(--space-4)',
          zIndex: 300, fontSize: 'var(--font-size-sm)'
        }}>{toast}</div>
      )}

      <div style={{ marginBottom: 'var(--space-3)' }}>
        <button className="admin-btn admin-btn--primary" onClick={() => setShowCreate(true)}>
          + Add user
        </button>
      </div>

      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Display Name</th>
              <th>Role</th>
              <th>Created</th>
              <th>Last Login</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map(u => (
              <tr key={u.id}>
                <td>{u.username}{u.username === currentUsername && ' (you)'}</td>
                <td>{u.displayName}</td>
                <td>
                  <select
                    value={u.role}
                    disabled={busy}
                    style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '2px 4px' }}
                    onChange={e => updateRole(u, e.target.value)}
                  >
                    {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                  </select>
                </td>
                <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : '—'}</td>
                <td>
                  {u.username !== currentUsername && (
                    <button className="admin-btn admin-btn--danger"
                      style={{ padding: '1px 8px' }}
                      disabled={busy}
                      onClick={() => setConfirmDelete(u)}>
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create user dialog */}
      {showCreate && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>Create User</h3>
            {(['username', 'password', 'displayName'] as const).map(field => (
              <div key={field} style={{ marginBottom: 'var(--space-2)' }}>
                <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>
                  {field === 'displayName' ? 'Display Name' : field.charAt(0).toUpperCase() + field.slice(1)}
                </label>
                <input
                  type={field === 'password' ? 'password' : 'text'}
                  value={newUser[field]}
                  onChange={e => setNewUser(prev => ({ ...prev, [field]: e.target.value }))}
                  style={{ width: '100%', fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}
                />
              </div>
            ))}
            <div style={{ marginBottom: 'var(--space-4)' }}>
              <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>Role</label>
              <select value={newUser.role} onChange={e => setNewUser(prev => ({ ...prev, role: e.target.value }))}
                style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}>
                {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
              </select>
            </div>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setShowCreate(false)}>Cancel</button>
              <button className="admin-btn admin-btn--primary" disabled={busy || !newUser.username || !newUser.password} onClick={createUser}>Create</button>
            </div>
          </div>
        </div>
      )}

      {/* Delete confirm dialog */}
      {confirmDelete && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>Delete user?</h3>
            <p>Delete account <strong>{confirmDelete.username}</strong>? This cannot be undone.</p>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirmDelete(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() => deleteUser(confirmDelete)}>Delete</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
```

- [ ] **Step 2: Build and run all backend tests**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -5
cd /home/anna/git/codify/IoTSpy && dotnet test src/ 2>&1 | tail -5
```

Expected: both succeed.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/admin/UsersTab.tsx
git commit -m "feat: implement UsersTab with user CRUD and role management"
```

---

## Task 11: BodyViewer — SSE/NDJSON stream detection, collapsible events, and CSS

**Files:**
- Modify: `frontend/src/components/common/BodyViewer.tsx`
- Modify: `frontend/src/styles/body-viewer.css`

- [ ] **Step 1: Add stream event CSS to body-viewer.css**

Append to `frontend/src/styles/body-viewer.css`:

```css
/* ── Stream / SSE / NDJSON event rows ─────────────────────────────────────── */

.bv-stream-toolbar {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.bv-badge--events {
  background: rgba(66, 153, 225, 0.15);
  color: #4299e1;
}

.bv-stream-toggle {
  margin-left: auto;
  padding: 2px var(--space-2);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.1s, color 0.1s;
  flex-shrink: 0;
}

.bv-stream-toggle:hover {
  background: var(--color-surface-3);
  color: var(--color-text);
}

.bv-stream-events {
  padding: var(--space-2);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.bv-event {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  overflow: hidden;
}

.bv-event__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: 4px var(--space-2);
  cursor: pointer;
  user-select: none;
  background: var(--color-surface-2);
  transition: background 0.1s;
}

.bv-event__header:hover {
  background: var(--color-surface-3);
}

.bv-event__chevron {
  font-size: 10px;
  color: var(--color-text-faint);
  flex-shrink: 0;
  transition: transform 0.15s;
}

.bv-event__chevron--open {
  transform: rotate(90deg);
}

.bv-event__index {
  font-size: 10px;
  font-family: var(--font-mono);
  color: var(--color-text-faint);
  flex-shrink: 0;
  min-width: 28px;
}

.bv-event__label {
  font-size: var(--font-size-xs);
  font-weight: 500;
  color: var(--color-text);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bv-event__size {
  font-size: 10px;
  font-family: var(--font-mono);
  color: var(--color-text-faint);
  flex-shrink: 0;
}

.bv-event__meta {
  padding: 4px var(--space-2);
  font-size: 10px;
  font-family: var(--font-mono);
  color: var(--color-text-muted);
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
}

.bv-event__body {
  padding: var(--space-2) var(--space-3);
  font-family: var(--font-mono);
  font-size: var(--font-size-sm);
  white-space: pre-wrap;
  word-break: break-all;
  background: var(--color-surface);
  line-height: 1.5;
}
```

- [ ] **Step 2: Add stream parsing and rendering to BodyViewer.tsx**

Add the following pure functions **before** the `resolvePretty` function in `BodyViewer.tsx` (around line 115):

```typescript
// ─── Stream / SSE / NDJSON parsing ──────────────────────────────────────────

interface StreamEvent {
  index: number
  label: string
  rawBytes: number
  jsonHtml: string | null  // null = non-JSON or plain text
  plainText: string | null // raw text when not JSON
  meta: Record<string, string> // SSE fields: event, id, retry
}

function parseSSE(body: string): StreamEvent[] {
  const blocks = body.split(/\n\n+/)
  const events: StreamEvent[] = []
  let index = 0
  for (const block of blocks) {
    if (!block.trim()) continue
    const lines = block.split(/\r?\n/)
    const meta: Record<string, string> = {}
    let dataLines: string[] = []
    for (const line of lines) {
      if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trim())
      } else if (line.startsWith('event:')) {
        meta['event'] = line.slice(6).trim()
      } else if (line.startsWith('id:')) {
        meta['id'] = line.slice(3).trim()
      } else if (line.startsWith('retry:')) {
        meta['retry'] = line.slice(6).trim()
      }
    }
    if (dataLines.length === 0) continue
    const dataStr = dataLines.join('\n')
    let jsonHtml: string | null = null
    let label = meta['event'] ?? ''
    try {
      const parsed = JSON.parse(dataStr)
      jsonHtml = highlightJson(JSON.stringify(parsed, null, 2))
      if (!label) {
        const firstStringKey = Object.keys(parsed).find(k => typeof parsed[k] === 'string')
        if (firstStringKey) label = String(parsed[firstStringKey]).slice(0, 40)
      }
    } catch {
      // not JSON
    }
    events.push({
      index: index++,
      label: label || `event ${index}`,
      rawBytes: new TextEncoder().encode(block).length,
      jsonHtml,
      plainText: jsonHtml ? null : dataStr,
      meta,
    })
  }
  return events
}

function parseNDJSON(body: string): StreamEvent[] {
  const lines = body.split(/\r?\n/).filter(l => l.trim())
  return lines.map((line, index) => {
    let jsonHtml: string | null = null
    let label = ''
    try {
      const parsed = JSON.parse(line)
      jsonHtml = highlightJson(JSON.stringify(parsed, null, 2))
      const firstStringKey = Object.keys(parsed).find(k => typeof parsed[k] === 'string')
      if (firstStringKey) label = String(parsed[firstStringKey]).slice(0, 40)
    } catch {
      // not JSON
    }
    return {
      index,
      label: label || `line ${index + 1}`,
      rawBytes: new TextEncoder().encode(line).length,
      jsonHtml,
      plainText: jsonHtml ? null : line,
      meta: {},
    }
  })
}

type StreamResult =
  | { kind: 'sse';   events: StreamEvent[] }
  | { kind: 'ndjson'; events: StreamEvent[] }
  | null

function detectStream(body: string, contentType: string): StreamResult {
  if (contentType === 'text/event-stream') {
    const events = parseSSE(body)
    return events.length > 0 ? { kind: 'sse', events } : null
  }
  if (contentType === 'application/x-ndjson' || contentType === 'application/jsonl') {
    const events = parseNDJSON(body)
    return events.length > 1 ? { kind: 'ndjson', events } : null
  }
  // Sniff: multiple non-empty lines all parsing as JSON
  if (body.includes('\n')) {
    const lines = body.split(/\r?\n/).filter(l => l.trim())
    if (lines.length > 1 && lines.every(l => { try { JSON.parse(l); return true } catch { return false } })) {
      return { kind: 'ndjson', events: parseNDJSON(body) }
    }
  }
  return null
}
```

- [ ] **Step 3: Add StreamEventRow component inside BodyViewer.tsx**

Add this component function **after** `detectStream` and **before** the main `BodyViewer` export:

```typescript
function StreamEventRow({ event, defaultOpen }: { event: StreamEvent; defaultOpen: boolean }) {
  const [open, setOpen] = useState(defaultOpen)
  const hasMeta = Object.keys(event.meta).length > 0

  return (
    <div className="bv-event">
      <div className="bv-event__header" onClick={() => setOpen(o => !o)}>
        <span className={`bv-event__chevron${open ? ' bv-event__chevron--open' : ''}`}>▶</span>
        <span className="bv-event__index">#{event.index + 1}</span>
        <span className="bv-event__label">{event.label}</span>
        <span className="bv-event__size">{event.rawBytes} B</span>
      </div>
      {open && (
        <>
          {hasMeta && (
            <div className="bv-event__meta">
              {Object.entries(event.meta).map(([k, v]) => (
                <span key={k} style={{ marginRight: 12 }}>{k}: {v}</span>
              ))}
            </div>
          )}
          <div className="bv-event__body">
            {event.jsonHtml
              ? <span dangerouslySetInnerHTML={{ __html: event.jsonHtml }} />
              : event.plainText}
          </div>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 4: Wire stream detection into the BodyViewer component**

In the `BodyViewer` component function, add the stream detection memo after the existing `pretty` memo (around line 194):

```typescript
  const stream = useMemo(
    () => (mode === 'pretty' && !isBase64 ? detectStream(body, contentType) : null),
    [mode, body, contentType, isBase64]
  )
  const [allExpanded, setAllExpanded] = useState(false)
```

In the toolbar badges section, add an events badge when a stream is detected. Find the `{contentType && <span className="bv-badge">{contentType}</span>}` line and add after it:

```tsx
{stream && (
  <span className="bv-badge bv-badge--events">{stream.events.length} events</span>
)}
```

Add the expand/collapse toggle button after the existing `bv-copy-btn`:

```tsx
{stream && mode === 'pretty' && (
  <button className="bv-stream-toggle" onClick={() => setAllExpanded(e => !e)}>
    {allExpanded ? 'Collapse all' : 'Expand all'}
  </button>
)}
```

In the `{/* Pretty view */}` section, add stream rendering as the first branch (before the image check):

```tsx
{/* Stream view — SSE / NDJSON */}
{mode === 'pretty' && stream && (
  <div className="bv-stream-events">
    {stream.events.map(event => (
      <StreamEventRow key={event.index} event={event} defaultOpen={allExpanded} />
    ))}
  </div>
)}

{/* Non-stream pretty view */}
{mode === 'pretty' && !stream && (
  <>
    {/* ... existing image / json / xml / text rendering ... */}
  </>
)}
```

> Wrap the existing `{mode === 'pretty' && (...)}` block content inside the `{mode === 'pretty' && !stream && (...)}` wrapper. Keep the existing image, JSON, XML, and text rendering untouched inside it.

- [ ] **Step 5: Build**

```bash
cd /home/anna/git/codify/IoTSpy/frontend && npm run build 2>&1 | tail -10
```

Expected: no TypeScript errors.

- [ ] **Step 6: Run full backend test suite**

```bash
cd /home/anna/git/codify/IoTSpy && dotnet test src/ 2>&1 | tail -5
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/components/common/BodyViewer.tsx \
        frontend/src/styles/body-viewer.css
git commit -m "feat: add SSE/NDJSON stream rendering with collapsible event rows"
```

---

## Final verification

- [ ] **Run full test suite one last time**

```bash
cd /home/anna/git/codify/IoTSpy && dotnet test src/ 2>&1 | tail -10
cd frontend && npm run build 2>&1 | tail -5
```

Expected: all backend tests pass, frontend build succeeds.

- [ ] **Create feature branch PR**

```bash
git push origin HEAD
```

Then open a PR targeting `main`.

---

## Self-review notes

- **Spec coverage confirmed:** AdminController stats/purge/export ✓, CertificatesController regenerate ✓, AuthController safety guards ✓, AdminPage with four tabs ✓, Header admin link ✓, /admin route with role guard ✓, BodyViewer SSE/NDJSON ✓, collapsible events ✓, event count badge ✓, expand-all toggle ✓.
- **UsersController discrepancy:** The spec called for a new `UsersController` at `/api/users`, but the existing `AuthController` already implements all user CRUD at `/api/auth/users`. Tasks 4 and 10 use the existing routes; no duplicate controller is created.
- **API client pattern:** Task 7 uses `apiGet`/`apiDelete` helper references. Tasks 8–10 use raw `fetch` with the stored token directly (consistent with how `CertificatesController` endpoints are called via the download `<a>` links). Before implementing, verify which pattern other hooks/components use and follow that convention.
- **`AuditEntry` type:** If `details` and `ipAddress` fields are missing from the frontend `AuditEntry` type in `api.ts`, add them in Task 9.
