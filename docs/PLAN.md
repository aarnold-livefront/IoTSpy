# IoTSpy — Implementation Plan & Session Handoff

This document captures the phased implementation plan, current progress, identified gaps, and the forward-looking roadmap. It serves as the single source of truth for any new contributor or Claude Code session to resume work.

---

## Project context

IoTSpy is an IoT network security research platform. The goal is a self-hosted tool that can be pointed at any IoT device to capture, inspect, and manipulate its network traffic, then run automated pen-test checks against the device.

Full architecture: [`docs/architecture.md`](architecture.md)
README / quick start: [`README.md`](../README.md)

---

## Completed phases (1–6 + OpenRTB)

All foundational phases are complete. Below is a summary of what was delivered.

### Phase 1 — Foundation ✅

**Goal:** Running proxy server + REST API + basic dashboard scaffold.

| # | Task | Status |
|---|---|---|
| 1.1 | Solution scaffold — 7 projects, .sln, nuget.config | ✅ Done |
| 1.2 | `IoTSpy.Core` — domain models, interfaces, enums | ✅ Done |
| 1.3 | `IoTSpy.Storage` — EF Core, SQLite + Postgres, migrations | ✅ Done |
| 1.4 | `IoTSpy.Proxy` — ExplicitProxyServer (HTTP + HTTPS CONNECT) | ✅ Done |
| 1.5 | `IoTSpy.Proxy` — TLS MITM CA (BouncyCastle) | ✅ Done |
| 1.6 | `IoTSpy.Proxy` — Polly 8 resilience pipelines | ✅ Done |
| 1.7 | `IoTSpy.Api` — JWT auth (single-user, PBKDF2), setup endpoint | ✅ Done |
| 1.8 | `IoTSpy.Api` — ProxyController, CapturesController, DevicesController, CertificatesController | ✅ Done |
| 1.9 | `IoTSpy.Api` — SignalR TrafficHub + SignalRCapturePublisher | ✅ Done |
| 1.10 | `IoTSpy.Api` — CORS, OpenAPI (Scalar), DB auto-migration on startup | ✅ Done |
| 1.11 | Frontend scaffold (Vite + React 19 + TypeScript) | ✅ Done |
| 1.12 | Frontend — split-pane capture list + detail view | ✅ Done |
| 1.13 | Frontend — proxy start/stop controls + settings form | ✅ Done |
| 1.14 | Frontend — CA download button | ✅ Done |
| 1.15 | Frontend — SignalR live stream connection | ✅ Done |
| 1.16 | Docker / docker-compose for single-command local run | ✅ Done |
| 1.17 | `.gitignore` updates, README, docs | ✅ Done |

---

### Phase 2 — Additional interception modes + protocols ✅

| # | Task | Status |
|---|---|---|
| 2.1 | `IoTSpy.Protocols` — MQTT 3.1.1 / 5.0 decoder | ✅ Done |
| 2.2 | `IoTSpy.Protocols` — DNS / mDNS decoder | ✅ Done |
| 2.3 | `IoTSpy.Proxy` — GatewayRedirect mode (iptables REDIRECT) | ✅ Done |
| 2.4 | `IoTSpy.Proxy` — ArpSpoof mode (SharpPcap) | ✅ Done |
| 2.5 | `IoTSpy.Api` — Real-time stream improvements (filter subscriptions) | ✅ Done |
| 2.6 | Frontend — timeline swimlane view per device | ✅ Done |

---

### Phase 3 — Pen-test suite ✅

| # | Task | Status |
|---|---|---|
| 3.1 | `IoTSpy.Scanner` — TCP port scan | ✅ Done |
| 3.2 | `IoTSpy.Scanner` — Service fingerprinting (banner grab + CPE) | ✅ Done |
| 3.3 | `IoTSpy.Scanner` — Default credential testing | ✅ Done |
| 3.4 | `IoTSpy.Scanner` — CVE lookup (NVD / OSV APIs) | ✅ Done |
| 3.5 | `IoTSpy.Scanner` — Config audit (Telnet, UPnP, anon MQTT, etc.) | ✅ Done |
| 3.6 | `IoTSpy.Api` — ScannerController | ✅ Done |
| 3.7 | Frontend — scan results panel | ✅ Done |

---

### Phase 4 — Active manipulation ✅

| # | Task | Status |
|---|---|---|
| 4.1 | `IoTSpy.Manipulation` — Declarative rules engine (match → modify) | ✅ Done |
| 4.2 | `IoTSpy.Manipulation` — C# Roslyn scripted breakpoints | ✅ Done |
| 4.3 | `IoTSpy.Manipulation` — JavaScript (Jint) scripted breakpoints | ✅ Done |
| 4.4 | `IoTSpy.Manipulation` — Request replay with diff view | ✅ Done |
| 4.5 | `IoTSpy.Manipulation` — Mutation-based fuzzer | ✅ Done |
| 4.6 | `IoTSpy.Api` — ManipulationController | ✅ Done |
| 4.7 | Frontend — rules editor, breakpoint UI, replay panel, fuzzer panel | ✅ Done |

---

### Phase 5 — AI mock + advanced protocol decoding ✅

| # | Task | Status |
|---|---|---|
| 5.1 | `IoTSpy.Manipulation` — AI mock engine (schema learning + LLM response) | ✅ Done |
| 5.2 | AI provider abstraction: Claude / OpenAI / Ollama | ✅ Done |
| 5.3 | `IoTSpy.Protocols` — CoAP UDP decoder | ✅ Done |
| 5.4 | `IoTSpy.Protocols` — Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor) | ✅ Done |
| 5.5 | `IoTSpy.Protocols` — Anomaly detection (statistical baseline + alert) | ✅ Done |

---

### Phase 6 — Packet capture analysis & device selection ✅

| # | Task | Status |
|---|---|---|
| 6.1 | `IoTSpy.Core` — `PacketFilterDto`, `FreezeFrameResult`, `ProtocolDistribution` models/interfaces | ✅ Done |
| 6.2 | `IoTSpy.Api` — `PacketCaptureController` with 14 analysis endpoints | ✅ Done |
| 6.3 | `IPacketCaptureAnalyzer` interface in Core | ✅ Done |
| 6.4 | `PacketCaptureAnalyzer` service implementation | ✅ Done |
| 6.5 | Frontend — packet capture panel UI with device selector + tabbed analysis layout | ✅ Done |
| 6.6 | Frontend — robust filter controls (protocol, IP/port ranges, time window, payload search) | ✅ Done |
| 6.7 | Frontend — freeze frame viewer with hex dump and layer-by-layer analysis | ✅ Done |
| 6.8 | Frontend — protocol distribution visualization (bar charts) | ✅ Done |
| 6.9 | Frontend — communication pattern explorer (top N source→dest pairs) | ✅ Done |
| 6.10 | Frontend — suspicious activity detection panel with severity indicators | ✅ Done |
| 6.11 | TypeScript API client for packet analysis endpoints (`packetCapture.ts`) | ✅ Done |
| 6.12 | React hooks for analysis data (`usePacketAnalysis`) | ✅ Done |

---

### OpenRTB Traffic Inspection (added post-Phase 6) ✅

| # | Task | Status |
|---|---|---|
| OR.1 | `IoTSpy.Protocols` — `OpenRtbDecoder` (OpenRTB 2.5 bid request/response parsing) | ✅ Done |
| OR.2 | `IoTSpy.Core` — `OpenRtbEvent`, `OpenRtbPiiPolicy` models; `IOpenRtbEventRepository`, `IOpenRtbPiiPolicyRepository`, `IOpenRtbService` interfaces | ✅ Done |
| OR.3 | `IoTSpy.Manipulation` — `OpenRtbPiiService` (PII detection + redaction strategies) | ✅ Done |
| OR.4 | `IoTSpy.Storage` — `OpenRtbEvents`, `OpenRtbPiiPolicies` DbSets + repositories + `AddOpenRtbInspection` migration | ✅ Done |
| OR.5 | `IoTSpy.Api` — `OpenRtbController` (events CRUD, PII policies CRUD, PII audit logs, stripping rules) | ✅ Done |
| OR.6 | `IoTSpy.Proxy` — Inline OpenRTB detection in both proxy servers | ✅ Done |
| OR.7 | Frontend — `OpenRtbPanel`, `OpenRtbTrafficList`, `OpenRtbInspector`, `PiiPolicyEditor`, `PiiAuditLog` | ✅ Done |
| OR.8 | Frontend — `openrtb.ts` API client, `useOpenRtb` hook | ✅ Done |
| OR.9 | Tests — `OpenRtbDecoderTests`, `OpenRtbPiiServiceTests` | ✅ Done |

---

## Current statistics

| Metric | Value |
|---|---|
| Backend projects | 7 (Core, Proxy, Protocols, Scanner, Manipulation, Storage, Api) |
| Test projects | 3 (Protocols.Tests, Manipulation.Tests, Scanner.Tests) |
| Total test classes | 14 |
| REST controllers | 9 (Auth, Proxy, Captures, Devices, Certificates, Scanner, Manipulation, PacketCapture, OpenRtb) |
| HTTP endpoints | 40+ |
| SignalR hubs | 2 (TrafficHub, PacketCaptureHub) |
| EF Core migrations | 6 (InitialCreate → AddPacketCapture) |
| Frontend components | 50+ TypeScript files across 13 component directories |
| Protocols supported | HTTP/HTTPS, MQTT 3.1.1/5.0, DNS/mDNS, CoAP, OpenRTB 2.5, Datadog, Firehose, Splunk HEC, Azure Monitor |

---

## Identified gaps and technical debt

The following items represent known gaps, incomplete integrations, or areas where the codebase would benefit from additional work. These inform the forward-looking roadmap below.

### Integration gaps

| Gap | Description | Severity |
|---|---|---|
| Anomaly detector not wired | `IAnomalyDetector` / `AnomalyDetector` exists in Core+Protocols but is not integrated into the proxy pipeline — it is standalone and must be called manually | Medium |
| No WebSocket interception | The proxy intercepts HTTP/HTTPS only; WebSocket upgrade frames pass through without content capture or inspection | Medium |
| No MQTT proxy mode | MQTT is decoded from packet captures but not actively proxied/intercepted at the application layer | Low |
| CoAP decoder is passive | CoAP is decoded from captures but there is no CoAP proxy or interception capability | Low |

### Test coverage gaps

| Gap | Description | Severity |
|---|---|---|
| No Api controller tests | 9 controllers with 40+ endpoints have zero unit or integration tests | High |
| No Proxy tests | `ExplicitProxyServer`, `TransparentProxyServer`, `CertificateAuthority`, resilience pipelines — all untested | High |
| No Storage/repository tests | EF Core repositories have no unit tests (in-memory provider or SQLite) | Medium |
| No Core model tests | Domain model validation/logic untested | Low |
| No frontend tests | 50+ React components, hooks, and API clients have zero tests (no Vitest/Jest setup) | High |
| No integration/E2E tests | No end-to-end test that boots the API and exercises the full request flow | Medium |

### Infrastructure gaps

| Gap | Description | Severity |
|---|---|---|
| No CI/CD pipeline | No GitHub Actions, Azure DevOps, or other CI configuration | High |
| No health check endpoint | No `/health` or `/ready` endpoint for container orchestration | Medium |
| No structured logging | Using `ILogger` but no structured sink (Serilog, Seq) for production use | Low |
| No rate limiting | API endpoints have no throttling — a concern if exposed beyond localhost | Medium |
| No HTTPS for the API itself | The API serves on plain HTTP; TLS termination is assumed external | Low |

### Feature gaps

| Gap | Description | Severity |
|---|---|---|
| No export/reporting | No PDF/HTML scan report generation, no CSV/JSON export of findings or captures | Medium |
| No data retention policies | Captures and packets accumulate without automatic cleanup or TTL | Medium |
| No alerting/notifications | Anomaly detection and suspicious activity findings have no notification mechanism (email, webhook, Slack) | Low |
| No multi-user support | Single-user JWT model — no RBAC, no audit trail of who did what | Low |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low |
| Dashboard not responsive | Frontend layout assumes desktop-width screens | Low |

---

## Forward-looking roadmap

### Phase 7 — Test coverage & CI/CD ✅

**Goal:** Establish a testing baseline and automated build/test pipeline.

| # | Task | Status |
|---|---|---|
| 7.1 | `IoTSpy.Api.Tests` — controller unit tests with mocked services (NSubstitute + manual DI) | ✅ Done |
| 7.2 | `IoTSpy.Proxy.Tests` — proxy service unit tests (ResilienceOptions, ProxyService state) | ✅ Done |
| 7.3 | `IoTSpy.Storage.Tests` — repository tests using EF Core SQLite in-memory provider | ✅ Done |
| 7.4 | `IoTSpy.Api.IntegrationTests` — boot API via `WebApplicationFactory`, exercise full HTTP auth + devices pipeline | ✅ Done |
| 7.5 | Frontend test setup — Vitest + React Testing Library + 11 component tests (ErrorBanner, LoadingSpinner, HeadersViewer) | ✅ Done |
| 7.6 | GitHub Actions CI workflow — build, test, lint on PR/push (`.github/workflows/ci.yml`) | ✅ Done |
| 7.7 | Code coverage reporting (Coverlet + ReportGenerator via `Directory.Build.props` + CI artifact upload) | ✅ Done |

---

### Phase 8 — Observability & production hardening ✅

**Goal:** Make IoTSpy reliable for long-running deployments.

| # | Task | Status |
|---|---|---|
| 8.1 | Health check endpoints (`/health`, `/ready`) for Docker/K8s probes | ✅ Done |
| 8.2 | Structured logging (Serilog + configurable sinks: console, file, Seq) | ✅ Done |
| 8.3 | API rate limiting (ASP.NET Core `RateLimiter` middleware) | ✅ Done |
| 8.4 | Data retention policies — configurable TTL for captures, packets, scan jobs with background cleanup | ✅ Done |
| 8.5 | Wire `AnomalyDetector` into the proxy pipeline — real-time anomaly alerts via SignalR | ✅ Done |
| 8.6 | Graceful shutdown — drain active proxy connections, flush pending SignalR messages | ✅ Done |
| 8.7 | Database connection pooling tuning + Postgres-specific optimizations | ✅ Done |

---

### Phase 9 — Export & reporting

**Goal:** Allow users to extract actionable outputs from IoTSpy.

| # | Task | Status |
|---|---|---|
| 9.1 | Scan report generation — HTML/PDF summary of scan findings per device, grouped by severity | ✅ Done |
| 9.2 | Capture export — CSV/JSON/HAR export of captured HTTP traffic | ✅ Done |
| 9.3 | PCAP export improvements — filtered export (by protocol, IP, time range) | ✅ Done |
| 9.4 | Alerting — webhook/email notifications when anomaly detector or suspicious activity triggers fire | ✅ Done |
| 9.5 | Scheduled scans — cron-like recurring scan jobs with result comparison (drift detection) | ✅ Done |

---

### Phase 10 — Protocol expansion & active protocol proxying

**Goal:** Extend beyond passive protocol decoding to active interception.

| # | Task | Status |
|---|---|---|
| 10.1 | WebSocket interception — capture and display WebSocket frames in the proxy pipeline | ⬚ Not started |
| 10.2 | MQTT broker proxy — transparent MQTT MITM with topic-level inspection and manipulation | ⬚ Not started |
| 10.3 | CoAP proxy — UDP-based interception for constrained IoT devices | ⬚ Not started |
| 10.4 | gRPC/Protobuf decoding — recognize and decode gRPC traffic passing through the proxy | ⬚ Not started |
| 10.5 | Modbus TCP decoder — industrial IoT protocol support | ⬚ Not started |

---

### Phase 11 — UX & multi-user

**Goal:** Polish the user experience and support team usage.

| # | Task | Status |
|---|---|---|
| 11.1 | Responsive/mobile-friendly dashboard layout | ⬚ Not started |
| 11.2 | Dark mode theme toggle | ⬚ Not started |
| 11.3 | Multi-user authentication with role-based access (admin, viewer, operator) | ⬚ Not started |
| 11.4 | Audit log — track user actions (scan started, rule created, replay executed, etc.) | ⬚ Not started |
| 11.5 | Dashboard customization — user-configurable panel layout, saved views/filters | ⬚ Not started |
| 11.6 | Onboarding wizard — guided first-run flow for device setup + CA installation | ⬚ Not started |

---

## Resuming this project

### Current status — all phases complete through 9

**Phases 1–9 and OpenRTB are complete.** The codebase is fully functional with:

- 9 REST controllers, 40+ endpoints, 2 SignalR hubs
- 3 proxy modes (explicit, gateway/iptables, ARP spoof)
- Protocol decoders: MQTT 3.1.1/5.0, DNS/mDNS, CoAP, OpenRTB 2.5, 4 telemetry formats
- Pen-test suite: port scan, fingerprinting, credential testing, CVE lookup, config audit
- Traffic manipulation: rules engine, C#/JS scripted breakpoints, replay, fuzzer, AI mock
- Packet capture with protocol analysis, pattern detection, suspicious activity alerts
- OpenRTB traffic inspection with PII detection and policy-based redaction
- Full React frontend with real-time SignalR streaming
- **7 test projects**: Protocols.Tests, Manipulation.Tests, Scanner.Tests (existing) + Api.Tests, Proxy.Tests, Storage.Tests, Api.IntegrationTests (Phase 7)
- **Frontend tests**: 11 component tests via Vitest + React Testing Library
- **GitHub Actions CI**: `.github/workflows/ci.yml` — build, test, lint on PR/push with coverage artifact upload

**Next recommended focus: Phase 10 (protocol expansion & active protocol proxying)**.

---

## Key design decisions already made

| Decision | Choice | Rationale |
|---|---|---|
| Proxy modes | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method |
| Storage | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres |
| Auth | Single-user JWT, PBKDF2 hash in DB | Simple; IoTSpy is a personal/team tool, not multi-tenant |
| TLS MITM | BouncyCastle (pure .NET) | No native dependency; works cross-platform |
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
  "Resilience": { /* timeouts, retry, circuit-breaker */ }
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

- `IoTSpy.Protocols` has MQTT, DNS/mDNS, CoAP, OpenRTB, and four telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor). `IoTSpy.Scanner` has port scan, fingerprinting, credential testing, CVE lookup, config audit, and packet capture. `IoTSpy.Manipulation` has rules engine, scripted breakpoints (C#/JS), replay, fuzzer, AI mock engine, packet capture analyzer, and OpenRTB PII service.
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` — 6 migrations applied (InitialCreate, AddPhase2ProxySettings, AddPhase3Scanner, AddPhase4ManipulationFix, AddOpenRtbInspection, AddPacketCapture). Run `dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` from the repo root when adding new entities or properties.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- Three test projects exist: `IoTSpy.Protocols.Tests` (MQTT, DNS, OpenRTB, telemetry decoders, anomaly detection), `IoTSpy.Manipulation.Tests` (rules engine, fuzzer, OpenRTB PII), `IoTSpy.Scanner.Tests` (port scanner, packet capture).
- The `IAnomalyDetector` / `AnomalyDetector` pair is in Core + Protocols respectively and is **not yet wired into the proxy pipeline** — it is available for callers to integrate (see Phase 8.5).
