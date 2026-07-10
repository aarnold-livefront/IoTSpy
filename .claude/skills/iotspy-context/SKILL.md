---
name: iotspy-context
description: IoTSpy project context — architecture, conventions, and security caveats specific to this codebase. Use when working in the IoTSpy repo, especially alongside dotnet-engineer, security-code-review, or threat-modeling. Pointers into in-repo docs rather than duplicated facts that rot.
---

# IoTSpy Context

This skill holds only the IoTSpy-specific bits that matter when reasoning about the codebase. Generic .NET, security-review, and threat-modeling guidance live in their own skills. **Don't duplicate facts here that already live in CLAUDE.md or AGENT.md** — point to them and let them be the source of truth.

## Where to look first

- **`CLAUDE.md`** — quick commands, project structure, current state (test counts, migration count, completed phases). This is the canonical place for "how big is the project right now."
- **`AGENT.md`** — full technical reference: commands, architecture, naming conventions, testing guidance, known operational requirements (setcap, JSON enum serialization, iOS cert constraints, SQLite migration foot-guns).
- **`docs/ARCHITECTURE.md`** — detailed architecture spec.
- **`docs/CODE-PATTERNS.md`** — implementation patterns to follow when adding controllers, repositories, hubs, decoders.
- **`docs/PHASES-COMPLETED.md`** / **`docs/PHASES-ROADMAP.md`** — phase history and roadmap.
- **`docs/TROUBLESHOOTING.md`** — known issues and resolutions.

When asked "what's the current state" / "how many tests" / "which migration is latest" / "what phase are we on", **read `CLAUDE.md` rather than answering from memory**. State drifts.

## Architectural shape (stable enough to state)

The project layout is stable, even when counts change:

- **`IoTSpy.Api`** — ASP.NET Core host, REST controllers, SignalR hubs, middleware
- **`IoTSpy.Core`** — domain models, interfaces, enums; **must not** take infrastructure dependencies
- **`IoTSpy.Storage`** — EF Core DbContext + repositories (SQLite/Postgres)
- **`IoTSpy.Proxy`** — TLS MITM/passthrough, SSL stripping, WebSocket/MQTT/CoAP proxies
- **`IoTSpy.Protocols`** — MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB, telemetry decoders
- **`IoTSpy.Scanner`** — port scan, fingerprinting, CVE lookup, packet capture
- **`IoTSpy.Manipulation`** — rules engine, scripted breakpoints, replay, fuzzer, AI mock, OpenRTB PII, API spec generation, content replacement
- **`IoTSpy.*.Tests`** — unit + integration tests
- **`frontend/`** — Vite 6 + React 19 + TypeScript dashboard

Namespace prefix is `IoTSpy` (capital I, o, T, S). Docker image/container name is `iotspy`.

## Conventions that matter

- **Run `dotnet test` before committing.** All backend tests must stay green.
- **New backend logic needs a test.** Use NSubstitute for mocks; EF Core SQLite in-memory for repository tests.
- **New EF entities need a migration** (`dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api`).
- **Hosted + singleton services** use `AddHostedService(sp => sp.GetRequiredService<T>())` — never double-register.
- **JSON enum serialization** must be configured on **both** `AddControllers().AddJsonOptions(...)` *and* `AddSignalR().AddJsonProtocol(...)`. Missing the SignalR call sends numeric enum values over live captures and crashes the frontend timeline.
- **Polly resilience** uses two named pipelines: `iotspy-connect` (timeout → retry → circuit breaker, keyed per upstream host) and `iotspy-tls` (timeout only).

## Security caveats specific to IoTSpy

When reviewing code or threat-modeling for this codebase, these are the IoTSpy-specific surfaces a generic skill won't catch:

1. **Dual-use research tool.** IoTSpy *intentionally* MITMs TLS, strips SSL, scans ports, captures packets, and rewrites traffic. Things that would be malicious in another context are core features here. Do not flag these as bugs — call them out as design decisions when the surrounding context shifts (e.g., if the proxy accepts connections from outside a research lab, the threat model changes).

2. **Single-user JWT, BCrypt password in `ProxySettings` row (Id=1).** The whole product authenticates against one row. Anything weakening that row's hash, leaking it, or bypassing the `Auth__JwtSecret` ≥ 32-char guard is critical. Verify the secret-length enforcement is reachable on every entry point (controllers, SignalR, design-time DbContext factory).

3. **SignalR token via `?access_token=` query param.** This means tokens can land in proxy/access logs. Reviewing logging or middleware that sees URLs needs to consider redaction.

4. **Roslyn (C#) and Jint (JS) scripted breakpoints.** Anywhere user-supplied scripts execute is RCE-by-design. Confirm it stays gated behind admin auth, that scripts cannot escape into the host process beyond the documented surface, and that imports/timeouts are bounded. Flag any change that broadens the script API.

5. **SharpPcap `CAP_NET_RAW` / `CAP_NET_ADMIN`.** The API process gets raw socket access via `setcap`. Any new code path that handles arbitrary sockets or executes privileged operations should be audited against this elevated capability.

6. **TLS validation intentionally disabled upstream in the proxy.** Don't spread that pattern outside the proxy. Other components (telemetry, AI mock client, scanner) must validate TLS normally.

7. **Captured-data confidentiality.** The product's value is intercepted traffic — credentials, tokens, payloads. Storage encryption-at-rest, retention, and access control on captures, replays, and exports are core, not nice-to-have.

8. **Migration foot-gun on SQLite.** `AlterColumn` emits `PRAGMA foreign_keys = 0` which can't run inside the EF transaction wrapper. Use `migrationBuilder.Sql(...)` directly. See `AGENT.md` § "EF Core migrations on SQLite" for the worked example.

9. **iOS/macOS leaf cert constraints.** ≤397-day validity, AKI keyid-only, IP-as-`GeneralName.IPAddress`. If you regenerate the root CA, delete leaf certs from the database. See `AGENT.md` for the SQL.

## When to use this skill alongside others

- With **`dotnet-engineer`**: when designing a new controller, hub, repository, or background service in this repo. Apply IoTSpy conventions on top of generic .NET advice.
- With **`security-code-review`**: when reviewing IoTSpy code, layer the IoTSpy-specific risks above onto the generic 7-step checklist.
- With **`threat-modeling`**: when modeling new IoTSpy features, anchor "dual-use tool" considerations from this skill into the model.

When in doubt about a project-specific fact, **read `CLAUDE.md` and `AGENT.md`**. Don't paraphrase from memory.
