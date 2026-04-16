# IoTSpy — Key Design Decisions & Architecture Notes

This document captures architectural decisions, naming conventions, and design rationale to guide future development.

---

## Core Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| **Proxy modes** | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method; user choice based on network setup |
| **Storage** | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres for multi-user deployments |
| **Auth** | Multi-user JWT with RBAC (Admin/Operator/Viewer), PBKDF2 hash in DB | Team tool with role-based access; backward-compatible with legacy single-user mode |
| **TLS MITM** | BouncyCastle (pure .NET) | No native dependency; works cross-platform; certificate generation in memory or DB cache |
| **TLS passthrough** | Custom handshake parser + relay | Extract metadata (JA3/JA3S/SNI/cert) without breaking the connection; research-grade visibility |
| **SSL stripping** | HTTP-level intercept + upstream TLS fetch | Visibility into HTTPS traffic for devices that can't install CAs; explicit opt-in feature |
| **Resilience** | Per-host Polly circuit breaker | A dead IoT cloud endpoint must not stall the whole proxy; per-service configuration |
| **Real-time** | SignalR | Native .NET, easy JS client, supports group subscriptions per device |
| **Frontend** | Vite 6 + React 19 + TypeScript | Lightweight, no framework overhead; compatible with existing CORS config |
| **AI mock** | Pluggable (Claude / OpenAI / Ollama) | Avoid lock-in; local Ollama for offline use |
| **Rules engine** | Declarative match → action model | Non-technical users can craft rules via UI; simple, composable actions (modify, drop, delay) |
| **Scripted breakpoints** | Both C# and JavaScript | Operators pick language familiarity; Jint for JS avoids dependency on Node |
| **Packet capture** | Ring buffer (10k packets, LinkedList) | O(1) enqueue/dequeue; limit prevents unbounded memory growth; old packets dropped on buffer full |
| **Anomaly detection** | Per-host Welford online algorithm | Streaming baseline with no external service; configurable deviation threshold |
| **Audit log** | Immutable event log per action | Compliance-friendly; tracks auth, user CRUD, destructive operations |
| **EF Core** | DateTimeOffset as Unix milliseconds (`long`) | SQLite lacks native datetime; milliseconds preserve precision; `ValueConverter` handles serialization |

---

## Data Flow & Architecture

```
IoT Device → ExplicitProxy :8888 (explicit mode)
           → TransparentProxy :9999 (gateway/ARP mode)
           → MqttBrokerProxy :1883 (MQTT MITM)
           → CoapProxy :5683 (UDP) (CoAP forward proxy)
  └─ ExplicitProxyServer / TransparentProxyServer
       ├─ Plain HTTP → InterceptHttpStreamAsync
       ├─ WebSocket Upgrade (101) → RelayWebSocketFramesAsync (frame capture)
       ├─ gRPC (application/grpc) → capture with Protocol=Grpc
       ├─ Plain HTTP + SslStrip=true → SslStripService (intercept HTTPS redirects)
       └─ TLS (CONNECT or transparent)
            ├─ CaptureTls=false → HandleTlsPassthroughAsync (JA3/SNI/cert extraction)
            └─ CaptureTls=true → CertificateAuthority TLS MITM
                                  SslStream relay
                                  InterceptHttpStreamAsync
                                    ├─ IManipulationService (rules → scripts)
                                    ├─ IPacketCaptureAnalyzer
                                    ├─ CaptureRepository (write)
                                    └─ ICapturePublisher → SignalR (broadcast)
```

---

## Naming Conventions

- **Namespace prefix:** `IoTSpy` (capital I, o, T, S — not `Iotspy` or `IOTSPY`)
- **Solution file:** `IoTSpy.sln`
- **Project layout:** 
  - Core domains: `IoTSpy.Core` (no infrastructure deps)
  - Infrastructure: `IoTSpy.Storage`, `IoTSpy.Proxy`, `IoTSpy.Protocols`, etc.
  - Host: `IoTSpy.Api`
  - Tests: `IoTSpy.*.Tests` (parallel to library under test)
- **Docker image/container:** `iotspy` (lowercase)
- **Git repo directory:** `IoTSpy/` (with capital I)
- **Frontend:** `frontend/` (React app, separate from backend)
- **Documentation:** `docs/` (architecture, planning, gaps)

---

## Layering & Dependencies

**Dependency rule:** Outer layers depend on inner layers only; no back-references.

```
IoTSpy.Api (controllers, hubs, DI setup)
  ├── IoTSpy.Proxy (proxy servers, resilience)
  │    ├── IoTSpy.Protocols (decoders)
  │    ├── IoTSpy.Manipulation (rules, fuzzer, AI mock)
  │    │    ├── IoTSpy.Core (models, interfaces)
  │    │    └── IoTSpy.Protocols
  │    └── IoTSpy.Core
  ├── IoTSpy.Scanner (port scan, CVE lookup)
  │    ├── IoTSpy.Core
  │    └── IoTSpy.Protocols
  ├── IoTSpy.Storage (EF Core, repositories)
  │    └── IoTSpy.Core
  ├── IoTSpy.Protocols (decoders)
  │    └── IoTSpy.Core
  ├── IoTSpy.Manipulation
  └── IoTSpy.Core (domain models, interfaces only)
```

**Key constraint:** `IoTSpy.Core` contains ZERO infrastructure dependencies (no EF, no DI containers, no logging frameworks). This ensures the domain layer remains portable and testable.

---

## Critical Implementation Notes

### EF Core & DateTimeOffset

All `DateTimeOffset` columns are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext.ConfigureConventions()`. This is required for SQLite `ORDER BY` compatibility.

```csharp
property.HasConversion(
  x => x.ToUnixTimeMilliseconds(),
  x => DateTimeOffset.FromUnixTimeMilliseconds(x)
);
```

### JSON Enum Serialization

Both ASP.NET Core and SignalR must configure `JsonStringEnumConverter` to ensure enums serialize as strings:

```csharp
services
  .AddControllers()
  .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

services
  .AddSignalR()
  .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

**Critical:** Missing the SignalR call causes numeric enum values in live-streamed captures, which crashes the frontend timeline.

### Linux Packet Capture (SharpPcap)

SharpPcap requires `CAP_NET_RAW` and `CAP_NET_ADMIN`. Grant them to the *real* dotnet binary (not the symlink):

```bash
sudo setcap cap_net_raw,cap_net_admin+eip "$(readlink -f $(which dotnet))"
```

`setcap` rejects symlinks; always use `readlink -f` to resolve to the actual binary path.

### iOS/macOS TLS Certificate Compatibility

Apple enforces strict certificate requirements:
- **Validity cap:** ≤ 397 days (iOS/macOS silently rejects certs exceeding the 398-day limit)
- **Authority Key Identifier:** Use keyid-only form (`CreateAuthorityKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(caKeyPair.Public))`)
  - iOS 16+ rejects the full form (keyId + DirName + serial)
- **IP SANs:** Use `GeneralName.IPAddress` with `DerOctetString(ip.GetAddressBytes())`
  - iOS rejects `DnsName`-as-IP

### Hosted Service Registration

Services that are both singletons and hosted (like `ProxyService`) use the pattern:

```csharp
services.AddSingleton<ProxyService>(/* ... */);
services.AddHostedService(sp => sp.GetRequiredService<ProxyService>());
```

This prevents double-registration while ensuring the singleton starts on app launch.

### EF Core SQLite Migration Quirks

SQLite doesn't support `AlterColumn` to add NOT NULL defaults. Use `.Sql()` instead:

```csharp
migrationBuilder.Sql(@"
  UPDATE Captures SET RequestBodySize = 0 WHERE RequestBodySize IS NULL;
");
migrationBuilder.AlterColumn<long>(
  "RequestBodySize",
  "Captures",
  nullable: false,
  oldClrType: typeof(long),
  oldNullable: true
);
```

Also add a stub `.Designer.cs` for EF Core migration discovery.

---

## Error Handling & Resilience Strategy

1. **Proxy errors (per-host):** Circuit breaker stops requests for 60s after 50% failure rate over 30s window
2. **Timeouts:** 15s connect, 10s TLS handshake (configurable via `Resilience` in `appsettings.json`)
3. **Parse failures:** Decode errors logged but do not crash the proxy; captures recorded as "Unknown" protocol
4. **SignalR broadcast failures:** Fire-and-forget via `PublishSafeAsync` (catches + logs exceptions)
5. **DB unavailability:** Health checks at `/health` (with DB connectivity), `/ready` (readiness probe)

---

## Testing Strategy

### Backend
- **Unit tests** in `*.Tests` projects (NSubstitute mocks for dependencies)
- **Integration tests** in `Api.IntegrationTests` (WebApplicationFactory, in-memory SQLite, no real network)
- **Controller tests** verify HTTP contracts; service/repository tests verify business logic
- **Decoder tests** use real packet fixtures (MQTT, DNS, CoAP, etc.)

### Frontend
- **Component tests** via Vitest + React Testing Library
- **No E2E tests** currently (would require Docker orchestration)
- **Manual testing** via dev server (`npm run dev`)

### Coverage targets
- Backend: 80%+ statement coverage via Coverlet
- Frontend: Component tests for critical paths (auth, capture list, rules editor)

---

## Configuration Strategy

All sensitive/deployment-specific values via `appsettings.json` or environment variables (double-underscore notation):

- `Auth__JwtSecret` (required, ≥32 chars)
- `Database__Provider` (sqlite|postgres)
- `Database__ConnectionString`
- `AiMock__Provider` (claude|openai|ollama)
- `AiMock__ApiKey`
- `Frontend__Origin` (CORS origin for dev)

Development overrides via `appsettings.Development.json`; production config injected at container startup.

---

## Open Questions & Areas for Refinement

1. **Passive mode design** — Phase 21 proposes in-memory buffering; need to finalize session save/load semantics
2. **Audit log retention** — Currently no auto-purge; consider TTL policies for compliance
3. **API spec schema inference** — Current algorithm naive; could use ML for better format detection
4. **Anomaly alert tuning** — Deviation threshold (default 3.0) may be too strict for some environments
5. **Horizontal scaling** — Redis backplane (Phase 16.8) needed; current session state (anomaly baseline, rule cache) not distributed

See [GAPS.md](GAPS.md) for known issues and [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for future work.
