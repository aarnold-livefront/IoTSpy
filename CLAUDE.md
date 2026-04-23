# CLAUDE.md

Guidance for Claude Code when working in this repository. See [AGENT.md](AGENT.md) for the full technical reference (commands, architecture, naming conventions, testing guidance).

## Quick commands

```bash
# Build
dotnet build

# Test (all / single project)
dotnet test
dotnet test src/IoTSpy.SomeTests/IoTSpy.SomeTests.csproj

# Run the API (Auth__JwtSecret required, ≥ 32 chars)
Auth__JwtSecret="replace-with-32-char-minimum-secret" dotnet run --project src/IoTSpy.Api

# Add / apply EF Core migration (run from repo root)
dotnet ef migrations add <MigrationName> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api
dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api

# Frontend
cd frontend && npm install && npm run dev
cd frontend && npm test && npm run build
```

Scalar API docs: `http://localhost:5000/scalar` (Development only).

## Project structure at a glance

```
IoTSpy.Api          ASP.NET Core host — controllers, SignalR hubs, middleware
IoTSpy.Core         Domain models, interfaces, enums (no infrastructure deps)
IoTSpy.Proxy        TLS MITM/passthrough, SSL stripping, WebSocket/MQTT/CoAP proxies
IoTSpy.Storage      EF Core DbContext + repositories (SQLite/Postgres)
IoTSpy.Protocols    MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB, telemetry decoders
IoTSpy.Scanner      Port scan, fingerprinting, CVE lookup, packet capture
IoTSpy.Manipulation Rules engine, scripted breakpoints, replay, fuzzer, AI mock, OpenRTB PII, API spec generation, content replacement
IoTSpy.*.Tests      Unit + integration tests
frontend/           Vite 6 + React 19 + TypeScript dashboard
docs/               ARCHITECTURE.md, PLAN.md
```

## Available skills

Project-specific Claude Code skills live in `.dev/claude-skills/`. Install them once from the repo root:

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

## Workflow rules

- **Always run `dotnet test` before committing.** All 300+ backend tests must stay green.
- New backend logic needs a corresponding test. Use NSubstitute for mocks; use EF Core SQLite in-memory for repository tests.
- New EF entities require a migration (`dotnet ef migrations add ...`).
- Keep `IoTSpy.Core` free of infrastructure dependencies.
- Use the `AddHostedService(sp => sp.GetRequiredService<T>())` pattern for services that are both singleton and hosted — never double-register.
- Namespace prefix is `IoTSpy` (capital I, o, T, S). Docker image/container name is `iotspy`.

## Current state

All phases 1–16, 18–22 plus OpenRTB inspection, TLS passthrough/SSL stripping, API Spec Generation & Content-Aware Mocking, collaboration, passive proxy mode, and rich-media/SSE content replacement are complete:
- 608+ backend tests across 8 test projects; 13+ frontend component tests
- 18 REST controllers, 120+ endpoints (added `PassiveCaptureController`; Phase 22 added rule preview + public asset-content routes to `ApiSpecController`)
- 17 EF Core migrations up through `AddPhase22SseReplayConfig`
- GitHub Actions CI at `.github/workflows/ci.yml`
- Helm chart at `deploy/helm/iotspy/`; production Docker Compose at `docker-compose.prod.yml`

### Operational notes

**Linux packet capture (SharpPcap):** grant `CAP_NET_RAW`/`CAP_NET_ADMIN` to the *real* dotnet binary — `setcap` rejects symlinks:
```bash
sudo setcap cap_net_raw,cap_net_admin+eip "$(readlink -f $(which dotnet))"
```
Restart the API after running `setcap`. See AGENT.md for full details.

**JSON enum serialization:** `Program.cs` must configure `JsonStringEnumConverter` on *both* `AddControllers().AddJsonOptions(...)` and `AddSignalR().AddJsonProtocol(...)`. Missing the SignalR call causes numeric enum values in live-streamed captures, which crashes the frontend timeline.

See `docs/PLAN.md` for the full phased task list, per-phase details, and roadmap.
See `docs/ARCHITECTURE.md` for the full architecture spec.
