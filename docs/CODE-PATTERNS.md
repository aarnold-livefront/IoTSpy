# IoTSpy — Code Patterns & Examples

Reference guide for implementing common patterns in IoTSpy. Shows where to put code and how to structure it.

---

## Table of Contents
1. [Where Does My Code Go?](#where-does-my-code-go)
2. [Controller Pattern](#controller-pattern)
3. [Repository Pattern](#repository-pattern)
4. [Protocol Decoder Pattern](#protocol-decoder-pattern)
5. [Service/Business Logic Pattern](#servicebusiness-logic-pattern)
6. [SignalR Hub Pattern](#signalr-hub-pattern)
7. [Model/Entity Pattern](#modelentity-pattern)
8. [Frontend Hook Pattern](#frontend-hook-pattern)
9. [Frontend Component Pattern](#frontend-component-pattern)
10. [Test Pattern](#test-pattern)

---

## Where Does My Code Go?

| What I'm adding | Project | Subdirectory | Example |
|---|---|---|---|
| Domain model | `IoTSpy.Core` | `Models/` | `Device.cs`, `CapturedRequest.cs` |
| Interface/contract | `IoTSpy.Core` | `Interfaces/` | `IDeviceRepository.cs` |
| Enum | `IoTSpy.Core` | `Enums/` | `UserRole.cs`, `ScanFindingSeverity.cs` |
| EF Entity mapping | `IoTSpy.Storage` | (auto in DbContext) | `builder.Entity<Device>()` |
| Repository impl | `IoTSpy.Storage` | `Repositories/` | `DeviceRepository.cs` |
| REST endpoint | `IoTSpy.Api` | `Controllers/` | `DevicesController.cs` |
| SignalR hub | `IoTSpy.Api` | `Hubs/` | `TrafficHub.cs` |
| Business logic | `IoTSpy.Proxy`, `Manipulation`, `Scanner`, `Protocols` | Project-specific | `ProxyService.cs`, `RulesEngine.cs` |
| Protocol decoder | `IoTSpy.Protocols` | `<Protocol>/` | `MqttDecoder.cs`, `DnsDecoder.cs` |
| Unit tests | `IoTSpy.*.Tests` | (parallel to code) | `DeviceRepositoryTests.cs` |
| Integration tests | `IoTSpy.Api.IntegrationTests` | (by feature) | `DevicesIntegrationTests.cs` |
| Frontend hook | `frontend/src` | `hooks/` | `useCaptures.ts`, `useDevices.ts` |
| Frontend component | `frontend/src` | `components/<feature>/` | `CaptureList.tsx`, `DeviceDetail.tsx` |
| Frontend API client | `frontend/src` | `api/` | `captures.ts`, `devices.ts` |
| Frontend types | `frontend/src` | `types/` | `api.ts`, `captures.ts` |

---

## Controller Pattern

### 1. Define the interface in Core
```csharp
// IoTSpy.Core/Interfaces/IDeviceRepository.cs
public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken ct = default);
    Task<Device> AddAsync(Device device, CancellationToken ct = default);
    Task UpdateAsync(Device device, CancellationToken ct = default);
}
```

### 2. Implement in Storage
```csharp
// IoTSpy.Storage/Repositories/DeviceRepository.cs
public class DeviceRepository(IoTSpyDbContext db) : IDeviceRepository
{
    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
    
    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken ct = default)
        => await db.Devices.AsNoTracking().ToListAsync(ct);
    
    public async Task<Device> AddAsync(Device device, CancellationToken ct = default)
    {
        db.Devices.Add(device);
        await db.SaveChangesAsync(ct);
        return device;
    }
    
    public async Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        db.Devices.Update(device);
        await db.SaveChangesAsync(ct);
    }
}
```

### 3. Register in DI (Program.cs)
```csharp
services.AddScoped<IDeviceRepository, DeviceRepository>();
```

### 4. Create the controller
```csharp
// IoTSpy.Api/Controllers/DevicesController.cs
[ApiController]
[Route("api/devices")]
[Authorize]  // Require JWT or API key
public class DevicesController(IDeviceRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetAll(CancellationToken ct)
    {
        var devices = await repo.GetAllAsync(ct);
        return Ok(devices.Select(d => new DeviceDto { Id = d.Id, ... }));
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetById(Guid id, CancellationToken ct)
    {
        var device = await repo.GetByIdAsync(id, ct);
        if (device == null) return NotFound();
        return Ok(new DeviceDto { ... });
    }
}
```

### Key points:
- **Always use `async/await`** with `CancellationToken`
- **Use `[Authorize]`** unless endpoint is explicitly public (`/api/auth/setup`, `/api/certificates/root-ca/download`)
- **Return DTOs**, not domain models (map in controller)
- **Use `AsNoTracking()`** for read-only queries
- **Inject dependencies** via constructor
- **Don't create new DbContext** — use scoped repo injected via DI

---

## Repository Pattern

### Structure
```csharp
public class DeviceRepository(IoTSpyDbContext db) : IDeviceRepository
{
    // Single responsibility: data access for Device entity
    // All methods async with CancellationToken
    // Use IQueryable for complex filters (let caller do paging)
    // Always SaveChangesAsync after mutations
}
```

### Read methods
```csharp
// Single entity by ID
public async Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

// With filtering
public async Task<IReadOnlyList<Device>> GetByVendorAsync(string vendor, CancellationToken ct = default)
    => await db.Devices.AsNoTracking()
        .Where(d => d.Vendor == vendor)
        .ToListAsync(ct);

// Return IQueryable for advanced filtering (controller handles paging)
public IQueryable<Device> QueryAll() => db.Devices.AsNoTracking();
```

### Write methods
```csharp
// Create
public async Task<Device> AddAsync(Device device, CancellationToken ct = default)
{
    db.Devices.Add(device);
    await db.SaveChangesAsync(ct);
    return device;
}

// Update
public async Task UpdateAsync(Device device, CancellationToken ct = default)
{
    db.Devices.Update(device);
    await db.SaveChangesAsync(ct);
}

// Delete
public async Task DeleteAsync(Guid id, CancellationToken ct = default)
{
    var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);
    if (device != null)
    {
        db.Devices.Remove(device);
        await db.SaveChangesAsync(ct);
    }
}
```

---

## Protocol Decoder Pattern

### 1. Define the DTO in Core
```csharp
// IoTSpy.Core/Models/MqttMessage.cs
public class MqttMessage
{
    public string Topic { get; set; } = "";
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public int Qos { get; set; }
    public bool IsRetain { get; set; }
}
```

### 2. Implement the decoder
```csharp
// IoTSpy.Protocols/Mqtt/MqttDecoder.cs
public class MqttDecoder : IProtocolDecoder<MqttMessage>
{
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        // Fast check: MQTT control packet type in first 4 bits
        if (header.IsEmpty) return false;
        var type = header[0] >> 4;
        return type >= 1 && type <= 15;
    }
    
    public async Task<IReadOnlyList<MqttMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data, 
        CancellationToken ct = default)
    {
        var messages = new List<MqttMessage>();
        var offset = 0;
        
        while (offset < data.Length)
        {
            var (msg, bytesRead) = ParseOneMessage(data.Span[offset..]);
            if (msg != null)
            {
                messages.Add(msg);
                offset += bytesRead;
            }
            else break;
        }
        
        return messages;
    }
    
    private (MqttMessage? msg, int bytesRead) ParseOneMessage(ReadOnlySpan<byte> span)
    {
        // Implementation...
        return (new MqttMessage { ... }, bytesRead);
    }
}
```

### 3. Register in DI
```csharp
services.AddSingleton<IProtocolDecoder<MqttMessage>, MqttDecoder>();
```

### Key points:
- Implement `IProtocolDecoder<T>` with `CanDecode()` and `DecodeAsync()`
- `CanDecode()` must be fast (header sniff only, no full parsing)
- Return empty list if unable to fully decode (not an error)
- Use `ReadOnlySpan<byte>` and `ReadOnlyMemory<byte>` (zero-copy)

---

## Service/Business Logic Pattern

### Example: Manipulation service
```csharp
// IoTSpy.Manipulation/ManipulationService.cs
public class ManipulationService(
    IManipulationRuleRepository ruleRepo,
    IBreakpointRepository bpRepo) : IManipulationService
{
    public async Task<HttpMessage> ApplyAsync(
        HttpMessage message,
        ManipulationPhase phase,
        CancellationToken ct = default)
    {
        // 1. Get applicable rules
        var rules = await ruleRepo.GetEnabledByPhaseAsync(phase, ct);
        
        // 2. Apply in priority order
        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            if (MatchesRule(message, rule))
            {
                await ApplyRuleAsync(message, rule, ct);
            }
        }
        
        // 3. Apply breakpoints (scripts)
        var breakpoints = await bpRepo.GetEnabledByPhaseAsync(phase, ct);
        foreach (var bp in breakpoints)
        {
            if (MatchesBreakpoint(message, bp))
            {
                await ExecuteBreakpointAsync(message, bp, ct);
            }
        }
        
        return message;
    }
    
    private bool MatchesRule(HttpMessage msg, ManipulationRule rule)
        => Regex.IsMatch(msg.Host, rule.HostPattern)
        && Regex.IsMatch(msg.Path, rule.PathPattern);
    
    private async Task ApplyRuleAsync(HttpMessage msg, ManipulationRule rule, CancellationToken ct)
    {
        // Implement action (modify, drop, delay)
    }
}
```

### Key points:
- Dependency inject all repos/services needed
- Use `CancellationToken` throughout
- Keep methods focused (single responsibility)
- Return modified state (don't mutate parameters unexpectedly)
- Register as singleton if stateless, scoped if needs DbContext

---

## SignalR Hub Pattern

```csharp
// IoTSpy.Api/Hubs/TrafficHub.cs
public class TrafficHub : Hub
{
    private readonly ILogger<TrafficHub> _logger;
    
    public TrafficHub(ILogger<TrafficHub> logger) => _logger = logger;
    
    public async Task JoinDeviceGroup(Guid deviceId)
    {
        await Groups.AddToGroupAsync(Connection.ConnectionId, $"device-{deviceId}");
        _logger.LogInformation("Client {ConnId} joined device {DeviceId}", 
            Context.ConnectionId, deviceId);
    }
    
    public async Task LeaveDeviceGroup(Guid deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device-{deviceId}");
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Key points:
- Inherit from `Hub`
- Methods callable from clients (public, async)
- Use groups for targeted broadcasts: `Groups.AddToGroupAsync`
- Log connections/disconnections
- Never throw unhandled exceptions (crashes client)

---

## Model/Entity Pattern

### Domain model (Core)
```csharp
// IoTSpy.Core/Models/Device.cs
public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpAddress { get; set; } = "";
    public string? MacAddress { get; set; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public int SecurityScore { get; set; } = 0;
    public DateTimeOffset FirstSeen { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
}
```

### EF configuration (Storage)
```csharp
// IoTSpy.Storage/IoTSpyDbContext.cs
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<Device>()
        .HasKey(d => d.Id);
    
    builder.Entity<Device>()
        .HasIndex(d => d.IpAddress)
        .IsUnique();
    
    // Default values
    builder.Entity<Device>()
        .Property(d => d.SecurityScore)
        .HasDefaultValue(0);
}
```

### DTO (Controller response)
```csharp
public class DeviceDto
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = "";
    public string? Hostname { get; set; }
    public int SecurityScore { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
```

### Key points:
- Domain model in Core (no [Column], [Table] attributes)
- EF mapping in DbContext.OnModelCreating
- DTOs for API responses (hide sensitive fields, optimize payload)
- Use DateTimeOffset (not DateTime) for timezone safety

---

## Frontend Hook Pattern

### API call hook
```typescript
// frontend/src/hooks/useCaptures.ts
export function useCaptures(deviceId?: string) {
    const [captures, setCaptures] = useState<CaptureDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    
    const fetch = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await apiFetch(
                `/api/captures${deviceId ? `?deviceId=${deviceId}` : ""}`,
                { headers: getAuthHeader() }
            );
            setCaptures(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unknown error");
        } finally {
            setLoading(false);
        }
    }, [deviceId]);
    
    useEffect(() => {
        fetch();
    }, [deviceId, fetch]);
    
    return { captures, loading, error, refetch: fetch };
}
```

### SignalR live stream hook
```typescript
export function useTrafficStream(deviceId: string) {
    const [captures, setCaptures] = useState<CaptureDto[]>([]);
    
    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl(`/hubs/traffic?access_token=${getToken()}`)
            .withAutomaticReconnect()
            .build();
        
        connection.on("TrafficCapture", (capture: CaptureDto) => {
            setCaptures(prev => [capture, ...prev]);
        });
        
        connection.start().catch(err => console.error(err));
        
        return () => {
            connection.stop();
        };
    }, [deviceId]);
    
    return captures;
}
```

### Key points:
- Custom hooks encapsulate logic (state, effects, callbacks)
- Use `useCallback` to memoize functions
- Clean up side effects (SignalR connections, timers)
- Return only what component needs

---

## Frontend Component Pattern

```typescript
// frontend/src/components/captures/CaptureList.tsx
interface CaptureListProps {
    captures: CaptureDto[];
    loading: boolean;
    onSelect: (capture: CaptureDto) => void;
}

export const CaptureList: React.FC<CaptureListProps> = React.memo(({
    captures,
    loading,
    onSelect,
}) => {
    if (loading) return <div>Loading...</div>;
    if (captures.length === 0) return <div>No captures</div>;
    
    return (
        <div className="capture-list">
            {captures.map(capture => (
                <div
                    key={capture.id}
                    className="capture-row"
                    onClick={() => onSelect(capture)}
                >
                    <span className="method">{capture.method}</span>
                    <span className="host">{capture.host}</span>
                    <span className="status">{capture.statusCode}</span>
                </div>
            ))}
        </div>
    );
});

CaptureList.displayName = "CaptureList";
```

### Key points:
- Props interface for clarity
- Use `React.memo` to prevent unnecessary re-renders
- Set `displayName` for debugging
- Keep components focused (one responsibility)
- No business logic (use hooks for that)

---

## Test Pattern

### Unit test (NSubstitute)
```csharp
// IoTSpy.Api.Tests/Controllers/DevicesControllerTests.cs
public class DevicesControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsOkWithDevices()
    {
        // Arrange
        var mockRepo = Substitute.For<IDeviceRepository>();
        var devices = new[] { new Device { Id = Guid.NewGuid(), IpAddress = "192.168.1.1" } };
        mockRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(devices);
        
        var controller = new DevicesController(mockRepo);
        
        // Act
        var result = await controller.GetAll(CancellationToken.None);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDevices = Assert.IsAssignableFrom<IEnumerable<DeviceDto>>(okResult.Value);
        Assert.Single(returnedDevices);
    }
}
```

### Integration test (WebApplicationFactory)
```csharp
// IoTSpy.Api.IntegrationTests/DevicesIntegrationTests.cs
public class DevicesIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    
    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.AddScoped<IDeviceRepository, TestDeviceRepository>();
            }));
        _client = _factory.CreateClient();
        await _client.AuthenticateAsync(); // Set up JWT token
    }
    
    [Fact]
    public async Task GetDevices_Returns200AndList()
    {
        var response = await _client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    public async Task DisposeAsync() => await _factory.DisposeAsync();
}
```

### Key points:
- Test one thing per test
- Use `Arrange/Act/Assert` structure
- Mock external dependencies
- Use integration tests for full HTTP flow
- Test both happy path and error cases

---

## Summary: Quick Decision Tree

```
What am I adding?
├─ Domain model → Core/Models/
├─ Interface → Core/Interfaces/
├─ Repository impl → Storage/Repositories/
├─ REST endpoint → Api/Controllers/
├─ Business logic → Proxy/Manipulation/Scanner/Protocols/
├─ SignalR hub → Api/Hubs/
├─ Protocol decoder → Protocols/<Protocol>/
├─ Test → *.Tests/ (parallel to code)
├─ Frontend hook → frontend/src/hooks/
├─ Frontend component → frontend/src/components/<feature>/
└─ Frontend API client → frontend/src/api/
```

## Recommended Workflow

1. **Design phase** → Use `/dotnet-engineer` for architecture guidance (see [SKILLS-PLUGINS.md](SKILLS-PLUGINS.md))
2. **Implementation** → Follow patterns in this document
3. **Testing** → Use patterns in Test Pattern section above
4. **Code review** → Use `/review` before PR
5. **Security review** → Use `/security-code-review` for auth/data code
6. **Cleanup** → Use `/simplify` if complex
7. **Commit** → Reference skill reviews in commit message

See [AGENT-NOTES.md](AGENT-NOTES.md#adding-features-checklist) for the full "Add a Feature" checklist.  
See [SKILLS-PLUGINS.md](SKILLS-PLUGINS.md) for when/how to use each skill.
