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
| 1.7 | `IoTSpy.Api` — JWT auth (single-user, BCrypt), setup endpoint | ✅ Done |
| 1.8 | `IoTSpy.Api` — ProxyController, CapturesController, DevicesController, CertificatesController | ✅ Done |
| 1.9 | `IoTSpy.Api` — SignalR TrafficHub + SignalRCapturePublisher | ✅ Done |
| 1.10 | `IoTSpy.Api` — CORS, OpenAPI (Scalar), DB auto-migration on startup | ✅ Done |
| 1.11 | Frontend scaffold (Vinext) | Not started |
| 1.12 | Frontend — split-pane capture list + detail view | Not started |
| 1.13 | Frontend — proxy start/stop controls + settings form | Not started |
| 1.14 | Frontend — CA download button | Not started |
| 1.15 | Frontend — SignalR live stream connection | Not started |
| 1.16 | Docker / docker-compose for single-command local run | Not started |
| 1.17 | `.gitignore` updates, README, docs | ✅ Done (this session) |

**Phase 1 backend is complete.** Remaining work is the Vinext frontend and Docker packaging.

---

### Phase 2 — Additional interception modes + protocols

| # | Task | Status |
|---|---|---|
| 2.1 | `IoTSpy.Protocols` — MQTT 3.1.1 / 5.0 decoder | Planned |
| 2.2 | `IoTSpy.Protocols` — DNS / mDNS decoder | Planned |
| 2.3 | `IoTSpy.Proxy` — GatewayRedirect mode (iptables REDIRECT) | Planned |
| 2.4 | `IoTSpy.Proxy` — ArpSpoof mode (SharpPcap) | Planned |
| 2.5 | `IoTSpy.Api` — Real-time stream improvements (filter subscriptions) | Planned |
| 2.6 | Frontend — timeline swimlane view per device | Planned |

---

### Phase 3 — Pen-test suite

| # | Task | Status |
|---|---|---|
| 3.1 | `IoTSpy.Scanner` — TCP port scan | Planned |
| 3.2 | `IoTSpy.Scanner` — Service fingerprinting (banner grab + CPE) | Planned |
| 3.3 | `IoTSpy.Scanner` — Default credential testing | Planned |
| 3.4 | `IoTSpy.Scanner` — CVE lookup (NVD / OSV APIs) | Planned |
| 3.5 | `IoTSpy.Scanner` — Config audit (Telnet, UPnP, anon MQTT, etc.) | Planned |
| 3.6 | `IoTSpy.Api` — ScannerController | Planned |
| 3.7 | Frontend — scan results panel | Planned |

---

### Phase 4 — Active manipulation

| # | Task | Status |
|---|---|---|
| 4.1 | `IoTSpy.Manipulation` — Declarative rules engine (match → modify) | Planned |
| 4.2 | `IoTSpy.Manipulation` — C# Roslyn scripted breakpoints | Planned |
| 4.3 | `IoTSpy.Manipulation` — JavaScript (Jint) scripted breakpoints | Planned |
| 4.4 | `IoTSpy.Manipulation` — Request replay with diff view | Planned |
| 4.5 | `IoTSpy.Manipulation` — Mutation-based fuzzer | Planned |
| 4.6 | `IoTSpy.Api` — ManipulationController | Planned |
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

### What is done (Phase 1 backend)

The entire backend is scaffolded and functional:

- `ExplicitProxyServer` listens on `:8888`, handles HTTP and HTTPS CONNECT.
- TLS MITM works via BouncyCastle dynamic CA (root CA auto-generated, per-host leaf certs cached).
- Polly 8 resilience: per-host circuit breaker + retry + timeout for TCP connect; timeout-only for TLS handshake.
- EF Core 10 with SQLite default; Postgres switchable via config.
- JWT single-user auth with BCrypt password hash.
- SignalR hub `/hubs/traffic` streams every captured request in real time.
- Five REST controllers fully implemented.
- OpenAPI (Scalar) in Development mode at `/scalar`.

### What to do next

The highest-value next task is the **Vinext frontend (Phase 1.11–1.15)**:

1. Scaffold the frontend: `npx skills add cloudflare/vinext` (from project root or `frontend/`)
2. Connect to `/api/auth/login` and store the JWT.
3. Connect to `/hubs/traffic` via SignalR for real-time traffic.
4. Build split-pane view: left = capture list, right = request/response detail.
5. Add proxy start/stop button calling `POST /api/proxy/start` / `POST /api/proxy/stop`.
6. Add CA download link: `GET /api/certificates/root-ca/download`.

After the frontend, add **Docker packaging (Phase 1.16)**:

```dockerfile
# Dockerfile target: src/IoTSpy.Api
# Multi-stage: build with dotnet:10-sdk, run with dotnet:10-aspnet
```

Then proceed to Phase 2 (ARP spoof / gateway mode + MQTT decoder).

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
| Frontend | Vinext (Cloudflare) | Specified in project requirements |
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

- The `.claude/settings.local.json` in the repo root stores tool permissions for this project.
- Project memory is maintained at `~/.claude/projects/-Users-annalise-git-aarnold-livefront-iotspy/memory/MEMORY.md`.
- The `IoTSpy.Protocols`, `IoTSpy.Scanner`, and `IoTSpy.Manipulation` projects are empty stubs — no source files yet, just `.csproj` files with placeholder dependencies.
- EF Core migrations have **not** been generated yet (the DB is created via `EnsureCreated` or a pending migration — check `StorageExtensions` before adding migrations).
- No tests exist yet. Phase 1 completion should include at minimum unit tests for `CertificateAuthority` and integration tests for the proxy intercept logic.
