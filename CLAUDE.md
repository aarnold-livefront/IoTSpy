# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the API (Auth__JwtSecret is required)
Auth__JwtSecret="replace-with-32-char-minimum-secret" dotnet run --project src/IoTSpy.Api

# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test src/IoTSpy.SomeTests/IoTSpy.SomeTests.csproj

# Restore dependencies
dotnet restore

# Add EF Core migration (run from repo root; DesignTimeDbContextFactory handles SQLite)
dotnet ef migrations add <MigrationName> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api

# Apply migrations manually
dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api
```

Scalar API docs are served at `http://localhost:5000/scalar` in Development mode only.

## Architecture

### Project dependency graph

```
IoTSpy.Api (ASP.NET Core host)
  ├── IoTSpy.Core          (domain models, interfaces, enums — no infrastructure deps)
  ├── IoTSpy.Proxy         (TCP listener, TLS MITM, Polly resilience)
  ├── IoTSpy.Storage       (EF Core DbContext + repositories; SQLite/Postgres)
  ├── IoTSpy.Protocols     (MQTT 3.1.1/5.0 + DNS/mDNS decoders)
  ├── IoTSpy.Scanner       (TCP port scan, service fingerprinting, credential testing, CVE lookup, config audit)
  └── IoTSpy.Manipulation  (Phase 4+ — empty stub)
```

`Manipulation` is an empty stub (`.csproj` only). `Protocols` has MQTT and DNS decoders. `Scanner` has the full pen-test suite.

### Data flow

```
IoT Device → ExplicitProxy :8888      (explicit mode — device configured to use proxy)
           → TransparentProxy :9999   (gateway/ARP mode — iptables REDIRECT)
  └─ ExplicitProxyServer / TransparentProxyServer
       ├─ Plain HTTP → InterceptHttpStreamAsync
       └─ TLS (CONNECT or transparent)
            ├─ CaptureTls=false → passthrough
            └─ CaptureTls=true  → CertificateAuthority.GetOrCreateHostCertAsync()
                                   SslStream MITM → InterceptHttpStreamAsync
                                        ├─ CaptureRepository (SQLite/PG)
                                        └─ ICapturePublisher → SignalR (group-routed) → dashboard
```

### Key service lifetimes (Program.cs)

- `ExplicitProxyServer`, `TransparentProxyServer`, `CertificateAuthority`, `ProxyService`, `ArpSpoofEngine`, `IptablesHelper`, `SignalRCapturePublisher`, `PortScanner`, `ServiceFingerprinter`, `CredentialTester`, `ConfigAuditor`, `ScannerService` — **Singleton** (TCP listeners / long-lived services).
- `ProxyService` is registered both as `IProxyService` (singleton) and as `IHostedService` via `AddHostedService(sp => ...)` to avoid double instantiation.
- Repositories are **Scoped** (EF Core DbContext).

### TLS MITM

Root CA is generated lazily on first use, stored as PEM in `CertificateEntries` (SQLite), and cached in memory. Per-host leaf certs are cached in the DB and reused until one day before expiry. Upstream TLS is validated with certificate validation disabled (research tool).

### Resilience (Polly 8)

Two named pipelines in `ProxyResiliencePipelines`:
- `iotspy-connect` — keyed by upstream hostname: `Timeout → Retry (exponential) → CircuitBreaker`
- `iotspy-tls` — `Timeout` only (TLS handshake is not safely retryable)

All resilience defaults are configurable under the `Resilience` section in `appsettings.json`.

### Authentication

Single-user model. BCrypt password hash stored in the single `ProxySettings` row (Id=1). JWT issuer and audience are both `"iotspy"`. SignalR accepts the token via `?access_token=` query param. `Auth:JwtSecret` must be ≥ 32 characters; the app throws on startup if absent.

### Storage

`StorageExtensions.AddIoTSpyStorage()` wires EF Core for SQLite (default) or Postgres based on `Database:Provider`. `MigrateAsync()` is called at startup and auto-applies pending migrations. Use `DesignTimeDbContextFactory` when running `dotnet ef` CLI commands.

## Naming conventions

- Namespace prefix: `IoTSpy` (capital I, o, T, S)
- Docker image/container: `iotspy` (lowercase)

## Current status

Phases 1, 2, and 3 (backend) are complete. `IoTSpy.Manipulation` is an empty stub. No tests exist yet. EF Core migrations: `InitialCreate` + `AddPhase2ProxySettings` + `AddPhase3Scanner`.

**Phase 3 additions:**
- `IoTSpy.Scanner` — `PortScanner` (TCP connect scan, configurable concurrency/port ranges), `ServiceFingerprinter` (banner grab, CPE extraction via regex), `CredentialTester` (FTP/Telnet/MQTT default credential checks), `CveLookupService` (OSV.dev API), `ConfigAuditor` (Telnet, UPnP, anon MQTT, exposed DB, HTTP admin detection)
- `IoTSpy.Scanner` — `ScannerService` orchestrator implementing `IScannerService` — runs scan pipeline in background, updates device `SecurityScore`
- `IoTSpy.Core` — `ScanJob`, `ScanFinding` models; `ScanStatus`, `ScanFindingSeverity`, `ScanFindingType` enums; `IScanJobRepository`, `IScannerService` interfaces
- `IoTSpy.Storage` — `ScanJobs` + `ScanFindings` DbSets, `ScanJobRepository`, `AddPhase3Scanner` migration
- `IoTSpy.Api` — `ScannerController` (POST scan, GET jobs/findings/status, cancel, delete)

Next priorities per `docs/PLAN.md`:
1. Phase 3.7: Frontend scan results panel
2. Phase 4: Active manipulation (rules engine, scripted breakpoints, replay, fuzzer)

See `docs/architecture.md` for full architecture spec and `docs/PLAN.md` for the phased task list.
