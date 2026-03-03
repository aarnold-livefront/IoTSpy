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
  ├── IoTSpy.Protocols     (Phase 2+ — empty stub)
  ├── IoTSpy.Scanner       (Phase 3+ — empty stub)
  └── IoTSpy.Manipulation  (Phase 4+ — empty stub)
```

All stubs (`Protocols`, `Scanner`, `Manipulation`) contain only a `.csproj`; no source files yet.

### Data flow (Phase 1)

```
IoT Device → HTTP/CONNECT :8888
  └─ ExplicitProxyServer (TcpListener)
       ├─ Plain HTTP → InterceptHttpStreamAsync
       └─ CONNECT tunnel
            ├─ CaptureTls=false → passthrough
            └─ CaptureTls=true  → CertificateAuthority.GetOrCreateHostCertAsync()
                                   SslStream MITM → InterceptHttpStreamAsync
                                        ├─ CaptureRepository (SQLite/PG)
                                        └─ ICapturePublisher → SignalR → dashboard
```

### Key service lifetimes (Program.cs)

- `ExplicitProxyServer`, `CertificateAuthority`, `ProxyService`, `SignalRCapturePublisher` — **Singleton** (TCP listener must live for the app lifetime).
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

Phase 1 is fully complete (backend + frontend + Docker). `IoTSpy.Protocols`, `IoTSpy.Scanner`, and `IoTSpy.Manipulation` are empty stubs. No tests exist yet. EF Core migrations should be verified before adding new ones (`MigrateAsync` calls `db.Database.MigrateAsync()`).

Next priorities per `docs/PLAN.md`:
1. Phase 2: MQTT decoder (`IoTSpy.Protocols`) — MQTT 3.1.1 / 5.0 packet parsing
2. Phase 2: GatewayRedirect / ArpSpoof modes (`IoTSpy.Proxy`)
3. Phase 2: Real-time stream filter subscriptions per device (SignalR groups)

See `docs/architecture.md` for full architecture spec and `docs/PLAN.md` for the phased task list.
