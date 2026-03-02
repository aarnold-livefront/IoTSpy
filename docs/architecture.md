# IoTSpy — Architecture

## Overview

IoTSpy is a .NET 10 / C# solution that functions as a transparent MITM proxy, protocol analyzer, and lightweight pen-test suite for IoT network research. The backend exposes a REST + SignalR API; the frontend is a Vinext (Cloudflare) single-page app.

---

## Solution structure

```
IoTSpy.sln
src/
  IoTSpy.Core/
  IoTSpy.Proxy/
  IoTSpy.Protocols/
  IoTSpy.Scanner/
  IoTSpy.Manipulation/
  IoTSpy.Storage/
  IoTSpy.Api/
frontend/
docs/
```

### IoTSpy.Core

Domain layer — no infrastructure dependencies.

- `Models/` — `CapturedRequest`, `Device`, `ProxySettings`, `CertificateEntry`, `ResilienceOptions`
- `Interfaces/` — `ICaptureRepository`, `IDeviceRepository`, `ICertificateRepository`, `IProxySettingsRepository`, `IProxyService`, `ICertificateAuthority`, `ICapturePublisher`
- `Enums/` — `ProxyMode` (ExplicitProxy | ArpSpoof | GatewayRedirect), `InterceptionProtocol` (Http | Https | Mqtt | CoAP | Dns)

### IoTSpy.Proxy

Interception engine.

- `Interception/ExplicitProxyServer` — TCP listener; handles HTTP CONNECT tunnels and plain HTTP. Supports optional TLS MITM.
- `Tls/CertificateAuthority` — BouncyCastle-based dynamic CA. Generates a self-signed root CA on first run (4096-bit RSA, stored in DB). Issues per-host leaf certificates (2048-bit RSA, 825-day validity, SAN).
- `Resilience/ProxyResiliencePipelines` — Polly 8 pipelines:
  - `iotspy-connect` (keyed per upstream host): Timeout → Retry (exponential backoff) → CircuitBreaker
  - `iotspy-tls`: Timeout-only (TLS handshake is not safely retryable)
- `ProxyService` — `IHostedService` + `IProxyService` façade; reads settings from DB at startup; manages `ExplicitProxyServer` lifecycle.

### IoTSpy.Protocols (Phase 2+)

Protocol decoders, each implementing a shared decoder interface:

- HTTP/HTTPS (Phase 1 — handled inline in proxy)
- MQTT 3.1.1 / 5.0 (port 1883/8883)
- CoAP UDP (port 5683) — Phase 5
- DNS / mDNS
- Telemetry: Datadog agent, AWS Firehose, Splunk HEC, Azure Monitor

### IoTSpy.Scanner (Phase 3+)

Pen-test suite:

- Port scan (TCP connect scan with concurrency limit)
- Service fingerprinting (banner grab + protocol heuristics)
- Default credential testing (configurable credential list per service type)
- CVE lookup (NVD / OSV APIs, keyed by CPE)
- Config audit: detect root SSH, Telnet, HTTP admin interfaces, UPnP, anonymous MQTT

### IoTSpy.Manipulation (Phase 4+)

Active traffic manipulation:

- Declarative rules engine (match on host/path/method/status → modify headers/body/status)
- Scripted breakpoints — C# Roslyn scripts and JavaScript (Jint)
- Request replay with diff view
- Fuzzer (mutation-based, schema-aware)
- AI mock engine (Phase 5): schema learning from captured traffic; pluggable LLM provider

### IoTSpy.Storage

EF Core 10 data access layer.

- `IoTSpyDbContext` — entities: `CapturedRequest`, `Device`, `ProxySettings`, `CertificateEntry`
- `StorageExtensions.AddIoTSpyStorage()` — registers DbContext for SQLite or PostgreSQL based on `Database:Provider` configuration key
- `DesignTimeDbContextFactory` — for `dotnet ef migrations` CLI
- Repositories: `CaptureRepository`, `DeviceRepository`, `CertificateRepository`, `ProxySettingsRepository`
- `MigrateAsync()` — called at startup to auto-apply pending migrations

### IoTSpy.Api

ASP.NET Core 10 host.

- `Program.cs` — DI wiring: Storage → Auth (JWT) → SignalR → Resilience → Proxy → Controllers → CORS
- Controllers: `AuthController`, `ProxyController`, `CapturesController`, `DevicesController`, `CertificatesController`
- `Hubs/TrafficHub` — SignalR hub; device-scoped group subscriptions
- `Hubs/SignalRCapturePublisher` — singleton `ICapturePublisher`; broadcasts to all clients and device groups
- `Services/AuthService` — BCrypt password hashing, JWT generation (issuer/audience: `iotspy`)
- OpenAPI spec via `Microsoft.AspNetCore.OpenApi`; Scalar UI at `/scalar` in Development

---

## Data flow

```
IoT Device
    │  HTTP/CONNECT :8888
    ▼
ExplicitProxyServer (TcpListener)
    │
    ├─ Plain HTTP ──────────────────────────────────────────────────┐
    │                                                               │
    └─ CONNECT tunnel                                               │
           │  CaptureTls=false → passthrough                        │
           │  CaptureTls=true  → TLS MITM                           │
           │      CertificateAuthority.GetOrCreateHostCertificate() │
           │      SslStream (client) ←→ SslStream (upstream)        │
           │                                                        │
           └─────────────────── InterceptHttpStreamAsync ───────────┘
                                      │
                              ReadHttpMessageAsync
                              WriteHttpMessageAsync
                                      │
                              ┌───────┴────────┐
                              │                │
                        CaptureRepository   ICapturePublisher
                        (SQLite/PG)         (SignalR → dashboard)
```

---

## Interception modes

| Mode | Mechanism | Status |
|---|---|---|
| ExplicitProxy | Configure device proxy to `host:8888`; IoTSpy handles HTTP CONNECT and plain HTTP | Phase 1 — implemented |
| GatewayRedirect | `iptables REDIRECT` rules on a Linux gateway to redirect TCP 80/443 to IoTSpy | Phase 2 |
| ArpSpoof | ARP poisoning to position IoTSpy as MITM on the same LAN segment | Phase 2 |

All three modes funnel into the same unified capture pipeline (`InterceptHttpStreamAsync`).

---

## Authentication

- Single-user model: one password stored as BCrypt hash in `ProxySettings`.
- `POST /api/auth/setup` — sets password (one-time; fails if already set).
- `POST /api/auth/login` — returns JWT (issuer: `iotspy`, audience: `iotspy`).
- JWT passed as `Authorization: Bearer` for REST; as `?access_token=` query param for SignalR.
- `Auth:JwtSecret` must be set in configuration (minimum 32 characters).

---

## Resilience (Polly 8)

Per-host circuit breaker pattern prevents a single dead IoT cloud endpoint from stalling the proxy.

```
iotspy-connect (keyed by upstream hostname):
  Timeout(ConnectTimeoutSeconds)
    → Retry(RetryCount, exponential, on SocketException | TimeoutRejectedException)
      → CircuitBreaker(per-host, FailureRatio over SamplingDuration, BreakDuration)

iotspy-tls:
  Timeout(TlsHandshakeTimeoutSeconds)
```

Defaults (configurable in `appsettings.json` under `Resilience`):

| Key | Default |
|---|---|
| ConnectTimeoutSeconds | 15 |
| TlsHandshakeTimeoutSeconds | 10 |
| RetryCount | 2 |
| RetryBaseDelaySeconds | 0.5 |
| CircuitBreakerFailureRatio | 0.5 |
| CircuitBreakerSamplingSeconds | 30 |
| CircuitBreakerBreakSeconds | 60 |

---

## TLS MITM detail

1. Device sends `CONNECT host:443 HTTP/1.1`.
2. Proxy replies `200 Connection established`.
3. `CertificateAuthority.GetOrCreateHostCertificateAsync(host)` returns (or generates) a leaf certificate signed by the IoTSpy root CA.
4. `SslStream.AuthenticateAsServerAsync` presents the leaf cert to the device.
5. A second `SslStream.AuthenticateAsClientAsync` connects to the real upstream (certificate validation disabled for research purposes).
6. HTTP exchanges between the two `SslStream` instances are parsed, recorded, and forwarded.

The root CA is generated lazily on first use, stored as PEM in the database, and cached in memory. Leaf certificates are cached in the database and reused until one day before expiry.

---

## Storage schema (EF Core)

| Table | Key fields |
|---|---|
| `CapturedRequests` | `Id` (GUID), `DeviceId` FK, method/scheme/host/port/path/query, request+response headers+body, TLS info, timestamp, durationMs |
| `Devices` | `Id` (GUID), `IpAddress`, `Hostname`, `Name`, `LastSeen` |
| `ProxySettings` | Single row (Id=1), port, mode, flags, password hash |
| `CertificateEntries` | `Id` (GUID), `CommonName`, `IsRootCa`, PEM fields, serial, validity range |

---

## Frontend (Vinext / Cloudflare)

Scaffold via: `npx skills add cloudflare/vinext`

Dashboard layout (Phase 1 target):

- **Split-pane detail view** (left: capture list, right: request/response detail)
- **Timeline swimlane view** (per-device horizontal lanes, toggleable)
- Real-time updates via SignalR connection to `/hubs/traffic`
- Proxy start/stop controls
- CA certificate download button

---

## AI mock engine (Phase 5)

Pluggable LLM provider, configured via `appsettings.json`:

```json
"AiMock": {
  "Provider": "claude",       // claude | openai | ollama
  "Model": "claude-sonnet-4-6",
  "ApiKey": "..."
}
```

Behavior:
1. On first request to a host, query historical `CapturedRequest` records for that host to build a schema.
2. Pass the schema + user-configured behavior description to the LLM.
3. Return the LLM-generated response body with inferred headers.
4. Cache schema per host; invalidate when new captures arrive.

---

## Protocols to be decoded (Phase 2+)

| Protocol | Port(s) | Phase |
|---|---|---|
| HTTP/HTTPS | 80 / 443 | 1 (done) |
| MQTT | 1883 / 8883 | 2 |
| DNS / mDNS | 53 / 5353 | 2 |
| CoAP | UDP 5683 | 5 |
| Datadog agent | 8126 | 5 |
| AWS Firehose | HTTPS | 5 |
| Splunk HEC | 8088 | 5 |
| Azure Monitor | HTTPS | 5 |

---

## Pen-test features (Phase 3)

- **Port scan** — TCP connect scan; configurable port ranges and concurrency.
- **Service fingerprinting** — banner grab + heuristics; emits CPE string where possible.
- **Default credential testing** — per-service credential lists (SSH, Telnet, HTTP basic/digest, MQTT, FTP).
- **CVE lookup** — NVD and OSV APIs; keyed by CPE from fingerprinting step.
- **Config audit** — detect: root SSH login enabled, Telnet open, HTTP admin on default port, UPnP responding, anonymous MQTT broker.
