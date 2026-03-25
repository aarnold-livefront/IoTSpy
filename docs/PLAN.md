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
| Test projects | 8 (Core.Tests, Protocols.Tests, Manipulation.Tests, Scanner.Tests, Api.Tests, Proxy.Tests, Storage.Tests, Api.IntegrationTests) |
| Backend tests | 300+ (all passing) — includes Phase 10 decoders + Phase 11 TLS parser/SSL strip/model/auth tests |
| Frontend tests | 11 component tests via Vitest + React Testing Library |
| REST controllers | 13 (Auth, Proxy, Captures, Devices, Certificates, Scanner, Manipulation, PacketCapture, OpenRtb, ProtocolProxy, Report, ScheduledScan, Dashboard) |
| HTTP endpoints | 60+ |
| SignalR hubs | 2 (TrafficHub, PacketCaptureHub) — TrafficHub extended with WebSocket frame + MQTT message + anomaly alert subscriptions |
| EF Core migrations | 11 (InitialCreate → AddPhase11MultiUserAndAudit) |
| Frontend components | 55+ TypeScript files across 14 component directories |
| Protocols supported | HTTP/HTTPS, TLS passthrough (JA3/JA3S), WebSocket, MQTT 3.1.1/5.0 (passive decode + active proxy), DNS/mDNS, CoAP (passive decode + active proxy), gRPC/Protobuf, Modbus TCP, OpenRTB 2.5, Datadog, Firehose, Splunk HEC, Azure Monitor |
| CI | GitHub Actions (`.github/workflows/ci.yml`) — build, test, lint, coverage on push/PR |

---

## What has been built

All phases 1–11, plus OpenRTB inspection and TLS passthrough/SSL stripping, are complete.

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

### Phase 11 — UX, multi-user & technical debt

**Goal:** Polish UX, support team usage, and address remaining technical debt.

- **Multi-user RBAC** — `User` model with `UserRole` enum (Admin/Operator/Viewer); `IUserRepository` + `UserRepository`; JWT claims include `NameIdentifier` + `Role`; admin-only user CRUD endpoints; backward-compatible with legacy single-user auth
- **Audit log** — `AuditEntry` model + `IAuditRepository`; tracks login, user CRUD; admin-only GET `/api/auth/audit`
- **Dashboard customization** — `DashboardLayout` model with JSON-serialized layout/filters; per-user CRUD via `DashboardController`
- **Dark mode** — CSS custom properties with `[data-theme="dark"|"light"]`; `useTheme` hook; persisted in localStorage; toggle in header
- **Responsive layout** — `responsive.css` with breakpoints at 480px, 768px, 1024px; stacked split panes on mobile; scrollable view toggles
- **Onboarding wizard** — `OnboardingWizard` component (5 steps: welcome, proxy mode, TLS setup, device setup, completion); shows on first authenticated visit; persisted dismissal in localStorage
- **TLS parser tests** — `TlsClientHelloParserTests` (13 tests): SNI extraction, cipher suites, extensions, JA3, GREASE filtering, edge cases
- **TLS server parser tests** — `TlsServerHelloParserTests` (11 tests): cipher suite, version, JA3S, TLS 1.3 supported_versions, edge cases
- **SSL strip tests** — `SslStripServiceTests` (14 tests): redirect detection, HSTS stripping, URL rewriting, body rewriting
- **Core model tests** — `IoTSpy.Core.Tests` project (30+ tests): model defaults, enum coverage, new User/AuditEntry/DashboardLayout models
- **Auth controller tests updated** — 10 tests covering multi-user + legacy auth
- EF Core migration `AddPhase11MultiUserAndAudit` — adds `Users`, `AuditEntries`, `DashboardLayouts` tables

---

## Remaining gaps and technical debt

Items that are still open. These inform the roadmap.

| Gap | Description | Severity |
|---|---|---|
| No HTTPS for the API itself | The API serves on plain HTTP; TLS termination is assumed external | Low |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low |

**Resolved in Phase 11:**
- ~~No Core model tests~~ — `IoTSpy.Core.Tests` project added with 30+ model default/enum tests
- ~~No multi-user support~~ — Multi-user RBAC with `User` model, `UserRole` enum (Admin/Operator/Viewer), user management endpoints
- ~~Dashboard not responsive~~ — Responsive CSS with mobile breakpoints (480px, 768px, 1024px)
- ~~TLS passthrough/SSL strip untested~~ — `TlsClientHelloParserTests` (13 tests), `TlsServerHelloParserTests` (11 tests), `SslStripServiceTests` (14 tests)

---

## Roadmap — what comes next

### Phase 11 — UX & multi-user ✅ Complete

| # | Task | Status | Details |
|---|---|---|---|
| 11.1 | Responsive/mobile-friendly dashboard layout | ✅ Complete | `responsive.css` with mobile breakpoints at 480px, 768px, 1024px; stacked split panes, scrollable view toggles |
| 11.2 | Dark mode theme toggle | ✅ Complete | `[data-theme]` CSS custom properties; `useTheme` hook; persisted in localStorage; toggle button in header |
| 11.3 | Multi-user authentication with RBAC | ✅ Complete | `User` model with `UserRole` enum (Admin/Operator/Viewer); `IUserRepository`; JWT claims include `sub` + `role`; admin-only user CRUD endpoints; backward-compatible with legacy single-user auth |
| 11.4 | Audit log | ✅ Complete | `AuditEntry` model + `IAuditRepository`; tracked: login, user CRUD; admin-only `/api/auth/audit` endpoint |
| 11.5 | Dashboard customization | ✅ Complete | `DashboardLayout` model + `IDashboardLayoutRepository`; per-user saved layouts with JSON config; `DashboardController` CRUD |
| 11.6 | Onboarding wizard | ✅ Complete | `OnboardingWizard` component (5 steps: welcome, proxy mode, TLS setup, device, done); persisted in localStorage; shows on first authenticated visit |

---

### Phase 12 — Threat Intelligence & Advanced Detection

**Goal:** Enrich scan findings and traffic analysis with external threat intelligence and improve automated detection capabilities.

| # | Task | Priority | Details |
|---|---|---|---|
| 12.1 | Shodan integration | High | Query Shodan for open ports/vulnerabilities on scanned device IPs; surface results as `ScanFinding` entries with `ScanFindingType.Shodan` |
| 12.2 | VirusTotal URL/IP lookup | High | Check captured hostnames/IPs against VirusTotal API; flag malicious destinations in the captures list |
| 12.3 | Local CVE database cache | Medium | Download NVD/OSV CVE data on a schedule so CVE lookups work offline and reduce API rate-limit exposure |
| 12.4 | MISP threat feed ingestion | Medium | Pull IOCs (IPs, domains, hashes) from a configured MISP instance; auto-tag matching captures |
| 12.5 | Passive OS fingerprinting | Medium | Derive OS/device type from TCP/IP stack behaviour (TTL, window size, options) in packet captures; enrich `Device` records |
| 12.6 | DGA domain detection | High | ML/heuristic model to classify captured DNS names as likely DGA (entropy, n-gram analysis); emit `AnomalyAlert` on match |
| 12.7 | Threat intelligence frontend panel | High | New "Threat Intel" tab: per-device IOC matches, VirusTotal reports, CVE timeline, flagged captures |

Backend: `IoTSpy.Core` — `ThreatIntelMatch`, `IOC`, `OsFingerprint` models; `IThreatIntelService`, `IOsFingerprinter` interfaces. `IoTSpy.Scanner` — `ThreatIntelService`, `OsFingerprinter`. `IoTSpy.Storage` — `ThreatIntelMatches` DbSet + migration.

---

### Phase 13 — PCAP Import & Offline Analysis

**Goal:** Allow users to upload and analyze existing PCAP files, not just live captures.

| # | Task | Priority | Details |
|---|---|---|---|
| 13.1 | PCAP file upload endpoint | High | `POST /api/packet-capture/import` — multipart upload; parse with SharpPcap `OfflinePcapDevice`; insert packets into DB with `Source=Import` tag |
| 13.2 | Import progress streaming | Medium | Stream import progress via SignalR `PacketCaptureHub` so the frontend can show a live progress bar |
| 13.3 | PCAP import frontend | High | Drag-and-drop upload zone in `PanelPacketCapture`; shows import job status + packet count |
| 13.4 | pcapng support | Medium | Extend the importer to handle pcapng (next-generation PCAP) via SharpPcap's built-in support |
| 13.5 | Session reconstruction | Medium | TCP stream reassembly on imported PCAP data to extract HTTP/MQTT/CoAP payloads; store as `CapturedRequest` entries for manipulation analysis |
| 13.6 | Diff view for imported vs. live | Low | Side-by-side comparison of an imported PCAP session against a live capture of the same device |

Backend: `IoTSpy.Scanner` — `PcapImportService`; `IoTSpy.Core` — `PcapImportJob`, `IPcapImportService`.

---

### Phase 14 — API Key Management & Service Accounts

**Goal:** Enable programmatic/CI access to IoTSpy without sharing user credentials.

| # | Task | Priority | Details |
|---|---|---|---|
| 14.1 | `ApiKey` model & repository | High | `ApiKey` entity: name, hashed secret, scopes, expiry, last-used timestamp, owner user ID; `IApiKeyRepository` |
| 14.2 | API key issuance endpoint | High | `POST /api/auth/api-keys` (admin/operator) — generates a random 32-byte key, returns it once in plaintext, stores PBKDF2 hash |
| 14.3 | API key authentication middleware | High | `ApiKeyAuthenticationHandler` — accepts `X-Api-Key` header; resolves key → user principal with role claims |
| 14.4 | Scope enforcement | Medium | Fine-grained scope strings (`captures:read`, `scanner:write`, etc.) validated per endpoint via policy attribute |
| 14.5 | Key rotation & revocation | Medium | `DELETE /api/auth/api-keys/{id}`, `POST /api/auth/api-keys/{id}/rotate`; rotation issues replacement and revokes old key |
| 14.6 | API key frontend management | Medium | Admin panel for listing/creating/revoking keys; copy-to-clipboard on creation |
| 14.7 | EF Core migration | High | `AddApiKeyManagement` migration — `ApiKeys` table |

---

### Phase 15 — Collaboration & Real-time Sharing

**Goal:** Support multiple operators monitoring the same device/proxy session simultaneously with role-aware views.

| # | Task | Priority | Details |
|---|---|---|---|
| 15.1 | Shared capture sessions | High | Named "investigation sessions" that multiple users can join; all captures within a session visible to all participants in real time |
| 15.2 | In-session annotations | Medium | Users can annotate individual captures with notes/tags; annotations stored in DB, broadcast via SignalR |
| 15.3 | Viewer role restrictions | High | Viewer role enforced at SignalR hub level: read-only groups, no rule/script application |
| 15.4 | Presence indicators | Low | Show which users are currently active on which device/panel in the dashboard header |
| 15.5 | Activity feed | Medium | Per-session activity log (user X started scan, user Y added rule) broadcast to all participants |
| 15.6 | Session export | Medium | Export a complete investigation session (captures + annotations + scan findings + manipulation rules) as a ZIP archive |

Backend: `IoTSpy.Core` — `InvestigationSession`, `CaptureAnnotation` models; `IoTSpy.Storage` — migration.

---

### Phase 16 — Deployment & Operations

**Goal:** Make IoTSpy production-ready for team deployments with proper TLS, container orchestration, and operational tooling.

| # | Task | Priority | Details |
|---|---|---|---|
| 16.1 | Kestrel HTTPS for the API | High | Configure Kestrel to serve the REST + SignalR API over TLS; support certificate path + password config or Let's Encrypt via `Certes` |
| 16.2 | Kubernetes Helm chart | Medium | Helm chart in `deploy/helm/iotspy/`; values for replica count, image tag, DB connection, secret management; Ingress with TLS annotation |
| 16.3 | Docker Compose improvements | Medium | Add a `docker-compose.prod.yml` with Postgres, pgAdmin, and Traefik reverse proxy; separate from dev compose |
| 16.4 | Plugin system for protocol decoders | Medium | `IPluginDecoder` interface loaded from a `plugins/` directory via MEF/AssemblyLoadContext; allows community-contributed decoders without rebuilding |
| 16.5 | LDAP / SAML SSO | Low | External identity provider integration; map LDAP groups to `UserRole`; `ExternalAuthController` |
| 16.6 | Metrics endpoint (Prometheus) | Medium | `GET /metrics` via `prometheus-net.AspNetCore`; expose proxy request counts, scan durations, anomaly alert rates, capture queue depth |
| 16.7 | Alerting integrations | Medium | Webhook targets for Slack, Teams, and PagerDuty; triggered on high-severity scan findings and anomaly alerts |
| 16.8 | Distributed mode (multi-node) | Low | Redis-backed SignalR backplane; shared Postgres; multiple IoTSpy nodes behind a load balancer covering different network segments |

---

### Phase 17 — Protocol Expansion (Non-IP IoT)

**Goal:** Extend coverage to wireless IoT protocols beyond TCP/IP networking.

| # | Task | Priority | Details |
|---|---|---|---|
| 17.1 | AMQP 1.0 decoder | Medium | Decode AMQP frames captured via transparent proxy or PCAP import; surface messages alongside MQTT |
| 17.2 | RTSP/RTP for IP cameras | Medium | Detect RTSP `DESCRIBE`/`SETUP`/`PLAY` sequences; capture SDP metadata; flag unauthenticated streams |
| 17.3 | Matter/Thread protocol support | Low | Passive decode of Matter commissioning and cluster messages; Thread network topology mapping (requires USB border router) |
| 17.4 | Zigbee passive capture | Low | USB Zigbee sniffer integration (e.g. RZUSBSTICK / CC2531) via `libusb`; decode ZDP/ZCL frames |
| 17.5 | Bluetooth LE advertisement decode | Low | HCI socket or BlueZ integration; decode BLE advertisements from IoT beacons; map to known vendor profiles (Eddystone, iBeacon, Tile) |
| 17.6 | Z-Wave frame decode | Low | Serial port integration with a Z-Wave controller; decode Z-Wave frames and map to device/command class |

---

## Key design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Proxy modes | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method |
| Storage | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres |
| Auth | Multi-user JWT with RBAC (Admin/Operator/Viewer), PBKDF2 hash in DB | Team tool with role-based access; backward-compatible with legacy single-user mode |
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
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` — 11 migrations applied: InitialCreate, AddPhase2ProxySettings, AddPhase3Scanner, AddPhase4ManipulationFix, AddOpenRtbInspection, AddPacketCapture, AddMissingPhase7Changes, AddPhase9ScheduledScans, AddBodyCaptureDefaults, AddTlsPassthroughAndSslStrip, AddPhase11MultiUserAndAudit. Run `dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` from the repo root.
- **Multi-user RBAC** — `User` model with `UserRole` enum (Admin/Operator/Viewer); `IUserRepository`; JWT claims include `sub` (user ID) + `role`; admin-only user CRUD in `AuthController`; backward-compatible with legacy single-user auth (falls back to `ProxySettings.PasswordHash` when no `User` record matches).
- **Audit log** — `AuditEntry` model + `IAuditRepository`; tracks login, user CRUD; admin-only GET `/api/auth/audit`.
- **Dashboard customization** — `DashboardLayout` model + `IDashboardLayoutRepository`; per-user saved layouts with JSON config; `DashboardController` CRUD.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- `AnomalyDetector` is wired into both proxy servers. After each captured request, `IAnomalyDetector.Record()` is called; alerts published via `IAnomalyAlertPublisher` → `SignalRAnomalyPublisher` → `TrafficHub` group `"anomaly-alerts"`.
- `DataRetentionService` runs as a background `IHostedService`. Disabled by default (`DataRetention:Enabled: false`).
- TLS passthrough records `CapturedRequest` entries with `Protocol=TlsPassthrough` and `TlsMetadataJson` (serialized `TlsMetadata` model). All passthrough log entries include `DnsCorrelationKey={ClientIp}→{SniHostname}` for DNS packet capture correlation.
- SSL stripping is enabled via `ProxySettings.SslStrip=true`. It only applies to plain HTTP connections — intercepts HTTPS redirects, follows upstream TLS, strips HSTS, rewrites `https://` links.
