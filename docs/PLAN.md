# IoTSpy — Implementation Plan & Session Handoff

This document captures the phased implementation plan, current progress, and everything needed for a new contributor (or a new Claude Code session) to resume work without losing context.

---

## Project context

IoTSpy is an IoT network security research platform. The goal is a self-hosted tool that can be pointed at any IoT device to capture, inspect, and manipulate its network traffic, then run automated pen-test checks against the device.

Full architecture: [`docs/architecture.md`](architecture.md)
README / quick start: [`README.md`](../README.md)

---

## Phased delivery plan

### Phase 1 — Foundation (current)

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
| 1.17 | `.gitignore` updates, README, docs | ✅ Done (this session) |

**Phase 1 is complete** (backend + frontend + Docker).

---

### Phase 2 — Additional interception modes + protocols

| # | Task | Status |
|---|---|---|
| 2.1 | `IoTSpy.Protocols` — MQTT 3.1.1 / 5.0 decoder | ✅ Done |
| 2.2 | `IoTSpy.Protocols` — DNS / mDNS decoder | ✅ Done |
| 2.3 | `IoTSpy.Proxy` — GatewayRedirect mode (iptables REDIRECT) | ✅ Done |
| 2.4 | `IoTSpy.Proxy` — ArpSpoof mode (SharpPcap) | ✅ Done |
| 2.5 | `IoTSpy.Api` — Real-time stream improvements (filter subscriptions) | ✅ Done |
| 2.6 | Frontend — timeline swimlane view per device | ✅ Done |

---

### Phase 3 — Pen-test suite

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

### Phase 4 — Active manipulation

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

### Phase 5 — AI mock + advanced protocol decoding

| # | Task | Status |
|---|---|---|
| 5.1 | `IoTSpy.Manipulation` — AI mock engine (schema learning + LLM response) | ✅ Done |
| 5.2 | AI provider abstraction: Claude / OpenAI / Ollama | ✅ Done |
| 5.3 | `IoTSpy.Protocols` — CoAP UDP decoder | ✅ Done |
| 5.4 | `IoTSpy.Protocols` — Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor) | ✅ Done |
| 5.5 | `IoTSpy.Protocols` — Anomaly detection (statistical baseline + alert) | ✅ Done |

---

### Phase 6 — Packet capture analysis & device selection

**Goal:** Network device selector, packet filtering UI with robust filters, freeze frame inspection, protocol pattern analysis and suspicious activity detection.

| # | Task | Status |
|---|---|---|
| 6.1 | `IoTSpy.Core` — `PacketFilterDto`, `FreezeFrameResult`, `ProtocolDistribution` models/interfaces | ✅ Done |
| 6.2 | `IoTSpy.Api` — `PacketCaptureController` with analysis endpoints | ✅ Done |
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

## Resuming this project

### What is done (Phase 1 — complete)

The entire Phase 1 backend and frontend are scaffolded and functional:

**Backend:**
- `ExplicitProxyServer` listens on `:8888`, handles HTTP and HTTPS CONNECT.
- TLS MITM works via BouncyCastle dynamic CA (root CA auto-generated, per-host leaf certs cached).
- Polly 8 resilience: per-host circuit breaker + retry + timeout for TCP connect; timeout-only for TLS handshake.
- EF Core 10 with SQLite default; Postgres switchable via config. `DateTimeOffset` stored as Unix ms (long) via `ValueConverter` for SQLite compatibility.
- JWT single-user auth with PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) password hash stored in DB.
- SignalR hub `/hubs/traffic` streams every captured request in real time.
- Five REST controllers fully implemented.
- OpenAPI (Scalar) in Development mode at `/scalar`.

**Frontend (`frontend/`):**
- Vite 6 + React 19 + TypeScript; dev server on port 3000.
- Auth flow: `/setup` (first-run password) → `/login` → dashboard. JWT in `localStorage`.
- Split-pane capture dashboard with draggable divider: capture list on left, request/response/TLS detail on right.
- `CaptureFilterBar`: filter by device, method, status code, host, date range, body search.
- SignalR live stream via `useTrafficStream` — new captures prepended in real time.
- Proxy start/stop + settings modal (port, listen address, TLS/body capture flags).
- CA download link (no auth required on that endpoint).
- React Context + `useReducer` for auth state; no Redux.

### Current status — all phases complete through 6

**All six phases are complete.** The codebase is fully implemented per the plan above.

Phase 6 additions (completed last):
- `PacketCaptureService` (IoTSpy.Scanner) — SharpPcap live capture with LinkedList ring buffer (10k cap), protocol parsing (TCP/UDP/ARP/ICMP/DNS/HTTP/MQTT/CoAP/DHCP/SSH/Telnet), PCAP export via raw frame data, Wireshark-style hex dump formatting.
- `PacketCaptureAnalyzer` (IoTSpy.Manipulation.Analysis) — protocol distribution, communication pattern detection (top N pairs), suspicious activity detection (port scan, ARP spoofing, DNS anomaly, retransmission bursts).
- `PacketCaptureHub` SignalR hub at `/hubs/packets` + `SignalRPacketPublisher` for real-time packet streaming.
- `PacketCaptureController` with 14 endpoints: devices, capture lifecycle, packet filtering, freeze frame (create/get with hex dump + layer analysis), protocol distribution, communication patterns, suspicious activity.
- EF Core migration `20260309120000_AddPacketCapture` — `CaptureDevices` + `Packets` tables with indexes.
- Frontend: tabbed `PanelPacketCapture` (Packets/Protocols/Patterns/Suspicious), `PacketInspector` (Details/Hex Dump/Layers via freeze frame API), `ProtocolDistributionView` (bar charts), `PatternExplorer` (table with visual bars), `SuspiciousActivityPanel` (severity-colored cards with evidence).
- API client (`packetCapture.ts`), hooks (`usePacketCapture`, `usePacketAnalysis`), TypeScript types for all DTOs.
- 154 tests passing across 3 test projects, 0 warnings.

---

**Phase 6 is complete.** All backend services, API endpoints, frontend components, API client, hooks, and TypeScript types are implemented and tested.

---

## Key design decisions already made

| Decision | Choice | Rationale |
|---|---|---|
| Proxy modes | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method |
| Storage | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres |
| Auth | Single-user JWT, BCrypt hash in DB | Simple; IoTSpy is a personal/team tool, not multi-tenant |
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
- Git repo directory: `iotspy/` (lowercase, existing)

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
cd /path/to/iotspy

# Required env var
export Auth__JwtSecret="replace-with-32-char-minimum-secret"

dotnet run --project src/IoTSpy.Api
# → http://localhost:5000
# → Scalar API docs: http://localhost:5000/scalar  (Development only)
```

---

## Notes for Claude Code sessions

- `IoTSpy.Protocols` has MQTT, DNS/mDNS, CoAP, and four telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor). `IoTSpy.Scanner` has port scan, fingerprinting, credential testing, CVE lookup, config audit. `IoTSpy.Manipulation` has rules engine, scripted breakpoints (C#/JS), replay, fuzzer, and AI mock engine.
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` — five migrations applied through `AddPhase4Manipulation`. Run `dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` from the repo root when adding new entities or properties.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- Three test projects exist: `IoTSpy.Protocols.Tests` (MQTT, DNS, telemetry decoders, anomaly detection), `IoTSpy.Manipulation.Tests` (rules engine, fuzzer), `IoTSpy.Scanner.Tests` (port scanner).
- The `IAnomalyDetector` / `AnomalyDetector` pair is in Core + Protocols respectively and is not yet wired into the proxy pipeline — it is available for callers to integrate.
