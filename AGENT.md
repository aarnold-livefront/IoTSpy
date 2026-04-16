# AGENT.md

Guidance for AI agents working with the IoTSpy codebase.

## What this project is

IoTSpy is an IoT network security research platform: transparent MITM proxy, multi-protocol analyzer, pen-test suite, and traffic manipulation engine. Backend is .NET 10 / ASP.NET Core. Frontend is Vite 6 + React 19 + TypeScript. Storage is SQLite (default) or PostgreSQL via EF Core.

## Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test src/IoTSpy.SomeTests/IoTSpy.SomeTests.csproj

# Restore dependencies
dotnet restore

# Run the API (Auth__JwtSecret must be ≥ 32 chars)
Auth__JwtSecret="replace-with-32-char-minimum-secret" dotnet run --project src/IoTSpy.Api

# Add EF Core migration (run from repo root)
dotnet ef migrations add <MigrationName> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api

# Apply migrations manually
dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api

# Frontend
cd frontend && npm install && npm run dev
cd frontend && npm test        # Vitest
cd frontend && npm run build
```

Scalar API docs: `http://localhost:5000/scalar` (Development mode only).

## Project layout

```
IoTSpy.sln
src/
  IoTSpy.Api/                  ASP.NET Core host — controllers, SignalR hubs, middleware
  IoTSpy.Core/                 Domain models, interfaces, enums — no infrastructure deps
  IoTSpy.Proxy/                TCP listener, TLS MITM/passthrough, SSL stripping, Polly resilience
  IoTSpy.Storage/              EF Core DbContext + repositories (SQLite/Postgres)
  IoTSpy.Protocols/            MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB, telemetry decoders
  IoTSpy.Scanner/              Port scan, service fingerprinting, CVE lookup, packet capture
  IoTSpy.Manipulation/         Rules engine, scripted breakpoints, replay, fuzzer, AI mock, OpenRTB PII, API spec generation, content replacement
  IoTSpy.*.Tests/              Unit + integration tests (NSubstitute mocks, EF Core in-memory)
  IoTSpy.Api.IntegrationTests/ WebApplicationFactory integration tests
frontend/                      Vite + React + TypeScript dashboard
docs/
  ARCHITECTURE.md              Full architecture spec
  PLAN.md                      Phased task list and roadmap
```

### Dependency graph

```
IoTSpy.Api
  ├── IoTSpy.Core
  ├── IoTSpy.Proxy         → IoTSpy.Core, IoTSpy.Protocols
  ├── IoTSpy.Storage       → IoTSpy.Core
  ├── IoTSpy.Protocols     → IoTSpy.Core
  ├── IoTSpy.Scanner       → IoTSpy.Core
  └── IoTSpy.Manipulation  → IoTSpy.Core, IoTSpy.Protocols
```

`IoTSpy.Core` has zero infrastructure dependencies — keep it that way.

## Architecture essentials

### Service lifetimes

- **Singleton**: `ExplicitProxyServer`, `TransparentProxyServer`, `CertificateAuthority`, `ProxyService`, `SslStripService`, `MqttBrokerProxy`, `CoapProxy`, `PortScanner`, `ScannerService`, all SignalR publishers.
- **Scoped**: all EF Core repositories.
- `ProxyService` is registered as both `IProxyService` (singleton) and `IHostedService` via `AddHostedService(sp => ...)` — do not register it twice.

### Adding a new feature

1. **Models/interfaces** go in `IoTSpy.Core`.
2. **EF entities + repositories** go in `IoTSpy.Storage`; add a migration.
3. **Protocol decoding** goes in `IoTSpy.Protocols`.
4. **HTTP/proxy-layer logic** goes in `IoTSpy.Proxy`.
5. **REST endpoints** go in a controller in `IoTSpy.Api`.
6. **Tests** mirror the project being tested (e.g. `IoTSpy.Protocols.Tests`).

### Storage

Switch between SQLite and Postgres via `appsettings.json`:

```json
"Database": { "Provider": "Sqlite" }   // or "Postgres"
"ConnectionStrings": { "DefaultConnection": "..." }
```

`MigrateAsync()` runs at startup automatically. Use `DesignTimeDbContextFactory` when running `dotnet ef` CLI commands.

### Authentication

JWT bearer auth. `Auth:JwtSecret` must be ≥ 32 characters (throws on startup if absent). Multi-user RBAC: `UserRole` enum (Admin / Operator / Viewer). SignalR accepts the token via `?access_token=` query param.

### Real-time streaming

SignalR hubs:
- `TrafficHub` (`/hubs/traffic`) — captured HTTP/HTTPS, WebSocket frames, MQTT messages, anomaly alerts
- `PacketCaptureHub` (`/hubs/packets`) — raw packet stream

## Naming conventions

- Namespace prefix: `IoTSpy` (capital **I**, lowercase **o**, capital **T**, capital **S**)
- Docker image / container: `iotspy` (all lowercase)
- Test class naming: `<ClassName>Tests` in the matching `IoTSpy.*.Tests` project
- C# style: standard .NET conventions; async methods suffixed with `Async`

## Testing guidance

- Run `dotnet test` before committing; all 350+ backend tests must pass.
- New backend code needs corresponding tests. Prefer unit tests with NSubstitute mocks; use EF Core SQLite in-memory for repository tests.
- Frontend tests use Vitest + React Testing Library (`npm test` inside `frontend/`).
- CI runs on every push/PR via `.github/workflows/ci.yml`.

## Key configuration sections (`appsettings.json`)

| Section | Purpose |
|---|---|
| `Auth:JwtSecret` | Required; ≥ 32 chars |
| `Database:Provider` | `Sqlite` (default) or `Postgres` |
| `Resilience` | Polly pipeline defaults (timeout, retry, circuit-breaker) |
| `RateLimit:Enabled` | Sliding-window rate limiter toggle |
| `DataRetention:Enabled` | Background TTL cleanup (default: false) |
| `Serilog` | Log sinks and minimum level |

## Available Claude Code skills

Project-specific skills live in `.dev/claude-skills/`. If you are running as Claude Code, install them once from the repo root:

```bash
# 1. Register the local marketplace (absolute path required)
claude plugin marketplace add "$(pwd)/.dev/claude-skills" --scope project

# 2. Install each skill
claude plugin install dotnet-engineer@iotspy-skills --scope project
claude plugin install security-code-review@iotspy-skills --scope project
claude plugin install threat-modeling@iotspy-skills --scope project
```

| Skill | When to use |
|---|---|
| `/dotnet-engineer` | ASP.NET Core, EF Core, SignalR, Polly, xUnit/NSubstitute architecture guidance |
| `/security-code-review` | OWASP Top 10 + auth/injection vulnerability review before merging |
| `/threat-modeling` | Structured threat analysis for new features or design changes |

See `.dev/claude-skills/README.md` for full details.

## Known operational requirements

### Packet capture on Linux — setcap

SharpPcap requires `CAP_NET_RAW` and `CAP_NET_ADMIN` to open raw sockets. On Linux, grant them to the **real** dotnet binary (not the symlink — `setcap` refuses symlinks):

```bash
sudo setcap cap_net_raw,cap_net_admin+eip "$(readlink -f $(which dotnet))"
# typically resolves to: /usr/share/dotnet/dotnet
```

If you see "Could not find any devices" in the Packet Capture tab, this is the most likely cause. Restart the API after running `setcap`.

### JSON enum serialization

`Program.cs` configures **both** MVC controllers and the SignalR JSON hub protocol with `JsonStringEnumConverter`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(opts => opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

This ensures `InterceptionProtocol` serializes as `"Http"`, `"Mqtt"`, etc. (not `0`, `1`, …) across both REST API responses and SignalR events. If you remove or forget the `AddJsonProtocol` call, live-streamed captures will have numeric protocols and the frontend timeline will crash.

### TLS certificate requirements for iOS/macOS

The `CertificateAuthority` class generates leaf certs meeting Apple's requirements:

- **Validity ≤ 397 days** — Apple enforces a 398-day cap on TLS leaf certificates (policy effective Sep 2020). Certs exceeding this are silently rejected by iOS/macOS even if the root CA is trusted.
- **AKI keyid-only form** — iOS 16+ (and iOS 26) rejects the full Authority Key Identifier form (`keyId + DirName + serial`) emitted by `CreateAuthorityKeyIdentifier(cert)`. Use `CreateAuthorityKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(caKeyPair.Public))` (keyid-only, matching mitmproxy/Charles/Proxyman behaviour).
- **SAN with IP literal** — For IP address hostnames, the SAN must use `GeneralName.IPAddress` with `DerOctetString(ip.GetAddressBytes())`, not `DnsName`. iOS rejects DnsName-as-IP.

If you regenerate the root CA or change cert generation logic, **delete all leaf certs** from the database so they are re-generated with the corrected extensions:
```bash
sqlite3 src/IoTSpy.Api/iotspy.db "DELETE FROM Certificates WHERE IsRootCa = 0;"
```

### EF Core migrations on SQLite

SQLite migrations that call `AlterColumn` generate `PRAGMA foreign_keys = 0` statements, which **cannot execute inside a transaction**. EF Core wraps migrations in transactions, so the migration will fail with:

> `The migration operation 'PRAGMA foreign_keys = 0;' from migration 'X' cannot be executed in a transaction`

**Workaround:** Replace `AlterColumn` with direct `migrationBuilder.Sql(...)` calls (e.g. `UPDATE` statements to backfill defaults). See `20260322032005_AddBodyCaptureDefaults.cs` for an example.

Every migration must have a matching `.Designer.cs` file with the `[Migration("...")]` attribute. Without it EF Core never discovers the migration. The Designer file can have a stub `BuildTargetModel` body — the full model is in `IoTSpyDbContextModelSnapshot.cs`.

## What to avoid

- Do not add infrastructure dependencies to `IoTSpy.Core`.
- Do not register a singleton service as both a singleton and a hosted service separately — use the `AddHostedService(sp => sp.GetRequiredService<T>())` pattern.
- Do not disable upstream TLS validation outside of test/development code (it is intentionally disabled in the proxy for research purposes — do not spread this pattern).
- Do not store secrets in source code; use environment variables or user-secrets.
