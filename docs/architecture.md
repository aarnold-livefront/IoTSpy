# IoTSpy — Architecture

## Overview

IoTSpy is a .NET 10 / C# solution that acts as a transparent MITM proxy, multi-protocol decoder, statistical anomaly detector, and lightweight pen-test suite for IoT network research. A REST + SignalR API exposes all functionality; the frontend is a Vite 6 + React 19 + TypeScript single-page application.

All phases (1–10) plus OpenRTB inspection and TLS passthrough/SSL stripping are complete. Phase 11 (UX & multi-user) is next.

---

## Solution structure

```
IoTSpy.sln
src/
  IoTSpy.Core/           — domain models, interfaces, enums (no infrastructure deps)
  IoTSpy.Proxy/          — TCP listeners, TLS MITM, TLS passthrough + JA3, SSL stripping, Polly resilience
  IoTSpy.Protocols/      — protocol decoders + anomaly detection
  IoTSpy.Scanner/        — pen-test suite (port scan, CVE lookup, config audit)
  IoTSpy.Manipulation/   — rules engine, scripted breakpoints, replay, fuzzer, AI mock
  IoTSpy.Storage/        — EF Core DbContext + repositories (SQLite / PostgreSQL)
  IoTSpy.Api/            — ASP.NET Core host (REST + SignalR)
  IoTSpy.Protocols.Tests/
  IoTSpy.Manipulation.Tests/
  IoTSpy.Scanner.Tests/
  IoTSpy.Api.Tests/
  IoTSpy.Proxy.Tests/
  IoTSpy.Storage.Tests/
  IoTSpy.Api.IntegrationTests/
frontend/                — Vite 6 + React 19 + TypeScript SPA
docs/
```

---

## Project dependency graph

```
IoTSpy.Api
  ├── IoTSpy.Core          ← domain only; no infrastructure deps
  ├── IoTSpy.Proxy         ← BouncyCastle, SharpPcap, Polly, IoTSpy.Protocols
  ├── IoTSpy.Storage       ← EF Core 10, Npgsql
  ├── IoTSpy.Protocols     ← IoTSpy.Core only
  ├── IoTSpy.Scanner       ← IoTSpy.Core, Microsoft.Extensions.Http
  └── IoTSpy.Manipulation  ← IoTSpy.Core, Roslyn, Jint, Microsoft.Extensions.Http
```

Test projects reference only the library under test (plus xunit 2.9.3).

---

## IoTSpy.Core

Pure domain layer — no NuGet dependencies beyond the BCL.

### Models (30+)

Domain models live in `IoTSpy.Core/Models/`; DTOs and result types are co-located with their interfaces in `IoTSpy.Core/Interfaces/`.

| Model | Purpose |
|---|---|
| `CapturedRequest` | One intercepted HTTP/HTTPS exchange; includes request + response headers/body, TLS flag, protocol, timestamp, durationMs, `IsModified`, `TlsMetadataJson` |
| `Device` | IoT device record: IP, MAC, hostname, vendor, label, `SecurityScore` |
| `ProxySettings` | Singleton config row (Id=1): ports, mode, TLS/body capture flags, PBKDF2 password hash, `SslStrip` toggle |
| `TlsMetadata` | All metadata extracted from TLS handshake during passthrough mode: SNI, JA3/JA3S, cipher suites, server cert details, byte counts (serialized as JSON in `CapturedRequest.TlsMetadataJson`) |
| `CertificateEntry` | PEM-encoded root CA or per-host leaf cert with validity range |
| `ResilienceOptions` | Typed options for Polly pipeline configuration |
| `ScanJob` | A scanner run: target IP, port range, which checks to run, status, result counts |
| `ScanFinding` | One finding from a scan: type, severity, port, service, banner, CPE, CVE |
| `ManipulationRule` | Declarative traffic rewrite rule: match patterns → action |
| `Breakpoint` | C# or JS script invoked on matching traffic |
| `HttpMessage` | Mutable HTTP message used within the manipulation pipeline |
| `ReplaySession` | Re-played capture: modified request + recorded response |
| `FuzzerJob` | A fuzzer run: base capture, strategy, concurrency, status |
| `FuzzerResult` | One fuzzer mutation result with anomaly flag |
| `AiMockResponse` | LLM-generated mock HTTP response |
| `AiProviderConfig` | AI provider selection and credentials |
| `AnomalyAlert` | Statistical anomaly: host, type, expected/actual values, deviation factor |
| `HostBaseline` | Per-host Welford running state: duration/size mean+M2, status distribution, request timestamps |
| `OpenRtbEvent` | Captured OpenRTB bid request/response with decoded fields |
| `OpenRtbPiiPolicy` | PII redaction policy: field path, strategy, auto-apply flag |
| `PiiStrippingLog` | Audit log entry for PII redaction actions |
| `ScheduledScan` | Cron-scheduled recurring scan job (Phase 9) |
| `CaptureDevice` | Network interface available for packet capture (name, IPs, MAC) |
| `CapturedPacket` | Captured network packet with L2-L4 fields, protocol flags, `[NotMapped] RawData` |
| `PacketFilter` | Filter criteria for captured packet queries |
| `NetworkDevice` | Discovered network device from packet capture |
| `WebSocketFrame` | Captured WebSocket frame with opcode, payload, direction |
| `MqttCapturedMessage` | Captured MQTT message with topic, payload, QoS |
| `MqttBrokerSettings` | Configuration for the MQTT broker proxy |
| `CoapProxySettings` | Configuration for the CoAP UDP proxy |
| `PacketFilterDto` | API-facing filter DTO: protocol, IP, port, time range, payload search |
| `FreezeFrameResult` | Frozen packet with hex dump and per-layer breakdown |
| `ProtocolDistribution` / `ProtocolStats` | Protocol distribution statistics |
| `CommunicationPattern` | Source→dest pair with packet count, bytes, protocols, time range |
| `SuspiciousActivity` | Detection result: category, severity, evidence list |
| `NetworkDeviceStatistics` | Live capture stats: PPS, BPS, drop rate |

### Interfaces (30)

`ICaptureRepository`, `IDeviceRepository`, `IProxySettingsRepository`, `ICertificateRepository`, `ICertificateAuthority`, `IProxyService`, `ICapturePublisher`, `IProtocolDecoder<T>`, `IScanJobRepository`, `IScannerService`, `IManipulationRuleRepository`, `IBreakpointRepository`, `IReplaySessionRepository`, `IFuzzerJobRepository`, `IManipulationService`, `IAiMockService`, `IAnomalyDetector`, `IAnomalyAlertPublisher`, `IPacketCaptureService`, `IPacketCaptureAnalyzer`, `IPacketCapturePublisher`, `IOpenRtbEventRepository`, `IOpenRtbPiiPolicyRepository`, `IOpenRtbService`, `IPiiStrippingLogRepository`, `ICaptureDeviceRepository`, `IAlertingService`, `IReportService`, `IScheduledScanRepository`, `IMqttBrokerProxy`, `ICoapProxy`

### Enums (13)

| Enum | Values |
|---|---|
| `ProxyMode` | ExplicitProxy, ArpSpoof, GatewayRedirect |
| `InterceptionProtocol` | Http, Https, Mqtt, MqttTls, CoAP, Dns, MDns, WebSocket, WebSocketTls, Grpc, Modbus, TlsPassthrough, Other |
| `ScanStatus` | Pending, Running, Completed, Failed, Cancelled |
| `ScanFindingType` | OpenPort, ServiceBanner, DefaultCredential, Cve, ConfigIssue |
| `ScanFindingSeverity` | Info, Low, Medium, High, Critical |
| `ManipulationPhase` | Request, Response |
| `ManipulationRuleAction` | ModifyHeader, ModifyBody, OverrideStatusCode, Drop, Delay |
| `ScriptLanguage` | CSharp, JavaScript |
| `FuzzerStrategy` | Random, Boundary, BitFlip |
| `FuzzerJobStatus` | Pending, Running, Completed, Cancelled, Failed |
| `WebSocketOpcode` | Continuation, Text, Binary, Close, Ping, Pong |
| `OpenRtbMessageType` | BidRequest, BidResponse |
| `PiiRedactionStrategy` | Redact, Hash, Mask, Remove |

---

## IoTSpy.Proxy

### Interception servers

| Class | Port | Mode |
|---|---|---|
| `ExplicitProxyServer` | 8888 (configurable) | Device explicitly configured to use proxy |
| `TransparentProxyServer` | 9999 (configurable) | Gateway / ARP-redirected traffic |

Both servers share a unified `InterceptHttpStreamAsync` pipeline: HTTP is intercepted inline; HTTPS CONNECT tunnels fork into a TLS MITM branch when `CaptureTls=true`.

### TLS MITM (`Tls/CertificateAuthority`)

1. Device sends `CONNECT host:443 HTTP/1.1`.
2. Proxy replies `200 Connection established`.
3. `CertificateAuthority.GetOrCreateHostCertAsync(host)` returns or generates a BouncyCastle leaf cert signed by the IoTSpy root CA.
4. `SslStream.AuthenticateAsServerAsync` presents the leaf cert to the device.
5. A second `SslStream.AuthenticateAsClientAsync` connects upstream (validation disabled — research tool).
6. HTTP exchanges are parsed, stored, and forwarded.

Root CA: 4096-bit RSA, self-signed, generated lazily on first use, stored as PEM in `CertificateEntries`, cached in memory.
Leaf certs: 2048-bit RSA, 825-day validity, SAN-bearing, cached in DB, reused until one day before expiry.

### TLS Passthrough (`Tls/TlsClientHelloParser`, `Tls/TlsServerHelloParser`)

When `CaptureTls=false`, traffic is **not** decrypted but metadata is extracted from the TLS handshake:

1. Proxy buffers the initial bytes from the client until a complete TLS record is available (`GetRecordLength()`).
2. `TlsClientHelloParser.TryParse()` extracts:
   - **SNI hostname** (extension type 0x0000)
   - **Cipher suites** and **extensions** lists
   - **Elliptic curves** and **EC point formats**
   - **JA3 fingerprint** — MD5 of `TLSVersion,Ciphers,Extensions,EllipticCurves,EcPointFormats` with GREASE values (RFC 8701) excluded
3. The buffered ClientHello is forwarded to the upstream server.
4. `TlsServerHelloParser.TryParseServerHello()` extracts:
   - **Selected cipher** and **TLS version** (including `supported_versions` extension for TLS 1.3 real version)
   - **JA3S fingerprint** — MD5 of `TLSVersion,Cipher,Extensions`
5. `TlsServerHelloParser.TryParseCertificate()` extracts the leaf X.509 certificate (TLS 1.2 only — Certificate is encrypted in TLS 1.3):
   - Subject, issuer, SAN list, serial number, SHA-256 fingerprint, validity dates
6. Traffic is relayed bidirectionally with byte counting (`ClientToServerBytes`, `ServerToClientBytes`).
7. A `CapturedRequest` is recorded with `Protocol=TlsPassthrough` and `TlsMetadataJson` containing the serialized `TlsMetadata` model.

All log entries include `DnsCorrelationKey={ClientIp}→{SniHostname}` for joining with DNS packet captures.

### SSL Stripping (`Interception/SslStripService`)

When `ProxySettings.SslStrip=true` on **plain HTTP** connections, `SslStripService` intercepts HTTP→HTTPS redirects and serves decrypted content over HTTP — useful for IoT devices that cannot have custom CAs installed.

1. `GetHttpsRedirectLocation()` detects 301/302/303/307/308 responses with `https://` Location headers.
2. `FetchHttpsAsync()` connects to the HTTPS URL upstream, performs a real TLS handshake, and returns the decrypted response.
3. `StripResponseHeaders()` removes `Strict-Transport-Security` headers and rewrites `https://` → `http://` in `Location`, `Set-Cookie`, and `Content-Security-Policy` headers.
4. `StripHttpsFromBody()` rewrites `https://` links in text response bodies (HTML, JSON, etc.).

Integrated into `InterceptHttpStreamAsync` of both `ExplicitProxyServer` and `TransparentProxyServer`. When `SslStrip=true`, HSTS headers are also stripped from all non-redirect responses.

### Network interception helpers

- `ArpSpoofEngine` — SharpPcap-based ARP poisoning to MITM LAN traffic without gateway cooperation.
- `IptablesHelper` — Applies/removes `iptables REDIRECT` rules to redirect device TCP traffic through `TransparentProxyServer`.

### Resilience (`Resilience/ProxyResiliencePipelines`, Polly 8)

Two named pipelines registered in DI:

```
iotspy-connect (keyed by upstream hostname):
  Timeout(ConnectTimeoutSeconds)
    → Retry(RetryCount, exponential, on SocketException | TimeoutRejectedException)
      → CircuitBreaker(per-host, FailureRatio, SamplingDuration, BreakDuration)

iotspy-tls:
  Timeout(TlsHandshakeTimeoutSeconds)   ← TLS handshake not safely retryable
```

All values are configurable under `Resilience` in `appsettings.json`:

| Key | Default |
|---|---|
| ConnectTimeoutSeconds | 15 |
| TlsHandshakeTimeoutSeconds | 10 |
| RetryCount | 2 |
| RetryBaseDelaySeconds | 0.5 |
| CircuitBreakerFailureRatio | 0.5 |
| CircuitBreakerSamplingSeconds | 30 |
| CircuitBreakerBreakSeconds | 60 |

---

## IoTSpy.Protocols

All decoders implement `IProtocolDecoder<T>`:

```csharp
public interface IProtocolDecoder<T>
{
    bool CanDecode(ReadOnlySpan<byte> header);   // fast sniff
    Task<IReadOnlyList<T>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}
```

### Decoder inventory

| Subdirectory | Decoder | Message type | Protocol / format |
|---|---|---|---|
| `Mqtt/` | `MqttDecoder` | `MqttMessage` | MQTT 3.1.1 and 5.0 (all packet types, QoS, props) |
| `Dns/` | `DnsDecoder` | `DnsMessage` | DNS (RFC 1035) and mDNS (RFC 6762), with label decompression |
| `Coap/` | — | `CoapMessage` | CoAP (RFC 7252) UDP; `CoapCode`, `CoapMessageType`, `CoapOptionNumber` |
| `Telemetry/` | `DatadogDecoder` | `TelemetryMessage` | Datadog v1/v2 series + logs intake JSON |
| `Telemetry/` | `FirehoseDecoder` | `TelemetryMessage` | AWS Kinesis Firehose HTTP PUT (base64 records) |
| `Telemetry/` | `SplunkHecDecoder` | `TelemetryMessage` | Splunk HEC single-event + newline-batched JSON |
| `Telemetry/` | `AzureMonitorDecoder` | `TelemetryMessage` | Azure Monitor array / `records`-wrapped / DCR ingestion payloads |
| `OpenRtb/` | `OpenRtbDecoder` | `OpenRtbEvent` | OpenRTB 2.5 bid request/response parsing |
| `WebSocket/` | `WebSocketDecoder` | `WebSocketDecodedFrame` | RFC 6455 frame decoding (FIN, opcode, masking, extended lengths, close codes) |
| `Grpc/` | `GrpcDecoder` | `GrpcMessage` | gRPC Length-Prefixed Message framing + schema-less protobuf field extraction |
| `Modbus/` | `ModbusDecoder` | `ModbusMessage` | Modbus TCP MBAP header, function codes 1-16 + exception responses |
| `Coap/` | `CoapDecoder` | `CoapMessage` | RFC 7252 full message decoding (header, tokens, delta-encoded options, payload) |

`TelemetryMessage` carries: detected protocol, source, timestamp, flat `Fields` map, per-event `Events` list, raw JSON (capped at 8 KB).

### Anomaly detection (`Anomaly/AnomalyDetector`)

Thread-safe, per-host statistical baseline updated with **Welford's online algorithm**. No external dependencies.

**`IAnomalyDetector.Record(host, durationMs, sizeBytes, statusCode)`**
- Updates Welford running mean + M2 for duration and size.
- Maintains status code distribution dictionary.
- Maintains a sliding timestamp queue for rate measurement.
- Returns `IReadOnlyList<AnomalyAlert>` (empty during warm-up period).

**Four alert types:**

| Alert | Trigger |
|---|---|
| `ResponseTime` | `|observed − mean| / stddev ≥ DeviationThreshold` |
| `ResponseSize` | Same formula applied to body size |
| `StatusCode` | Observed HTTP class differs from dominant class **and** dominant code holds ≥ 90% of historical observations |
| `RequestRate` | Current window rate (req/s) exceeds `historicalRate × (1 + DeviationThreshold)` |

Configurable properties: `DeviationThreshold` (default 3.0), `WarmUpSamples` (default 30), `RateWindowSeconds` (default 60).

---

## IoTSpy.Scanner

`ScannerService` orchestrates the full pipeline in a background task; updates `Device.SecurityScore` on completion.

| Class | Responsibility |
|---|---|
| `PortScanner` | TCP connect scan; configurable port ranges and concurrency |
| `ServiceFingerprinter` | Banner grab; CPE string extraction via regex heuristics |
| `CredentialTester` | Default credential checks: FTP, Telnet, MQTT |
| `CveLookupService` | OSV.dev API lookup keyed by CPE |
| `ConfigAuditor` | Detects: Telnet open, UPnP responding, anonymous MQTT, exposed DB ports, HTTP admin interfaces |

### Packet capture (`PacketCaptureService`)

SharpPcap / PacketDotNet-based live packet capture.

| Feature | Details |
|---|---|
| Ring buffer | `LinkedList<CapturedPacket>` with 10,000 packet cap (O(1) enqueue/dequeue) |
| Protocol parsing | Ethernet, IPv4/IPv6, TCP, UDP, ARP, ICMP; app-layer: HTTP, HTTPS, MQTT, DNS, CoAP, DHCP, SSH, Telnet |
| Raw data | `CapturedPacket.RawData` (`[NotMapped]` byte array) stored in-memory for PCAP export |
| Hex dump | Wireshark-style `FormatHexDump` (offset | hex | ASCII) |
| Device sync | `EnsureDevicesEnumeratedAsync()` lazily syncs SharpPcap interfaces to DB on first call |
| Publishing | Fire-and-forget via `PublishSafeAsync` (catches + logs exceptions) |

Registered via `ScannerExtensions.AddIoTSpyScanner()` (all singletons).

---

## IoTSpy.Manipulation

`ManipulationService` is the DI-registered orchestrator (`IManipulationService`) and is called inline by both proxy servers for every intercepted exchange.

### Rules engine (`RulesEngine`)

Declarative match → modify rules evaluated in priority order. Match fields: `HostPattern`, `PathPattern`, `MethodPattern` (all regex). Actions: `ModifyHeader`, `ModifyBody`, `OverrideStatusCode`, `Drop`, `Delay`. Operates on `HttpMessage` at request or response phase.

### Scripted breakpoints

- `CSharpScriptEngine` — Roslyn-based C# script compilation and execution. Script receives the `HttpMessage` and can inspect/modify it.
- `JavaScriptEngine` — Jint-based JS execution with the same contract.

### Replay (`ReplayService`)

Re-sends a captured request with optional modifications; records the new response as a `ReplaySession`. Supports arbitrary header/body overrides.

### Fuzzer (`FuzzerService`)

Mutation-based fuzzer with three strategies:

| Strategy | Description |
|---|---|
| `Random` | Random byte substitutions in the request body |
| `Boundary` | Injects boundary values (empty, max-length, null bytes, format strings) |
| `BitFlip` | Sequential single-bit flips across the body |

Runs mutations concurrently (semaphore-controlled); records each response as a `FuzzerResult`; flags anomalies by comparing response status/size to baseline.

### AI mock engine (`AiMock/`)

Pluggable LLM provider for generating synthetic HTTP responses.

```json
"AiMock": {
  "Provider": "claude",         // claude | openai | ollama
  "Model": "claude-sonnet-4-6",
  "ApiKey": "sk-ant-..."
}
```

Providers: `ClaudeProvider`, `OpenAiProvider`, `OllamaProvider` — all implement `IAiProvider.CompleteAsync(systemPrompt, userPrompt)`.
`AiProviderFactory` selects the provider from config.
`AiMockService` builds a schema from historical captures for the target host, prompts the LLM, parses the structured response, and caches the schema in memory (invalidatable per host).

### Packet analysis (`Analysis/PacketCaptureAnalyzer`)

Implements `IPacketCaptureAnalyzer` — freeze frame, protocol distribution, pattern detection, suspicious activity.

| Detection | Trigger |
|---|---|
| Port scan | 50+ unique destination ports from a single source |
| ARP spoofing | Multiple MAC addresses observed for the same IP |
| DNS anomaly | Long domain queries (>50 chars) or DGA-like patterns |
| Retransmission burst | TCP retransmission rate exceeds 10% |

Thread-safe with lock-based synchronization. Freeze/unfreeze toggles snapshot mode.

Registered via `ManipulationExtensions.AddIoTSpyManipulation(aiConfig)`.

---

## IoTSpy.Storage

EF Core 10 data access layer; supports SQLite (default) and PostgreSQL.

### DbSets

| Table | Entity | Notes |
|---|---|---|
| `Captures` | `CapturedRequest` | Indexed on Timestamp, DeviceId, Host |
| `Devices` | `Device` | Unique index on IpAddress |
| `ProxySettings` | `ProxySettings` | Always Id=1 |
| `Certificates` | `CertificateEntry` | Root CA + per-host leaf certs |
| `ScanJobs` | `ScanJob` | Cascade-deletes ScanFindings |
| `ScanFindings` | `ScanFinding` | FK → ScanJob |
| `ManipulationRules` | `ManipulationRule` | Indexed on Enabled + Priority |
| `Breakpoints` | `Breakpoint` | — |
| `ReplaySessions` | `ReplaySession` | — |
| `FuzzerJobs` | `FuzzerJob` | Cascade-deletes FuzzerResults |
| `FuzzerResults` | `FuzzerResult` | FK → FuzzerJob |
| `OpenRtbEvents` | `OpenRtbEvent` | OpenRTB bid request/response events |
| `OpenRtbPiiPolicies` | `OpenRtbPiiPolicy` | PII redaction policies |
| `PiiStrippingLogs` | `PiiStrippingLog` | PII redaction audit trail |
| `CaptureDevices` | `CaptureDevice` | Indexed on IpAddress, MacAddress |
| `Packets` | `CapturedPacket` | FK → CaptureDevice; indexed on Timestamp, DeviceId, Protocol, SourceIp, DestinationIp |
| `ScheduledScans` | `ScheduledScan` | Cron-based recurring scan jobs (Phase 9) |

`DateTimeOffset` columns are stored as Unix milliseconds (`long`) via a `ValueConverter` — required for SQLite `ORDER BY` compatibility.

### Repositories (12+)

`CaptureRepository`, `DeviceRepository`, `CertificateRepository`, `ProxySettingsRepository`, `ScanJobRepository`, `ManipulationRuleRepository`, `BreakpointRepository`, `ReplaySessionRepository`, `FuzzerJobRepository`, `OpenRtbEventRepository`, `OpenRtbPiiPolicyRepository`, `PiiStrippingLogRepository`, `CaptureDeviceRepository`, `ScheduledScanRepository`

All repositories are **scoped** (one per HTTP request / DI scope) because they depend on the scoped EF Core `DbContext`.

### Migrations (10)

| Migration | Contents |
|---|---|
| `InitialCreate` | Devices, Captures, ProxySettings, CertificateEntries |
| `AddPhase2ProxySettings` | TransparentProxyPort, TargetDeviceIp, GatewayIp columns |
| `AddPhase3Scanner` | ScanJobs, ScanFindings |
| `AddPhase4ManipulationFix` | ManipulationRules, Breakpoints, ReplaySessions, FuzzerJobs, FuzzerResults |
| `AddOpenRtbInspection` | OpenRtbEvents, OpenRtbPiiPolicies, PiiStrippingLogs |
| `AddPacketCapture` | CaptureDevices, Packets (with FK and indexes) |
| `AddMissingPhase7Changes` | Schema fixes for Phase 7 test compatibility |
| `AddPhase9ScheduledScans` | ScheduledScans table |
| `AddBodyCaptureDefaults` | Body capture default column values |
| `AddTlsPassthroughAndSslStrip` | `TlsMetadataJson` (TEXT) on Captures, `SslStrip` (BOOLEAN) on ProxySettings |

---

## IoTSpy.Api

ASP.NET Core 10 host. `Program.cs` wires everything up in order: Storage → Auth → SignalR → Proxy → Scanner → Manipulation → Controllers → CORS → SPA fallback.

### Service lifetimes

- **Singleton**: `ExplicitProxyServer`, `TransparentProxyServer`, `CertificateAuthority`, `ProxyService`, `SslStripService`, `ArpSpoofEngine`, `IptablesHelper`, `SignalRCapturePublisher`, `SignalRAnomalyPublisher`, `PortScanner`, `ServiceFingerprinter`, `CredentialTester`, `ConfigAuditor`, `ScannerService`, `MqttBrokerProxy`, `CoapProxy`
  *(TCP listeners and other long-lived services that must not be re-created per request)*
- **Hosted services**: `ProxyService` (via `AddHostedService(sp => sp.GetRequiredService<ProxyService>())`), `DataRetentionService` (background cleanup; disabled by default)
- **Scoped**: All EF Core repositories, `DbContext`
- `ProxyService` is registered once as `IProxyService` and once as `IHostedService` via `AddHostedService(sp => sp.GetRequiredService<ProxyService>())` to prevent double instantiation.

### Controllers (12)

| Controller | Endpoints |
|---|---|
| `AuthController` | `POST /api/auth/setup`, `POST /api/auth/login` |
| `ProxyController` | `GET/PUT /api/proxy/settings`, `POST /api/proxy/start`, `POST /api/proxy/stop` |
| `CapturesController` | `GET /api/captures` (paged + filtered), `GET /api/captures/{id}`, `DELETE /api/captures/{id}` |
| `DevicesController` | `GET /api/devices`, `GET /api/devices/{id}`, `PUT /api/devices/{id}`, `DELETE /api/devices/{id}` |
| `CertificatesController` | `GET /api/certificates/root` (download CA PEM, no auth) |
| `ScannerController` | `POST /api/scanner/scan`, `GET /api/scanner/jobs`, `GET /api/scanner/jobs/{id}`, `GET /api/scanner/jobs/{id}/findings`, `POST /api/scanner/jobs/{id}/cancel`, `DELETE /api/scanner/jobs/{id}` |
| `ManipulationController` | CRUD for rules, breakpoints; `POST /replay`, `POST /fuzzer`, `GET+DELETE /fuzzer/{id}`, `POST /ai-mock/generate`, `DELETE /ai-mock/{host}/cache` |
| `PacketCaptureController` | Device list, start/stop capture, packet filtering, freeze frame (create/get), protocol distribution, communication patterns, suspicious activity, freeze/unfreeze analysis |
| `OpenRtbController` | OpenRTB events CRUD, PII policies CRUD, PII audit logs |
| `ProtocolProxyController` | MQTT start/stop/status, CoAP start/stop/status |
| `ReportController` | `GET /api/reports/devices/{id}/html`, `GET /api/reports/devices/{id}/pdf` — scan report generation |
| `ScheduledScanController` | CRUD `GET/POST/PUT/DELETE /api/scheduled-scans` — cron-based recurring scan jobs |

### SignalR

- Hub: `TrafficHub` at `/hubs/traffic`
- Clients join device groups: `hubConnection.invoke("JoinDeviceGroup", deviceId)`
- `SignalRCapturePublisher` broadcasts a `TrafficCapture` event to all clients and to the device's group on every captured request
- Clients subscribe to anomaly alerts: `hubConnection.invoke("SubscribeToAnomalyAlerts")` → receives `AnomalyAlert` events on group `"anomaly-alerts"`; `SignalRAnomalyPublisher` sends alerts emitted by `AnomalyDetector` after each proxied request
- Hub: `PacketCaptureHub` at `/hubs/packets` — clients auto-join `packet-capture-live` group
- `SignalRPacketPublisher` broadcasts `PacketCaptured` and `CaptureStatus` events for live packet streaming
- Token is accepted via `?access_token=` query parameter (standard SignalR pattern)

### Observability & hardening (Phase 8)

| Feature | Implementation |
|---|---|
| Health checks | `MapHealthChecks("/health")` — liveness + EF Core DB connectivity; `MapHealthChecks("/ready")` — readiness probe. JSON response via `WriteResponse` writer. |
| Structured logging | Serilog with `Console` + rolling `File` sinks (7-day retention). `UseSerilogRequestLogging()` enriches each request log with host and user-agent. Configurable via `Serilog` section. |
| Rate limiting | ASP.NET Core `RateLimiter` middleware. Sliding-window policy partitioned by JWT `sub` (falls back to IP). Default: 100 permits / 60 s. Toggle via `RateLimit:Enabled`. |
| Data retention | `DataRetentionService` (`IHostedService`) runs on configurable interval (default 24 h). Deletes `CapturedRequests`, `CapturedPackets`, `ScanJobs`, and `OpenRtbEvents` older than their configured TTL. **Disabled by default.** |
| Graceful shutdown | Both proxy servers track active connection counts with `Interlocked`. `StopAsync` waits up to a configurable timeout (default 10 s) for in-flight connections to drain. |
| DB connection pooling | `StorageExtensions.AddIoTSpyStorage()` accepts `maxPoolSize` / `minPoolSize`; Postgres connection strings are auto-augmented with `Maximum Pool Size` / `Minimum Pool Size`. |

### Authentication

- Single-user model; PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) password hash stored in `ProxySettings.PasswordHash`
- JWT issuer and audience: `"iotspy"`
- `Auth:JwtSecret` must be ≥ 32 characters; app throws on startup if absent
- OpenAPI (Scalar) at `/scalar` in Development mode only

---

## Data flow

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
                                        ├─ IManipulationService (rules → scripts, request + response phases)
                                        ├─ CaptureRepository (SQLite/PG)
                                        └─ ICapturePublisher → SignalR (group-routed) → dashboard
```

---

## Frontend (Vite 6 + React 19 + TypeScript)

Located in `frontend/`. Dev server on `:3000`; Vite proxies `/api` and `/hubs` to `http://localhost:5000`.

### Auth flow

`GET /api/auth/status` on mount → `/setup` if no password set → `/login` if no token → dashboard.
JWT stored in `localStorage['iotspy_token']`; passed as `?access_token=` for SignalR.

### Layout & panels

| Panel | Components |
|---|---|
| Captures | Split-pane list + detail (request / response / TLS tabs); `CaptureFilterBar` |
| Devices | Device list with timeline swimlane view per device |
| Scanner | `ScannerPanel` → `ScanJobList` + `ScanFindingsView` |
| Manipulation | `ManipulationPanel` → `RulesEditor`, `BreakpointsEditor`, `ReplayPanel`, `FuzzerPanel` |
| Packet Capture | `PanelPacketCapture` (tabbed: Packets / Protocols / Patterns / Suspicious) → `PacketListFilterable`, `PacketInspector` (Details / Hex Dump / Layers), `ProtocolDistributionView`, `PatternExplorer`, `SuspiciousActivityPanel` |
| Live stream | `useTrafficStream` via SignalR; new captures prepended in real time |

### Source structure

```
frontend/src/
  types/api.ts              # TS interfaces matching all backend DTOs
  api/
    apiFetch.ts             # fetch wrapper (auth header, error handling)
    captures.ts
    devices.ts
    proxy.ts
    scanner.ts
    manipulation.ts
    packetCapture.ts          # Packet capture + analysis endpoints
  store/authStore.tsx        # React Context + useReducer
  hooks/
    useAuth.ts
    useCaptures.ts
    useDevices.ts
    useProxy.ts
    useTrafficStream.ts      # SignalR live stream
    useScanner.ts
    useManipulation.ts
    usePacketCapture.ts      # SignalR packet stream + capture lifecycle
    usePacketAnalysis.ts     # Protocol distribution, patterns, suspicious activity
  pages/                     # SetupPage, LoginPage, DashboardPage
  components/                # layout, captures, capture-detail, proxy, devices,
                             # scanner, manipulation, common
  styles/                    # CSS custom properties + per-component sheets
```

---

## Test projects

248 backend tests + 11 frontend component tests. All passing. Coverage reported via Coverlet + ReportGenerator in CI.

| Project | Test classes | Coverage |
|---|---|---|
| `IoTSpy.Protocols.Tests` | `MqttDecoderTests`, `DnsDecoderTests`, `TelemetryDecoderTests`, `AnomalyDetectorTests` | MQTT 3.1.1/5.0 packet types; DNS/mDNS; all four telemetry decoders; anomaly detection (warm-up, per-type alerts, convergence, Reset, independent hosts) |
| `IoTSpy.Manipulation.Tests` | `RulesEngineTests`, `FuzzerServiceTests`, `OpenRtbPiiServiceTests` | Rule matching + all actions; fuzzer mutation strategies; OpenRTB PII detection + redaction |
| `IoTSpy.Scanner.Tests` | `PortScannerTests`, `PacketCaptureServiceTests` | Concurrency limiting, timeout handling; ring buffer, PCAP export |
| `IoTSpy.Api.Tests` | `AuthControllerTests`, `ProxyControllerTests`, `CapturesControllerTests`, `DevicesControllerTests`, `ScannerControllerTests`, `AuthServiceTests` | Controller unit tests with NSubstitute mocks; `AuthService` BCrypt/JWT logic |
| `IoTSpy.Proxy.Tests` | `ProxyServiceTests`, `ResilienceOptionsTests`, `GracefulShutdownTests` | ProxyService state machine; resilience defaults; graceful shutdown drain |
| `IoTSpy.Storage.Tests` | `DeviceRepositoryTests`, `CaptureRepositoryTests`, `ProxySettingsRepositoryTests`, `DataRetentionServiceTests` | EF Core in-memory SQLite integration tests; data retention service cleanup |
| `IoTSpy.Api.IntegrationTests` | `AuthIntegrationTests`, `DevicesIntegrationTests`, `HealthCheckEndpointTests` | `WebApplicationFactory` with NSubstitute fakes; full HTTP auth flow; health check endpoint contract |

---

## Configuration reference

```jsonc
// src/IoTSpy.Api/appsettings.json
{
  "Database": {
    "Provider": "sqlite",            // sqlite | postgres
    "ConnectionString": "Data Source=iotspy.db"
  },
  "Auth": {
    "JwtSecret": "<≥32 chars>",      // REQUIRED — also via Auth__JwtSecret env var
    "PasswordHash": ""               // populated by POST /api/auth/setup
  },
  "Frontend": {
    "Origin": "http://localhost:3000"  // CORS allowed origin
  },
  "Urls": "http://localhost:5000",
  "Resilience": {
    "ConnectTimeoutSeconds": 15,
    "TlsHandshakeTimeoutSeconds": 10,
    "RetryCount": 2,
    "RetryBaseDelaySeconds": 0.5,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerSamplingSeconds": 30,
    "CircuitBreakerBreakSeconds": 60
  },
  "AiMock": {
    "Provider": "claude",            // claude | openai | ollama
    "Model": "claude-sonnet-4-6",
    "ApiKey": "",
    "BaseUrl": ""                    // Ollama only
  },
  "Serilog": {
    "MinimumLevel": "Information"    // overridable per namespace
  },
  "RateLimit": {
    "Enabled": true,
    "PermitLimit": 100,
    "WindowSeconds": 60
  },
  "DataRetention": {
    "Enabled": false,                // opt-in; set true to enable automatic cleanup
    "IntervalHours": 24,
    "CaptureRetentionDays": 30,
    "PacketRetentionDays": 7,
    "ScanJobRetentionDays": 90,
    "OpenRtbEventRetentionDays": 14
  }
}
```

Environment variable overrides use double-underscore: `Auth__JwtSecret=...`

---

## Extending the system

### Adding a protocol decoder

1. Create a class in `IoTSpy.Protocols/<Protocol>/` implementing `IProtocolDecoder<YourMessage>`.
2. Add the decoder as a singleton in `Program.cs` (or a dedicated extensions class).
3. Add tests to `IoTSpy.Protocols.Tests`.

### Adding a manipulation action

1. Add a value to `ManipulationRuleAction` in `IoTSpy.Core`.
2. Implement the action branch in `RulesEngine.ApplyAsync`.
3. Add the corresponding `ManipulationController` endpoint if it needs a dedicated API.

### Switching AI provider

Update `AiMock:Provider` in `appsettings.json` to `claude`, `openai`, or `ollama`. No code changes required. For Ollama, set `AiMock:BaseUrl` to the local Ollama API address (e.g. `http://localhost:11434`).

### Switching database

Set `Database:Provider` to `postgres` and provide a Postgres connection string in `Database:ConnectionString`. Run `dotnet ef database update` to apply migrations.
