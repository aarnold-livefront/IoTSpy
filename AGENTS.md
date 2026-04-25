# AGENTS.md

AI agent customization for IoTSpy. For full technical reference, see [AGENT.md](AGENT.md); for quick commands, see [CLAUDE.md](CLAUDE.md).

## Overview

IoTSpy is a .NET 10 IoT security MITM proxy + pen-test suite with ASP.NET Core backend and React 19 frontend. **644 backend tests** across 8 test projects; **13+ frontend component tests**. All phases 1–22 complete; production-ready with Docker, Kubernetes, and GitHub Actions CI.

## Agent patterns & conventions

### Backend structure

Keep `IoTSpy.Core` **free of infrastructure dependencies**. Feature addition flow:

1. **Model/interface** → `IoTSpy.Core` (domain only)
2. **EF entity + repository** → `IoTSpy.Storage` (+ migration)
3. **Decode/analyze logic** → `IoTSpy.Protocols` or `IoTSpy.Scanner` or `IoTSpy.Manipulation`
4. **Proxy/networking** → `IoTSpy.Proxy` (TCP, TLS, buffers)
5. **REST endpoint** → `IoTSpy.Api/Controllers/`
6. **Tests** → matching `*.Tests` project (mirror structure)

### Service registration patterns

- **Singletons**: all proxy servers, publishers, auth, resilience, anomaly detector
- **Scoped**: EF Core repositories (`AddIoTSpyStorage()` handles this)
- **Dual-register pattern** for hosted services that are also singletons:
  ```csharp
  builder.Services.AddSingleton<ProxyService>();
  builder.Services.AddHostedService(sp => (ProxyService)sp.GetRequiredService<IProxyService>());
  ```
  Never double-register with `.AddSingleton()` + `.AddHostedService()`—use the cast pattern above.

### Testing conventions

- **NSubstitute** for mocks (not Moq)
- **EF Core in-memory** for repository tests (SQLite provider; avoids real DB)
- **xUnit 2.9.3** for test framework
- Run `dotnet test` before committing; all 644 tests must pass
- Prefix test class names with the class being tested: `ProxyServiceTests`, `CertificateAuthorityTests`

### Configuration & secrets

- `Auth:JwtSecret` ≥ 32 chars (HS256 requirement; throws at startup if missing)
- JWT issuer/audience both hardcoded to `"iotspy"`
- SignalR JWT extraction: `?access_token=<token>` query param
- EF Core: switch DB provider via `Database:Provider` (sqlite|postgres)
- All configs read via `builder.Configuration["Section:Key"]` with null checks

### Real-time streaming patterns

- **SignalR hubs** at `/hubs/traffic` and `/hubs/packets`
- **JSON enum serialization** must be configured on *both*:
  ```csharp
  builder.Services.AddControllers().AddJsonOptions(opts =>
      opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
  builder.Services.AddSignalR().AddJsonProtocol(opts =>
      opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
  ```
  Missing the SignalR call = numeric enum values in live-streamed data = frontend crashes.

### Pagination & responses

All list endpoints return envelope:
```json
{ "items": [...], "total": 42, "page": 1, "pageSize": 20, "pages": 3 }
```

### EF Core migrations

```bash
dotnet ef migrations add <MigrationName> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api
dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api
```

Use `DesignTimeDbContextFactory` when running `dotnet ef` from CLI.

### Frontend stack

- **Vite 6** (dev server, build)
- **React 19** with TypeScript
- **Vitest** for unit tests + React Testing Library for component tests
- **API client** in `frontend/src/api/` (fetch-based, wraps REST + SignalR)
- Run tests: `cd frontend && npm test`
- Dev server: `npm run dev` (watches on `http://localhost:5173`)

### Naming conventions

| Item | Convention |
|---|---|
| Namespace prefix | `IoTSpy` (capital I, lowercase o, capital TS) |
| Docker image/container | `iotspy` (all lowercase) |
| Test class | `<ClassName>Tests` in matching `IoTSpy.*.Tests` project |
| Async methods | suffix with `Async` |
| Enum naming | `UserRole` not `UserRoles`; use `JsonConverter` for enum serialization |

## Quick workflows

### Add a new controller endpoint

1. Define models in `IoTSpy.Core/Models/`
2. Define interface in `IoTSpy.Core/Interfaces/`
3. Implement repository in `IoTSpy.Storage/Repositories/`
4. Implement controller in `IoTSpy.Api/Controllers/`
5. Add unit tests in `IoTSpy.Api.Tests/Controllers/`
6. Run `dotnet test` to verify all 644 tests pass

### Add a new protocol decoder

1. Define `IProtocolDecoder` in `IoTSpy.Core/Interfaces/`
2. Implement decoder in `IoTSpy.Protocols/Decoders/`
3. Register in `IoTSpy.Api/Program.cs` (if needed)
4. Add tests in `IoTSpy.Protocols.Tests/`

### Add a rule or manipulation feature

1. Define models + interfaces in `IoTSpy.Core/`
2. Implement rules engine logic in `IoTSpy.Manipulation/`
3. Add EF entity + migration in `IoTSpy.Storage/`
4. Expose via REST endpoint in `IoTSpy.Api/Controllers/ManipulationController.cs`
5. Test with `IoTSpy.Manipulation.Tests/`

### Fix a Linux packet capture issue (SharpPcap)

SharpPcap requires `CAP_NET_RAW` and `CAP_NET_ADMIN` capabilities. Grant to the *real* dotnet binary (not symlinks):

```bash
sudo setcap cap_net_raw,cap_net_admin+eip "$(readlink -f $(which dotnet))"
```

Restart the API after running `setcap`. See [AGENT.md](AGENT.md#packet-capture) for full details.

## Before committing

- [ ] All 644 backend tests pass: `dotnet test`
- [ ] Frontend tests pass: `cd frontend && npm test`
- [ ] New backend code has unit tests
- [ ] No changes to `IoTSpy.Core` that add infrastructure dependencies
- [ ] New EF entities have a migration (run `dotnet ef migrations add ...`)
- [ ] SignalR changes include JSON enum converter on both controllers and SignalR hub

## Documentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — full architecture spec, service lifetimes, dependency graph
- [PLAN.md](docs/PLAN.md) — phased feature roadmap and completed phases
- [CODE-PATTERNS.md](docs/CODE-PATTERNS.md) — async/await, error handling, logging patterns
- [DESIGN-DECISIONS.md](docs/DESIGN-DECISIONS.md) — why certain technologies were chosen
- [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) — common setup issues and solutions
- [QUICK-REF.md](docs/QUICK-REF.md) — command quick reference and filesystem locations

## Custom Claude Code skills

Project-specific skills are in `.dev/claude-skills/`:

| Skill | Use when |
|---|---|
| `dotnet-engineer` | ASP.NET Core, EF Core, SignalR architecture guidance |
| `security-code-review` | OWASP Top 10, auth/injection vulnerability review before PR merge |
| `threat-modeling` | Structured threat analysis for features or design changes |

To install: see `.dev/claude-skills/README.md`.
