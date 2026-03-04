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
| 3.7 | Frontend — scan results panel | Planned |

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
| 4.7 | Frontend — rules editor, breakpoint UI, replay panel | Planned |

---

### Phase 5 — AI mock + advanced protocol decoding

| # | Task | Status |
|---|---|---|
| 5.1 | `IoTSpy.Manipulation` — AI mock engine (schema learning + LLM response) | Planned |
| 5.2 | AI provider abstraction: Claude / OpenAI / Ollama | Planned |
| 5.3 | `IoTSpy.Protocols` — CoAP UDP decoder | Planned |
| 5.4 | `IoTSpy.Protocols` — Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor) | Planned |
| 5.5 | `IoTSpy.Protocols` — Anomaly detection (statistical baseline + alert) | Planned |

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

### What to do next

Phases 1, 2, and 3 (backend) are complete. Remaining work:

1. **Frontend scan results panel** (Phase 3.7) — UI for triggering scans and viewing results.
2. **Phase 4** — Active manipulation (rules engine, scripted breakpoints, replay, fuzzer).
3. **Phase 5** — AI mock engine + advanced protocol decoders.

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

- The `.claude/` directory in the repo root stores tool permissions and session memory for this project.
- Project memory is maintained at `~/.claude/projects/C--Users-annag-source-repos-IoTSpy/memory/MEMORY.md`.
- The `IoTSpy.Protocols`, `IoTSpy.Scanner`, and `IoTSpy.Manipulation` projects are empty stubs — no source files yet, just `.csproj` files with placeholder dependencies.
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` (`InitialCreate` applied). Run `dotnet ef migrations add <Name>` from repo root when adding new entities/properties.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- No tests exist yet. Recommended: unit tests for `CertificateAuthority` + integration tests for proxy intercept logic.
