# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the API (Auth__JwtSecret is required)
Auth__JwtSecret="replace-with-32-char-minimum-secret" dotnet run --project src/IoTSpy.Api

# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test src/IoTSpy.SomeTests/IoTSpy.SomeTests.csproj

# Restore dependencies
dotnet restore

# Add EF Core migration (run from repo root; DesignTimeDbContextFactory handles SQLite)
dotnet ef migrations add <MigrationName> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api

# Apply migrations manually
dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api
```

Scalar API docs are served at `http://localhost:5000/scalar` in Development mode only.

## Architecture

### Project dependency graph

```
IoTSpy.Api (ASP.NET Core host)
  ├── IoTSpy.Core          (domain models, interfaces, enums — no infrastructure deps)
  ├── IoTSpy.Proxy         (TCP listener, TLS MITM, TLS passthrough + JA3, SSL stripping, Polly resilience)
  ├── IoTSpy.Storage       (EF Core DbContext + repositories; SQLite/Postgres)
  ├── IoTSpy.Protocols     (MQTT 3.1.1/5.0, DNS/mDNS, CoAP, OpenRTB, telemetry decoders, anomaly detection)
  ├── IoTSpy.Scanner       (TCP port scan, service fingerprinting, credential testing, CVE lookup, config audit, packet capture)
  └── IoTSpy.Manipulation  (rules engine, scripted breakpoints, replay, fuzzer, AI mock, OpenRTB PII, packet analysis)
```

`Protocols` has MQTT, DNS, CoAP, WebSocket, gRPC/Protobuf, Modbus TCP, OpenRTB, and telemetry decoders. `Scanner` has the pen-test suite and packet capture. `Manipulation` has the rules engine, C#/JS scripted breakpoints, request replay, mutation fuzzer, AI mock engine, OpenRTB PII service, and packet capture analyzer. `Proxy` has HTTP/HTTPS interception, TLS passthrough with metadata capture (SNI, JA3/JA3S, server cert extraction), SSL stripping, MQTT broker proxy, and CoAP UDP proxy.

### Data flow

```
IoT Device → ExplicitProxy :8888      (explicit mode — device configured to use proxy)
           → TransparentProxy :9999   (gateway/ARP mode — iptables REDIRECT)
           → MqttBrokerProxy :1883    (MQTT MITM — decodes packets, topic-level filtering)
           → CoapProxy :5683 (UDP)    (CoAP forward proxy — decodes messages, captures)
  └─ ExplicitProxyServer / TransparentProxyServer
       ├─ Plain HTTP → InterceptHttpStreamAsync
       ├─ WebSocket Upgrade (101) → RelayWebSocketFramesAsync (bidirectional frame capture)
       ├─ gRPC (application/grpc) → capture with Protocol=Grpc
       ├─ Plain HTTP + SslStrip=true → SslStripService (intercept HTTPS redirects, fetch upstream TLS, serve HTTP)
       └─ TLS (CONNECT or transparent)
            ├─ CaptureTls=false → HandleTlsPassthroughAsync
            │    └─ Parse ClientHello (SNI, JA3) → relay → parse ServerHello (JA3S) + Certificate (TLS 1.2)
            │         → record CapturedRequest with Protocol=TlsPassthrough + TlsMetadataJson
            └─ CaptureTls=true  → CertificateAuthority.GetOrCreateHostCertAsync()
                                   SslStream MITM → InterceptHttpStreamAsync
                                        ├─ CaptureRepository (SQLite/PG)
                                        └─ ICapturePublisher → SignalR (group-routed) → dashboard
```

### Key service lifetimes (Program.cs)

- `ExplicitProxyServer`, `TransparentProxyServer`, `CertificateAuthority`, `ProxyService`, `SslStripService`, `ArpSpoofEngine`, `IptablesHelper`, `SignalRCapturePublisher`, `PortScanner`, `ServiceFingerprinter`, `CredentialTester`, `ConfigAuditor`, `ScannerService`, `MqttBrokerProxy`, `CoapProxy` — **Singleton** (TCP/UDP listeners / long-lived services).
- `ProxyService` is registered both as `IProxyService` (singleton) and as `IHostedService` via `AddHostedService(sp => ...)` to avoid double instantiation.
- Repositories are **Scoped** (EF Core DbContext).

### TLS MITM

Root CA is generated lazily on first use, stored as PEM in `CertificateEntries` (SQLite), and cached in memory. Per-host leaf certs are cached in the DB and reused until one day before expiry. Upstream TLS is validated with certificate validation disabled (research tool).

### TLS Passthrough (no-CA-install mode)

When `CaptureTls=false`, traffic is not decrypted but metadata is extracted from the handshake:
- **ClientHello**: SNI hostname, cipher suites, extensions, JA3 fingerprint (`TlsClientHelloParser`)
- **ServerHello**: selected cipher/version, extensions, JA3S fingerprint (`TlsServerHelloParser`)
- **Certificate** (TLS 1.2 only — encrypted in TLS 1.3): subject, issuer, SAN list, serial, SHA-256, expiry
- Traffic byte counts (client→server, server→client) and connection duration
- Stored as `TlsMetadata` JSON in `CapturedRequest.TlsMetadataJson` with `Protocol=TlsPassthrough`
- All log entries include `DnsCorrelationKey={ClientIp}→{SniHostname}` for joining with DNS packet captures

### SSL Stripping

When `ProxySettings.SslStrip=true` on plain HTTP connections, `SslStripService` intercepts HTTP→HTTPS redirects (301/302/307/308), follows the HTTPS URL upstream, strips HSTS headers, rewrites `https://` links in Location/Set-Cookie/CSP headers and HTML/JSON bodies, and serves decrypted content over HTTP. Useful for IoT devices that cannot have custom CAs installed.

### Resilience (Polly 8)

Two named pipelines in `ProxyResiliencePipelines`:
- `iotspy-connect` — keyed by upstream hostname: `Timeout → Retry (exponential) → CircuitBreaker`
- `iotspy-tls` — `Timeout` only (TLS handshake is not safely retryable)

All resilience defaults are configurable under the `Resilience` section in `appsettings.json`.

### Authentication

Single-user model. BCrypt password hash stored in the single `ProxySettings` row (Id=1). JWT issuer and audience are both `"iotspy"`. SignalR accepts the token via `?access_token=` query param. `Auth:JwtSecret` must be ≥ 32 characters; the app throws on startup if absent.

### Storage

`StorageExtensions.AddIoTSpyStorage()` wires EF Core for SQLite (default) or Postgres based on `Database:Provider`. `MigrateAsync()` is called at startup and auto-applies pending migrations. Use `DesignTimeDbContextFactory` when running `dotnet ef` CLI commands.

## Naming conventions

- Namespace prefix: `IoTSpy` (capital I, o, T, S)
- Docker image/container: `iotspy` (lowercase)

## Current status

All phases (1–10) plus OpenRTB inspection and TLS passthrough/SSL stripping are complete. Seven test projects (Protocols.Tests, Manipulation.Tests, Scanner.Tests + Api.Tests, Proxy.Tests, Storage.Tests, Api.IntegrationTests) with 14+ test classes. Ten REST controllers (Auth, Proxy, Captures, Devices, Certificates, Scanner, Manipulation, PacketCapture, OpenRtb, ProtocolProxy) with 46+ endpoints. EF Core migrations: `InitialCreate` + `AddPhase2ProxySettings` + `AddPhase3Scanner` + `AddPhase4ManipulationFix` + `AddOpenRtbInspection` + `AddPacketCapture` + `AddTlsPassthroughAndSslStrip`. GitHub Actions CI at `.github/workflows/ci.yml`.

**Phase 3 additions:**
- `IoTSpy.Scanner` — `PortScanner` (TCP connect scan, configurable concurrency/port ranges), `ServiceFingerprinter` (banner grab, CPE extraction via regex), `CredentialTester` (FTP/Telnet/MQTT default credential checks), `CveLookupService` (OSV.dev API), `ConfigAuditor` (Telnet, UPnP, anon MQTT, exposed DB, HTTP admin detection)
- `IoTSpy.Scanner` — `ScannerService` orchestrator implementing `IScannerService` — runs scan pipeline in background, updates device `SecurityScore`
- `IoTSpy.Core` — `ScanJob`, `ScanFinding` models; `ScanStatus`, `ScanFindingSeverity`, `ScanFindingType` enums; `IScanJobRepository`, `IScannerService` interfaces
- `IoTSpy.Storage` — `ScanJobs` + `ScanFindings` DbSets, `ScanJobRepository`, `AddPhase3Scanner` migration
- `IoTSpy.Api` — `ScannerController` (POST scan, GET jobs/findings/status, cancel, delete)

**Phase 4 additions:**
- `IoTSpy.Manipulation` — `RulesEngine` (declarative match→modify rules, regex-based host/path/method matching, header/body modification, status override, delay, drop actions)
- `IoTSpy.Manipulation` — `CSharpScriptEngine` (Roslyn-based C# scripted breakpoints) + `JavaScriptEngine` (Jint-based JS scripted breakpoints)
- `IoTSpy.Manipulation` — `ReplayService` (replay captured requests with modifications, record response)
- `IoTSpy.Manipulation` — `FuzzerService` (mutation-based fuzzer: Random, Boundary, BitFlip strategies with anomaly detection)
- `IoTSpy.Manipulation` — `ManipulationService` orchestrator implementing `IManipulationService` — applies rules+scripts inline in the proxy pipeline
- `IoTSpy.Core` — `ManipulationRule`, `Breakpoint`, `ReplaySession`, `FuzzerJob`, `FuzzerResult`, `HttpMessage` models; `ManipulationRuleAction`, `ManipulationPhase`, `ScriptLanguage`, `FuzzerStrategy`, `FuzzerJobStatus` enums; `IManipulationRuleRepository`, `IBreakpointRepository`, `IReplaySessionRepository`, `IFuzzerJobRepository`, `IManipulationService` interfaces
- `IoTSpy.Storage` — `ManipulationRules`, `Breakpoints`, `ReplaySessions`, `FuzzerJobs`, `FuzzerResults` DbSets + repositories + `AddPhase4Manipulation` migration
- `IoTSpy.Api` — `ManipulationController` (CRUD rules/breakpoints, replay, fuzzer start/cancel/status/results)
- `IoTSpy.Proxy` — Both `ExplicitProxyServer` and `TransparentProxyServer` now call `IManipulationService.ApplyAsync()` for request and response phases, setting `IsModified` on captured requests

**Phase 5 additions (complete):**
- `IoTSpy.Manipulation` — `AiMockService` (schema learning + LLM response generation), `AiProviderFactory`, `IAiProvider` interface
- `IoTSpy.Manipulation` — `ClaudeProvider`, `OpenAiProvider`, `OllamaProvider` (pluggable AI backends)
- `IoTSpy.Core` — `AiMockResponse`, `AiProviderConfig`, `AnomalyAlert`, `IAiMockService` models/interfaces
- `IoTSpy.Protocols` — CoAP message decoder (`CoapMessage`, `CoapCode`, `CoapMessageType`, `CoapOptionNumber`)
- `IoTSpy.Api` — AI mock endpoints in `ManipulationController` (generate, invalidate cache)
- `IoTSpy.Protocols` — Telemetry decoders: `DatadogDecoder`, `FirehoseDecoder`, `SplunkHecDecoder`, `AzureMonitorDecoder`; all implement `IProtocolDecoder<TelemetryMessage>`
- `IoTSpy.Protocols` — Anomaly detection: `AnomalyDetector` (Welford online algorithm, per-host baseline for duration/size/status/rate); `IAnomalyDetector` interface in Core; `HostBaseline` model in Core
- Tests: `TelemetryDecoderTests`, `AnomalyDetectorTests` added to `IoTSpy.Protocols.Tests`

**Frontend additions (Phases 3-4):**
- Scanner panel: `ScannerPanel`, `ScanJobList`, `ScanFindingsView` components
- Manipulation UI: `RulesEditor`, `BreakpointsEditor`, `ReplayPanel`, `FuzzerPanel`, `ManipulationPanel`
- API clients: `manipulation.ts`, `scanner.ts`
- React hooks: `useManipulation`, `useScanner`
- TypeScript types in `api.ts` aligned with backend C# models

**Phase 6 additions (complete):**
- `IoTSpy.Scanner` — `PacketCaptureService` (SharpPcap live capture, LinkedList ring buffer 10k cap, protocol parsing, PCAP export, hex dump)
- `IoTSpy.Manipulation` — `PacketCaptureAnalyzer` (protocol distribution, communication patterns, suspicious activity: port scan, ARP spoof, DNS anomaly, retransmission bursts)
- `IoTSpy.Core` — `CaptureDevice`, `CapturedPacket`, `PacketFilter`, `PacketFilterDto`, `FreezeFrameResult`, `ProtocolDistribution`, `CommunicationPattern`, `SuspiciousActivity`, `NetworkDeviceStatistics` models; `IPacketCaptureService`, `IPacketCaptureAnalyzer`, `IPacketCapturePublisher` interfaces
- `IoTSpy.Storage` — `CaptureDevices` + `Packets` DbSets, `AddPacketCapture` migration
- `IoTSpy.Api` — `PacketCaptureController` (14 endpoints), `PacketCaptureHub` (SignalR at `/hubs/packets`), `SignalRPacketPublisher`
- Frontend: `PanelPacketCapture` (tabbed: Packets/Protocols/Patterns/Suspicious), `PacketInspector` (Details/Hex/Layers), `ProtocolDistributionView`, `PatternExplorer`, `SuspiciousActivityPanel`
- Frontend: `packetCapture.ts` API client, `usePacketCapture` + `usePacketAnalysis` hooks, TypeScript DTOs

**OpenRTB additions (post-Phase 6):**
- `IoTSpy.Protocols` — `OpenRtbDecoder` (OpenRTB 2.5 bid request/response parsing)
- `IoTSpy.Manipulation` — `OpenRtbPiiService` (PII detection + redaction strategies)
- `IoTSpy.Core` — `OpenRtbEvent`, `OpenRtbPiiPolicy` models; `IOpenRtbEventRepository`, `IOpenRtbPiiPolicyRepository`, `IOpenRtbService` interfaces
- `IoTSpy.Storage` — `OpenRtbEvents`, `OpenRtbPiiPolicies` DbSets + repositories + `AddOpenRtbInspection` migration
- `IoTSpy.Api` — `OpenRtbController` (events CRUD, PII policies CRUD, PII audit logs)
- `IoTSpy.Proxy` — Inline OpenRTB detection in both proxy servers
- Frontend: `OpenRtbPanel`, `OpenRtbTrafficList`, `OpenRtbInspector`, `PiiPolicyEditor`, `PiiAuditLog`
- Tests: `OpenRtbDecoderTests`, `OpenRtbPiiServiceTests`

**Phase 7 additions (complete):**
- `IoTSpy.Api.Tests` — controller unit tests for Auth, Proxy, Captures, Devices, Scanner using NSubstitute mocks; `AuthService` unit tests
- `IoTSpy.Proxy.Tests` — `ProxyService` state tests; `ResilienceOptions` defaults tests
- `IoTSpy.Storage.Tests` — repository integration tests using EF Core SQLite in-memory: `DeviceRepository`, `CaptureRepository`, `ProxySettingsRepository`
- `IoTSpy.Api.IntegrationTests` — `WebApplicationFactory`-based integration tests for auth and devices endpoints; `IoTSpyWebApplicationFactory` replaces heavy infrastructure with NSubstitute fakes
- Frontend: Vitest + React Testing Library setup; `vitest.config.ts`; 11 component tests across `ErrorBanner`, `LoadingSpinner`, `HeadersViewer`
- `.github/workflows/ci.yml` — GitHub Actions CI: backend build + test + ReportGenerator coverage, frontend lint + Vitest + build on push/PR
- `src/Directory.Build.props` — shared Coverlet coverage config for all test projects
- `src/IoTSpy.Api/Program.cs` — `public partial class Program` for `WebApplicationFactory` compatibility

**Phase 8 additions (complete):**
- `IoTSpy.Api` — health check endpoints (`/health` liveness + DB probe, `/ready` readiness) with JSON response writer
- `IoTSpy.Api` — Serilog structured logging: console + rolling file sinks (7-day retention), `UseSerilogRequestLogging()` request middleware, configurable via `Serilog` section
- `IoTSpy.Api` — ASP.NET Core `RateLimiter` middleware: sliding-window policy partitioned per JWT sub / IP; default 100 permits / 60 s; toggle via `RateLimit:Enabled`
- `IoTSpy.Api` — `DataRetentionService` (`IHostedService`) — background cleanup with configurable TTLs for captures (30d), packets (7d), scan jobs (90d), OpenRTB events (14d); **disabled by default** (`DataRetention:Enabled: false`)
- `IoTSpy.Core` — `IAnomalyAlertPublisher` interface; `IoTSpy.Api` — `SignalRAnomalyPublisher` implementation
- `IoTSpy.Proxy` — `AnomalyDetector` wired into both proxy servers: `IAnomalyDetector.Record()` called after each captured request; alerts published via `SignalRAnomalyPublisher` to SignalR group `"anomaly-alerts"`; `TrafficHub` gains `SubscribeToAnomalyAlerts()` / `UnsubscribeFromAnomalyAlerts()` methods
- `IoTSpy.Proxy` — graceful shutdown: active connection count tracked with `Interlocked`; `StopAsync` drains connections up to a configurable timeout (default 10 s)
- `IoTSpy.Storage` — DB connection pooling: `StorageExtensions.AddIoTSpyStorage()` accepts `maxPoolSize` / `minPoolSize`; Postgres strings auto-augmented
- Tests: `HealthCheckEndpointTests` (5 integration tests), `DataRetentionServiceTests` (4 unit tests), `GracefulShutdownTests` (4 unit tests)
- Total: **248 backend tests** across 7 test projects + **11 frontend component tests** (all green)

**Phase 10 additions (complete):**
- `IoTSpy.Protocols` — `WebSocketDecoder` (RFC 6455 frame decoder: FIN, opcode, masking, extended lengths, close codes) + `WebSocketDecodedFrame` model
- `IoTSpy.Protocols` — `GrpcDecoder` (gRPC Length-Prefixed Message framing, schema-less protobuf field extraction: varint, fixed32/64, length-delimited) + `GrpcMessage`, `ProtobufField` models
- `IoTSpy.Protocols` — `ModbusDecoder` (Modbus TCP MBAP header parsing, function codes 1-16 + exception responses, register/coil value decoding) + `ModbusMessage` model
- `IoTSpy.Protocols` — `CoapDecoder` (RFC 7252 CoAP message decoder: header, tokens, delta-encoded options, payload) — complements existing `CoapMessage` model
- `IoTSpy.Core` — `WebSocketFrame`, `MqttCapturedMessage`, `MqttBrokerSettings`, `CoapProxySettings` models; `WebSocketOpcode` enum; `IMqttBrokerProxy`, `ICoapProxy` interfaces; `InterceptionProtocol` extended with `WebSocket`, `WebSocketTls`, `Grpc`, `Modbus`
- `IoTSpy.Core` — `ICapturePublisher` extended with `PublishWebSocketFrameAsync()`, `PublishMqttMessageAsync()`; `ICaptureRepository` extended with `UpdateAsync()`
- `IoTSpy.Proxy` — `MqttBrokerProxy` (TCP MQTT MITM: bidirectional relay, packet decoding, topic-level wildcard filtering `+`/`#`, real-time message publishing via SignalR)
- `IoTSpy.Proxy` — `CoapProxy` (UDP forward proxy: CoAP message decoding, upstream relay with timeout, device registration, capture recording)
- `IoTSpy.Proxy` — WebSocket interception in both `ExplicitProxyServer` and `TransparentProxyServer`: detects 101 Switching Protocols + `Upgrade: websocket`, switches to bidirectional frame relay with capture
- `IoTSpy.Proxy` — gRPC detection in both proxy servers: `application/grpc` content-type sets `InterceptionProtocol.Grpc` on captures
- `IoTSpy.Proxy` — now references `IoTSpy.Protocols` for MQTT/CoAP decoder access
- `IoTSpy.Api` — `ProtocolProxyController` (6 endpoints: MQTT start/stop/status, CoAP start/stop/status)
- `IoTSpy.Api` — `TrafficHub` extended with `SubscribeToWebSocketFrames()`, `SubscribeToMqttMessages()` SignalR subscriptions
- `IoTSpy.Api` — `SignalRCapturePublisher` implements new `PublishWebSocketFrameAsync()`, `PublishMqttMessageAsync()`
- Tests: `WebSocketDecoderTests` (14 tests), `GrpcDecoderTests` (8 tests), `ModbusDecoderTests` (9 tests), `CoapDecoderTests` (10 tests), `MqttTopicMatchTests` (15 tests)

**TLS passthrough & SSL stripping additions (complete):**
- `IoTSpy.Proxy` — `TlsClientHelloParser` (static parser: SNI extraction, cipher suites, extensions, JA3 fingerprint with GREASE filtering per RFC 8701)
- `IoTSpy.Proxy` — `TlsServerHelloParser` (ServerHello: selected cipher/version, JA3S fingerprint; Certificate: X.509 leaf cert subject/issuer/SAN/serial/SHA-256/expiry; TLS 1.3 detection skips encrypted Certificate)
- `IoTSpy.Proxy` — `SslStripService` (intercept HTTP→HTTPS redirects, follow upstream TLS, strip HSTS, rewrite `https://` links in headers and bodies)
- `IoTSpy.Proxy` — `HandleTlsPassthroughAsync` in both `ExplicitProxyServer` and `TransparentProxyServer`: buffers ClientHello, parses SNI/JA3, relays to upstream, parses ServerHello/JA3S + Certificate (TLS 1.2), counts bytes, records `CapturedRequest` with `Protocol=TlsPassthrough` and `TlsMetadataJson`
- `IoTSpy.Proxy` — SSL strip integration in `InterceptHttpStreamAsync` of both proxy servers: detects HTTPS redirect responses, fetches via TLS, strips HSTS from all responses when `SslStrip=true`
- `IoTSpy.Core` — `TlsMetadata` model (SNI, JA3/JA3S, client/server TLS versions, cipher suites, cert details, byte counts); `InterceptionProtocol.TlsPassthrough` enum value
- `IoTSpy.Core` — `CapturedRequest.TlsMetadataJson` field; `ProxySettings.SslStrip` field
- `IoTSpy.Storage` — `AddTlsPassthroughAndSslStrip` migration (adds `TlsMetadataJson` to Captures, `SslStrip` to ProxySettings)
- Structured logging with `DnsCorrelationKey={ClientIp}→{SniHostname}` on all TLS passthrough events for DNS-to-TLS correlation

All phases (1–10) plus OpenRTB and TLS passthrough/SSL stripping are complete. See `docs/PLAN.md` for the full plan, identified gaps, and forward-looking roadmap.

See `docs/architecture.md` for full architecture spec and `docs/PLAN.md` for the phased task list.
