# IoTSpy — Implementation Plan & Session Handoff

This document is the single source of truth for any new contributor or Claude Code session to resume work. It covers what has been built, what remains, and how to pick up where the last session left off.

---

## Project context

IoTSpy is an IoT network security research platform. The goal is a self-hosted tool that can be pointed at any IoT device to capture, inspect, and manipulate its network traffic, then run automated pen-test checks against the device.

Full architecture: [`docs/architecture.md`](architecture.md)
README / quick start: [`README.md`](../README.md)

---

## Current statistics

| Metric | Value |
|---|---|
| Backend projects | 7 (Core, Proxy, Protocols, Scanner, Manipulation, Storage, Api) |
| Test projects | 7 (Protocols.Tests, Manipulation.Tests, Scanner.Tests, Api.Tests, Proxy.Tests, Storage.Tests, Api.IntegrationTests) |
| Backend tests | 248+ (all passing) + Phase 10 decoder tests |
| Frontend tests | 11 component tests via Vitest + React Testing Library |
| REST controllers | 12 (Auth, Proxy, Captures, Devices, Certificates, Scanner, Manipulation, PacketCapture, OpenRtb, ProtocolProxy, Report, ScheduledScan) |
| HTTP endpoints | 50+ |
| SignalR hubs | 2 (TrafficHub, PacketCaptureHub) — TrafficHub extended with WebSocket frame + MQTT message + anomaly alert subscriptions |
| EF Core migrations | 10 (InitialCreate → AddTlsPassthroughAndSslStrip) |
| Frontend components | 50+ TypeScript files across 13 component directories |
| Protocols supported | HTTP/HTTPS, TLS passthrough (JA3/JA3S), WebSocket, MQTT 3.1.1/5.0 (passive decode + active proxy), DNS/mDNS, CoAP (passive decode + active proxy), gRPC/Protobuf, Modbus TCP, OpenRTB 2.5, Datadog, Firehose, Splunk HEC, Azure Monitor |
| CI | GitHub Actions (`.github/workflows/ci.yml`) — build, test, lint, coverage on push/PR |

---

## What has been built

All phases 1–10, plus OpenRTB inspection and TLS passthrough/SSL stripping, are complete.

### Phase 1 — Foundation

Proxy server + REST API + React dashboard scaffold. Explicit proxy (HTTP + HTTPS CONNECT), TLS MITM with BouncyCastle CA, Polly 8 resilience, JWT auth, SignalR live streaming, EF Core (SQLite/Postgres), Docker support.

### Phase 2 — Interception modes + protocols

MQTT 3.1.1/5.0 and DNS/mDNS decoders. Gateway redirect mode (iptables). ARP spoof mode (SharpPcap). SignalR filter subscriptions. Timeline swimlane view.

### Phase 3 — Pen-test suite

TCP port scan, service fingerprinting (banner grab + CPE), default credential testing (FTP/Telnet/MQTT), CVE lookup (OSV.dev), config audit (Telnet/UPnP/anon MQTT/exposed DB/HTTP admin). `ScannerController` + frontend.

### Phase 4 — Active manipulation

Declarative rules engine (regex match → modify/drop/delay). C# (Roslyn) and JavaScript (Jint) scripted breakpoints. Request replay. Mutation fuzzer (Random/Boundary/BitFlip). `ManipulationController` + frontend.

### Phase 5 — AI mock + advanced protocols

AI mock engine with pluggable providers (Claude/OpenAI/Ollama). CoAP UDP decoder. Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor). Statistical anomaly detection (Welford online algorithm).

### Phase 6 — Packet capture & analysis

SharpPcap live capture with ring buffer (10k). Protocol distribution, communication patterns, suspicious activity detection (port scan, ARP spoof, DNS anomaly, retransmission burst). Hex dump + freeze frame. PCAP export. `PacketCaptureController` (14 endpoints) + `PacketCaptureHub` SignalR.

### OpenRTB inspection

OpenRTB 2.5 bid request/response decoding. PII detection + policy-based redaction. Inline detection in both proxy servers. `OpenRtbController` + frontend (traffic list, inspector, PII policy editor, audit log).

### Phase 7 — Test coverage & CI/CD

7 test projects, 248+ backend tests. Controller unit tests (NSubstitute), proxy service tests, repository integration tests (EF Core SQLite in-memory), `WebApplicationFactory` integration tests. Frontend: Vitest + React Testing Library (11 tests). GitHub Actions CI with Coverlet coverage.

### Phase 8 — Observability & production hardening

Health checks (`/health`, `/ready`). Serilog structured logging (console + rolling file, 7-day retention). Rate limiting (sliding window, 100/60s). `DataRetentionService` (configurable TTLs, disabled by default). `AnomalyDetector` wired into proxy pipeline with SignalR alerts. Graceful shutdown (connection draining). DB connection pooling.

### Phase 9 — Export & reporting

HTML/PDF scan report generation (`ReportController`). Capture export (CSV/JSON/HAR). Filtered PCAP export. Alerting (webhook/email via `IAlertingService`). Scheduled scans with cron expressions (`ScheduledScanController`).

### Phase 10 — Protocol expansion & active proxying

WebSocket interception (bidirectional frame relay + capture). MQTT broker proxy (TCP MITM, topic-level wildcard filtering, SignalR message publishing). CoAP UDP forward proxy (message decoding, device registration, capture). gRPC/Protobuf decoder (LPM framing + schema-less field extraction). Modbus TCP decoder (MBAP, function codes 1-16). `ProtocolProxyController` (6 endpoints).

### TLS Passthrough & SSL Stripping

**Goal:** HTTPS visibility for IoT devices where CA installation is not possible.

- `TlsClientHelloParser` — SNI extraction, cipher suite enumeration, JA3 fingerprint with GREASE filtering (RFC 8701)
- `TlsServerHelloParser` — ServerHello parsing (selected cipher, `supported_versions` for TLS 1.3, JA3S), Certificate extraction (TLS 1.2 only — encrypted in TLS 1.3)
- `HandleTlsPassthroughAsync` — in both `ExplicitProxyServer` and `TransparentProxyServer`: buffer ClientHello, parse SNI/JA3, relay, parse ServerHello/JA3S + Certificate, count bytes, record `CapturedRequest` with `Protocol=TlsPassthrough` and `TlsMetadataJson`
- `SslStripService` — intercept HTTP→HTTPS redirects, follow upstream TLS, strip HSTS headers, rewrite `https://` links in Location/Set-Cookie/CSP headers and HTML/JSON bodies
- `DnsCorrelationKey={ClientIp}→{SniHostname}` structured logging on all passthrough events for DNS-to-TLS correlation
- `AddTlsPassthroughAndSslStrip` migration — `TlsMetadataJson` on Captures, `SslStrip` on ProxySettings

---

## Remaining gaps and technical debt

Items that are still open. These inform the roadmap.

| Gap | Description | Severity |
|---|---|---|
| No HTTPS for the API itself | The API serves on plain HTTP; TLS termination is assumed external | Low |
| No Core model tests | Domain model validation/logic untested | Low |
| No multi-user support | Single-user JWT model — no RBAC, no audit trail | Low |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low |
| Dashboard not responsive | Frontend layout assumes desktop-width screens | Low |
| TLS passthrough/SSL strip untested | No unit tests for `TlsClientHelloParser`, `TlsServerHelloParser`, `SslStripService`, or `HandleTlsPassthroughAsync` | Medium |

---

## Roadmap — what comes next

### Phase 11 — UX & multi-user

**Goal:** Polish the user experience and support team usage.

| # | Task | Status | Details |
|---|---|---|---|
| 11.1 | Responsive/mobile-friendly dashboard layout | ⬚ Not started | CSS grid/flexbox rework of all panels for mobile breakpoints |
| 11.2 | Dark mode theme toggle | ⬚ Not started | CSS custom properties theme switching; persist preference in localStorage |
| 11.3 | Multi-user authentication with RBAC | ⬚ Not started | Roles: admin, operator, viewer. Replace single-user JWT model. Add user table + migration. |
| 11.4 | Audit log | ⬚ Not started | Track user actions (scan started, rule created, replay executed). New `AuditEntry` model + DbSet. |
| 11.5 | Dashboard customization | ⬚ Not started | User-configurable panel layout, saved views/filters |
| 11.6 | Onboarding wizard | ⬚ Not started | Guided first-run flow for device setup + CA installation |

### Future ideas (unplanned)

- Bluetooth/Zigbee/Z-Wave protocol support
- HTTPS for the API itself (Kestrel HTTPS endpoint)
- PCAP import (analyze previously captured traffic)
- Plugin system for custom protocol decoders
- Collaborative session sharing (multiple users watching the same proxy in real time)

---

## Key design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Proxy modes | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method |
| Storage | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres |
| Auth | Single-user JWT, PBKDF2 hash in DB | Simple; IoTSpy is a personal/team tool, not multi-tenant |
| TLS MITM | BouncyCastle (pure .NET) | No native dependency; works cross-platform |
| TLS passthrough | Custom handshake parser + relay | Extract metadata (JA3/JA3S/SNI/cert) without breaking the connection |
| SSL stripping | HTTP-level intercept + upstream TLS fetch | Visibility into HTTPS traffic for devices that can't install CAs |
| Resilience | Per-host Polly circuit breaker | A dead IoT cloud endpoint must not stall the whole proxy |
| Real-time | SignalR | Native .NET, easy JS client, supports group subscriptions per device |
| Frontend | Vite 6 + React 19 + TypeScript | Lightweight, no framework overhead; compatible with existing CORS config |
| AI mock | Pluggable (Claude / OpenAI / Ollama) | Avoid lock-in; local Ollama for offline use |

---

## Naming conventions

- Namespace / project prefix: `IoTSpy` (capital I, o, T, S — not `Iotspy` or `IOTSPY`)
- Solution file: `IoTSpy.sln`
- Docker image / container names: `iotspy` (lowercase)
- Git repo directory: `IoTSpy/`

---

## Configuration quick reference

```jsonc
// src/IoTSpy.Api/appsettings.json (key sections)
{
  "Database": { "Provider": "sqlite|postgres", "ConnectionString": "..." },
  "Auth": { "JwtSecret": "<32+ chars>", "PasswordHash": "" },
  "Frontend": { "Origin": "http://localhost:3000" },
  "Urls": "http://localhost:5000",
  "Resilience": { /* timeouts, retry, circuit-breaker — see architecture.md */ },
  "AiMock": { "Provider": "claude|openai|ollama", "Model": "...", "ApiKey": "" },
  "Serilog": { "MinimumLevel": "Information" },
  "RateLimit": { "Enabled": true, "PermitLimit": 100, "WindowSeconds": 60 },
  "DataRetention": {
    "Enabled": false,             // opt-in; set true to enable automatic cleanup
    "CaptureRetentionDays": 30,
    "PacketRetentionDays": 7,
    "ScanJobRetentionDays": 90,
    "OpenRtbEventRetentionDays": 14
  }
}
```

Environment variable override uses double-underscore: `Auth__JwtSecret=...`

---

## Running the backend locally

```bash
cd /path/to/IoTSpy

# Required env var
export Auth__JwtSecret="replace-with-32-char-minimum-secret"

dotnet run --project src/IoTSpy.Api
# → http://localhost:5000
# → Scalar API docs: http://localhost:5000/scalar  (Development only)
```

---

## Notes for Claude Code sessions

- `IoTSpy.Proxy` has explicit + transparent proxy servers, TLS MITM CA, TLS passthrough (JA3/JA3S), SSL stripping, MQTT broker proxy, CoAP proxy, WebSocket interception, ARP spoofing, iptables helper, Polly resilience. References `IoTSpy.Protocols` for decoder access.
- `IoTSpy.Protocols` has MQTT, DNS/mDNS, CoAP, WebSocket, gRPC/Protobuf, Modbus TCP, OpenRTB, and four telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor).
- `IoTSpy.Scanner` has port scan, fingerprinting, credential testing, CVE lookup, config audit, and packet capture.
- `IoTSpy.Manipulation` has rules engine, scripted breakpoints (C#/JS), replay, fuzzer, AI mock engine, packet capture analyzer, and OpenRTB PII service.
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` — 10 migrations applied: InitialCreate, AddPhase2ProxySettings, AddPhase3Scanner, AddPhase4ManipulationFix, AddOpenRtbInspection, AddPacketCapture, AddMissingPhase7Changes, AddPhase9ScheduledScans, AddBodyCaptureDefaults, AddTlsPassthroughAndSslStrip. Run `dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` from the repo root.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- `AnomalyDetector` is wired into both proxy servers. After each captured request, `IAnomalyDetector.Record()` is called; alerts published via `IAnomalyAlertPublisher` → `SignalRAnomalyPublisher` → `TrafficHub` group `"anomaly-alerts"`.
- `DataRetentionService` runs as a background `IHostedService`. Disabled by default (`DataRetention:Enabled: false`).
- TLS passthrough records `CapturedRequest` entries with `Protocol=TlsPassthrough` and `TlsMetadataJson` (serialized `TlsMetadata` model). All passthrough log entries include `DnsCorrelationKey={ClientIp}→{SniHostname}` for DNS packet capture correlation.
- SSL stripping is enabled via `ProxySettings.SslStrip=true`. It only applies to plain HTTP connections — intercepts HTTPS redirects, follows upstream TLS, strips HSTS, rewrites `https://` links.
