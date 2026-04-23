# IoTSpy — Completed Phases (1–16, 18–22)

This document details all implemented phases. Phases 1–16 and 18–22 are complete and production-ready. Phase 17 has been archived (hardware-dependent non-IP IoT protocols); see [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md). See [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for the Phase 23+ roadmap.

---

## Phase 1 — Foundation

Proxy server + REST API + React dashboard scaffold. Explicit proxy (HTTP + HTTPS CONNECT), TLS MITM with BouncyCastle CA, Polly 8 resilience, JWT auth, SignalR live streaming, EF Core (SQLite/Postgres), Docker support.

## Phase 2 — Interception modes + protocols

MQTT 3.1.1/5.0 and DNS/mDNS decoders. Gateway redirect mode (iptables). ARP spoof mode (SharpPcap). SignalR filter subscriptions. Timeline swimlane view.

## Phase 3 — Pen-test suite

TCP port scan, service fingerprinting (banner grab + CPE), default credential testing (FTP/Telnet/MQTT), CVE lookup (OSV.dev), config audit (Telnet/UPnP/anon MQTT/exposed DB/HTTP admin). `ScannerController` + frontend.

## Phase 4 — Active manipulation

Declarative rules engine (regex match → modify/drop/delay). C# (Roslyn) and JavaScript (Jint) scripted breakpoints. Request replay. Mutation fuzzer (Random/Boundary/BitFlip). `ManipulationController` + frontend.

## Phase 5 — AI mock + advanced protocols

AI mock engine with pluggable providers (Claude/OpenAI/Ollama). CoAP UDP decoder. Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor). Statistical anomaly detection (Welford online algorithm).

## Phase 6 — Packet capture & analysis

SharpPcap live capture with ring buffer (10k). Protocol distribution, communication patterns, suspicious activity detection (port scan, ARP spoof, DNS anomaly, retransmission burst). Hex dump + freeze frame. PCAP export. `PacketCaptureController` (14 endpoints) + `PacketCaptureHub` SignalR.

## OpenRTB inspection

OpenRTB 2.5 bid request/response decoding. PII detection + policy-based redaction. Inline detection in both proxy servers. `OpenRtbController` + frontend (traffic list, inspector, PII policy editor, audit log).

## Phase 7 — Test coverage & CI/CD

7 test projects, 248+ backend tests. Controller unit tests (NSubstitute), proxy service tests, repository integration tests (EF Core SQLite in-memory), `WebApplicationFactory` integration tests. Frontend: Vitest + React Testing Library (11 tests). GitHub Actions CI with Coverlet coverage.

## Phase 8 — Observability & production hardening

Health checks (`/health`, `/ready`). Serilog structured logging (console + rolling file, 7-day retention). Rate limiting (sliding window, 100/60s). `DataRetentionService` (configurable TTLs, disabled by default). `AnomalyDetector` wired into proxy pipeline with SignalR alerts. Graceful shutdown (connection draining). DB connection pooling.

## Phase 9 — Export & reporting

HTML/PDF scan report generation (`ReportController`). Capture export (CSV/JSON/HAR). Filtered PCAP export. Alerting (webhook/email via `IAlertingService`). Scheduled scans with cron expressions (`ScheduledScanController`).

## Phase 10 — Protocol expansion & active proxying

WebSocket interception (bidirectional frame relay + capture). MQTT broker proxy (TCP MITM, topic-level wildcard filtering, SignalR message publishing). CoAP UDP forward proxy (message decoding, device registration, capture). gRPC/Protobuf decoder (LPM framing + schema-less field extraction). Modbus TCP decoder (MBAP, function codes 1-16). `ProtocolProxyController` (6 endpoints).

## TLS Passthrough & SSL Stripping

**Goal:** HTTPS visibility for IoT devices where CA installation is not possible.

- `TlsClientHelloParser` — SNI extraction, cipher suite enumeration, JA3 fingerprint with GREASE filtering (RFC 8701)
- `TlsServerHelloParser` — ServerHello parsing (selected cipher, `supported_versions` for TLS 1.3, JA3S), Certificate extraction (TLS 1.2 only — encrypted in TLS 1.3)
- `HandleTlsPassthroughAsync` — in both `ExplicitProxyServer` and `TransparentProxyServer`: buffer ClientHello, parse SNI/JA3, relay, parse ServerHello/JA3S + Certificate, count bytes, record `CapturedRequest` with `Protocol=TlsPassthrough` and `TlsMetadataJson`
- `SslStripService` — intercept HTTP→HTTPS redirects, follow upstream TLS, strip HSTS headers, rewrite `https://` links in Location/Set-Cookie/CSP headers and HTML/JSON bodies
- `DnsCorrelationKey={ClientIp}→{SniHostname}` structured logging on all passthrough events for DNS-to-TLS correlation
- `AddTlsPassthroughAndSslStrip` migration — `TlsMetadataJson` on Captures, `SslStrip` on ProxySettings

## Phase 11 — UX, multi-user & technical debt

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

## Phase 12 — API Spec Generation & Content-Aware Mocking

**Goal:** Generate OpenAPI 3.0 specs from captured traffic, mock API responses based on observed data, and replace content (images, video, etc.) in responses with custom assets.

- **OpenAPI spec generation** — `ApiSpecGenerator` analyzes captured traffic to produce OpenAPI 3.0 JSON; path normalization detects GUID/numeric/hex segments and replaces with `{id}` placeholders; recursive JSON schema inference with format detection (uuid, date-time, email, uri)
- **Import/export** — Import and export API spec `.json` files via `ApiSpecController`; specs can be shared across environments
- **Passthrough-first mocking** — `ApiSpecMockService` allows real traffic through while observing payloads; dual-layer cache (ConcurrentDictionary + Timer-based background DB flush) records observed responses as OpenAPI examples
- **LLM-enhanced refinement** — `ApiSpecLlmEnhancer` uses existing `IAiProvider` infrastructure (Claude/OpenAI/Ollama) to improve spec descriptions, add summaries, and infer semantic types
- **Content replacement engine** — `ContentReplacer` supports four match types: `ContentType` (wildcards like `image/*`), `JsonPath`, `HeaderValue`, `BodyRegex`; four actions: `ReplaceWithFile`, `ReplaceWithUrl`, `ReplaceWithValue`, `Redact`; priority-based rule ordering with host/path scope patterns
- **Asset management** — Local filesystem storage (`./data/assets/`) for replacement files (images, video, audio); upload/list/delete via API endpoints; 50MB upload limit
- **Proxy pipeline integration** — `ApiSpecMockService.ApplyMockAsync` called in the manipulation pipeline between rules engine and breakpoint scripts
- **Models** — `ApiSpecDocument` (spec entity with status, mock/passthrough/LLM flags), `ContentReplacementRule` (replacement rule with match type, action, priority), `ApiSpecGenerationRequest` (generation DTO)
- **Enums** — `ApiSpecStatus` (Draft/Active/Archived), `ContentMatchType` (ContentType/JsonPath/HeaderValue/BodyRegex), `ContentReplacementAction` (ReplaceWithFile/ReplaceWithUrl/ReplaceWithValue/Redact)
- **Controller** — `ApiSpecController` with 20+ endpoints: spec CRUD, generate, import, export, refine, activate/deactivate, replacement rules CRUD, asset upload/list/delete
- **Tests** — `ApiSpecGeneratorTests` (18 tests: path normalization, JSON schema inference, format detection), `ContentReplacerTests` (14 tests: match types, actions, scope, priority), `ApiSpecControllerTests` (14 tests: controller endpoints)
- **Frontend** — `ApiSpecPanel`, `GenerateSpecDialog`, `SpecEditor`, `ReplacementRulesEditor`, `ImportExportControls` components; `useApiSpec` hook; new "API Spec" tab in ManipulationPanel
- EF Core migration `AddApiSpecAndContentReplacement` — adds `ApiSpecDocuments` and `ContentReplacementRules` tables with indexes and FK cascade delete

## Phase 13 — PCAP Import & Offline Analysis

**Goal:** Allow users to upload and analyze existing PCAP files, not just live captures.

- **PCAP/pcapng upload** — `POST /api/packet-capture/import` — multipart upload (up to 200 MB); writes to temp file; parsed synchronously via SharpPcap `CaptureFileReaderDevice` (handles both `.pcap` and `.pcapng` natively); packets tagged `Source=Import` ([NotMapped] field on `CapturedPacket`)
- **Import progress streaming** — `PublishImportProgressAsync` added to `IPacketCapturePublisher`/`SignalRPacketPublisher`; broadcasts `ImportProgress` SignalR event every 100 packets with `jobId`, `processed`, `total`, `percent`; frontend hook subscribes and auto-clears progress after completion
- **In-memory buffer replacement** — Imported packets replace `_livePackets` ring buffer so all existing analysis features (protocol distribution, communication patterns, suspicious activity, freeze-frame, hex dump) work identically on imported data
- **Export round-trip compatibility** — Existing `ExportToPcapAsync`/`ExportToPcapFilteredAsync` write the same libpcap format (magic `0xa1b2c3d4`, LINKTYPE_ETHERNET) that the importer reads; `RawData` is preserved through import so re-export produces a valid PCAP
- **TCP session reconstruction** — `TcpSessionReconstructor` (new, `IoTSpy.Scanner`) reassembles TCP flows, detects HTTP on standard and non-standard ports (payload sniff), and enriches `CapturedPacket.HttpMethodName`/`HttpRequestUri`/`HttpResponseCode` fields; returns count of reconstructed sessions included in import response
- **Frontend** — Drag-and-drop upload zone in `PanelPacketCapture` left panel; animated progress bar and packet count display during import; result summary (imported/skipped/sessions) shown after completion; Export PCAP button uses authenticated `fetch` → Blob download
- **No migration required** — `Source` field is `[NotMapped]` consistent with `RawData`; no DB schema changes needed

## Phase 14 — API Key Management & Service Accounts

**Goal:** Enable programmatic/CI access to IoTSpy without sharing user credentials.

- **`ApiKey` model** — `Id`, `Name`, `KeyHash` (SHA-256 Base64, unique), `Scopes` (space-delimited), `ExpiresAt`, `LastUsedAt`, `OwnerId`, `IsRevoked`, `CreatedAt`
- **`IApiKeyRepository` / `ApiKeyRepository`** — CRUD + `GetByHashAsync` for O(1) lookup at auth time
- **API key issuance** — `POST /api/auth/api-keys` (admin/operator); generates `iotspy_<64-hex>` key, returns plaintext once, stores SHA-256 hash
- **`ApiKeyAuthenticationHandler`** — ASP.NET Core custom scheme (`ApiKey`); reads `X-Api-Key` header, hashes it, looks up in DB, builds `ClaimsPrincipal` with owner's role + scope claims; fires and forgets `LastUsedAt` update
- **Policy scheme forwarding** — `SmartScheme` auto-selects `ApiKey` scheme when `X-Api-Key` header is present, `JwtBearer` otherwise — all existing `[Authorize]` attributes work unchanged
- **Scope enforcement** — `RequireScopeAttribute : IAuthorizationFilter`; JWT users bypass scope check; API key users must carry the required `scope` claim; 9 predefined scopes: `captures:read/write`, `scanner:read/write`, `manipulation:read/write`, `packets:read/write`, `admin`
- **Key rotation** — `POST /api/auth/api-keys/{id}/rotate`; revokes old key, creates replacement with same name/scopes/expiry, returns new plaintext key once
- **Key revocation** — `DELETE /api/auth/api-keys/{id}` sets `IsRevoked=true`; admins can revoke any key, operators only their own
- **All operations audit-logged** via `AuditEntry`
- **Frontend** — `ApiKeysTab` in Admin panel: table of all keys, scope badges, status, last-used; create dialog with scope checkboxes + optional expiry; copy-to-clipboard banner after creation/rotation; revoke/rotate per-row; confirmation guard on revoke
- **EF Core migration** `AddApiKeyManagement` — `ApiKeys` table with unique index on `KeyHash`
- **Tests** — `ApiKeyControllerTests` (11 tests): list, create, hash storage, revoke, rotate, hash determinism

## Phase 15 — Collaboration & Real-time Sharing

**Goal:** Support multiple operators monitoring the same device/proxy session simultaneously with role-aware views.

- **Shared investigation sessions (15.1)** — `InvestigationSession` + `SessionCapture` models; `SessionsController` (15 endpoints); SignalR group `session:{id}`; named sessions with creator, description, active/closed state
- **In-session annotations (15.2)** — `CaptureAnnotation` model; REST CRUD + SignalR `AddAnnotation`/`UpdateAnnotation`/`DeleteAnnotation` methods broadcast to session group; tags (comma-separated)
- **Viewer role restrictions (15.3)** — `CollaborationHub` enforces Admin/Operator for all write methods (AddAnnotation, UpdateAnnotation, DeleteAnnotation); Viewers receive read-only events
- **Presence indicators (15.4)** — In-memory `ConcurrentDictionary` per session in `CollaborationHub`; `JoinSession`/`LeaveSession`/`OnDisconnectedAsync` broadcast `PresenceUpdated` to group; `PresenceIndicator` component in dashboard header area
- **Activity feed (15.5)** — `SessionActivity` model; stored in DB; broadcast via `CollaborationPublisher` from controller and hub; feed tab in `SessionsPanel`; REST `GET /api/sessions/{id}/activity`
- **Session export (15.6)** — `GET /api/sessions/{id}/export` returns ZIP archive with `session.json`, `captures.json`, `annotations.json`, `activity.json`; browser download via `exportSession()` API helper
- **AirDrop Sharing (15.7)** — `ShareToken` (64-hex random) stored on `InvestigationSession`; `POST /api/sessions/{id}/share` generates token + URL; `GET /api/sessions/share/{token}` is `[AllowAnonymous]` and returns portable `iotspy-session/v1` JSON payload; share link copied to clipboard in UI
- **Models** — `InvestigationSession`, `SessionCapture`, `CaptureAnnotation`, `SessionActivity` (IoTSpy.Core)
- **Repositories** — `IInvestigationSessionRepository`/`InvestigationSessionRepository`, `ICaptureAnnotationRepository`/`CaptureAnnotationRepository`, `ISessionActivityRepository`/`SessionActivityRepository`
- **Hub** — `CollaborationHub` at `/hubs/collaboration`; `CollaborationPublisher` singleton for cross-controller broadcasting
- **EF Core migration** — `AddPhase15Collaboration` — `InvestigationSessions`, `SessionCaptures`, `CaptureAnnotations`, `SessionActivities` tables
- **Tests** — `SessionsControllerTests` (14 tests): list, create, viewer-forbid, update, add capture, conflict, annotation CRUD, share token, AirDrop endpoint
- **Frontend** — `frontend/src/api/sessions.ts`, `frontend/src/types/sessions.ts`, `frontend/src/hooks/useSessions.ts`; `SessionsPanel` (list/create/detail), `AnnotationPanel` (per-capture notes + tags), `PresenceIndicator` (avatar chips); "Sessions" tab in DashboardPage

## Phase 18 — React Frontend Performance & Correctness

**Goal:** Fix React best-practices issues identified in code audit; improve rendering performance and eliminate memory leaks.

- **Duplicate hook removal** — Removed duplicate `useCaptures` from `ManipulationPanel`; now accepts captures as prop from `DashboardPage` so SignalR live-updates are not missed
- **Dependency array fixes** — Added `analysis` to `useEffect` dependency array in `PanelPacketCapture` (stale closure fix)
- **Callback optimization** — Removed needless arrow-function wrappers around stable callbacks in `OpenRtbPanel`; pass `rtb.refreshEvents` directly to prevent defeating `React.memo`
- **API client consolidation** — Replaced raw `fetch` calls with shared `apiFetch` client in `usePacketCapture` for consistent auth/error handling
- **Component memoization** — Wrapped `CaptureRow` in `React.memo` to short-circuit re-renders on unchanged rows during SignalR events
- **Key stabilization** — Replaced array index keys with `tick.position` in `TimelineSwimlaneView` to avoid reconciliation artifacts on zoom
- **Constant hoisting** — Moved `statusBadge` colors map to module scope in `ApiSpecPanel`
- **Styling** — Added comprehensive `manipulation.css` with full styles for all manipulation/apispec components

## Phase 18.5 — Frontend Design & Usability Overhaul

**Goal:** Modernize UI with cohesive typography, colors, icons, and interactions for improved usability and visual hierarchy.

- **Typography** — Space Grotesk (primary) + IBM Plex Sans (fallback) from Google Fonts; replaces generic system font stack
- **Accent colour** — Changed from generic blue `#4e7aff` → teal `#00c9b1` across both dark and light themes, including focus rings and split-pane divider
- **Logo** — Placeholder letter 'I' replaced with radar/signal SVG icon (two concentric arcs + dot) thematically fitting for IoT traffic interception
- **Header** — Start/Stop buttons now solid filled (green/red with white text) with subtle depth shadow; 'CA' renamed to 'Root CA'; Sign Out becomes icon-only button with door/arrow SVG
- **Capture rows** — min-height 36 → 40 px; alternating row stripes; 4xx/5xx left-border color coding; real-time flash: 1.4s teal glow animation on new captures via SignalR
- **Badges** — Padding increased 1px → 2px 5px; DELETE badge now solid red + white text; added letter-spacing for readability
- **Empty states** — Emoji icons replaced with inline SVGs (magnifying glass in capture list; document outline in detail pane); detail placeholder now vertical with icon above text
- **Radius tokens** — `sm` 4 → 5 px, `lg` 8 → 10 px for softer feel
- **Scrollbars** — 6 → 4 px wide, transparent track, semi-transparent thumb for less visual dominance
- **Focus rings** — Global `:focus-visible` with teal outline replaces inconsistent border-color approach on individual inputs

## Phase 19 — Bugfixes, UI Polish, iOS TLS Compatibility & Proxy Auto-start

**Goal:** Fix critical bugs (timeline crash, enum serialization), improve UI polish, resolve iOS/macOS TLS certificate rejection, enable proxy auto-start on launch.

- **Timeline crash fix** — `InterceptionProtocol` serialized as integers over SignalR (missing `JsonStringEnumConverter` on `AddJsonProtocol`). Fixed by adding `JsonStringEnumConverter` to both controllers and SignalR, defensive guard in `getStatusClass`, and `ErrorBoundary` component wrapping each view panel
- **Timeline device labels** — Label column now resizable via drag handle (100–320 px range); name/IP stacks vertically with ellipsis truncation
- **Settings modal** — Proxy mode selector now enabled for all three modes with conditional extra fields; added "Relaunch Welcome Guide" button and **Auto-start proxy** checkbox
- **Capture list timestamp** — Timestamp column widened 56 px → 76 px to prevent clipping of locale-aware time strings
- **iOS/macOS TLS compatibility** — `CertificateAuthority` now uses keyid-only AKI form; leaf cert validity capped at 397 days (Apple enforces ≤ 398-day limit); IP SANs use `GeneralName.IPAddress`
- **EF Core migration quirks** — `AddBodyCaptureDefaults` uses `Sql()` instead of `AlterColumn` for SQLite compatibility; stub `.Designer.cs` added for EF Core migration discovery
- **`AddProxyAutoStart` migration** — Adds `AutoStart BOOLEAN DEFAULT 0` column to `ProxySettings`; `ProxyService` auto-starts on launch when `AutoStart=true`
- **Rich body viewer** — New `BodyViewer` component with three view modes: Pretty (content-type-aware JSON/XML/HTML with syntax highlighting), Raw (plain text), Hex (Wireshark-style offset/hex/ASCII dump, 16 bytes per row, capped at 8 KiB)
- **Body viewer features** — Info toolbar shows Content-Type, Content-Encoding (e.g. `gzip ✓ decoded`), byte size; copy-to-clipboard button; unknown content types sniff JSON; images rendered as `<img>` via Blob URL
- **Backend decompression** — Both proxy servers now decompress gzip/deflate/Brotli response bodies before DB write (original compressed bytes forwarded to client), making IoT JSON/HTML responses readable in UI
- **Documentation updates** — README, AGENT.md, CLAUDE.md, PLAN.md, ARCHITECTURE.md updated with Linux `setcap` procedure, JSON enum serialization requirement, iOS TLS cert requirements, EF Core SQLite migration quirks

## Phase 20 — Admin UI & Body Viewer Stream Rendering

**Goal:** Complete operational admin page with database maintenance, user management, and support for SSE/NDJSON stream rendering in body viewer.

- **Admin dashboard** — `/admin` route with four management tabs (Database, Certificates, Audit, Users)
- **Database tab** — Row count and size stats; date-range and hostname-filter purge controls; JSON/CSV export of captures/packets; purge-all confirmation guard
- **Certificates tab** — Root CA metadata display; DER/PEM download links; regenerate root CA endpoint with audit logging; purge leaf certs
- **Audit tab** — Paginated table of all audit entries (timestamp, user, action, entity, details, source IP)
- **Users tab** — User list with inline role selector; create user dialog; delete with confirmation guards (blocks self-delete and last-admin demotion/deletion)
- **Admin controller** — `POST /api/admin/purge`, `GET /api/admin/export`, `POST /api/certificates/root-ca/regenerate`; admin-role gated; all destructive operations audit-logged
- **Stream rendering** — `detectStream()` identifies SSE (`text/event-stream`) and NDJSON (`application/x-ndjson`, `application/jsonl`); `StreamEventRow` component renders collapsible per-event rows with chevron, index, label, byte count, optional SSE metadata; expand/collapse-all toggle
- **Integration tests** — `AdminControllerTests` (10 tests), `CertificatesControllerTests` (2 tests), `UserSafetyGuardsTests` (3 tests)

## Phase 16 — Deployment & Operations

**Goal:** Make IoTSpy production-ready for team deployments with proper TLS, container orchestration, observability, alerting, and package distribution.

- **Kestrel HTTPS (16.1)** — `HttpsCertificateHolder` singleton + `CertesLetsEncryptService`; certificate file or Let's Encrypt via `Certes`; HTTPS on port 5001
- **Kubernetes Helm chart (16.2)** — `deploy/helm/iotspy/` — Chart.yaml, values.yaml, Deployment, Service, ConfigMap, Secret, Ingress, PVC, HPA, ServiceAccount
- **Docker Compose improvements (16.3)** — `docker-compose.prod.yml` with Postgres 17, pgAdmin, Traefik v3 reverse proxy with automatic TLS
- **Plugin system for protocol decoders (16.4)** — `IPluginDecoder` + `IPluginRegistry` in Core; `PluginLoaderService` via `AssemblyLoadContext`; `PluginsController` REST API
- **Metrics endpoint (16.6)** — `/metrics` via `prometheus-net.AspNetCore`; `IoTSpyMetrics` with proxy requests, scan durations, anomaly alerts, capture queue depth
- **Alerting integrations (16.7)** — Slack (blocks API), Teams (MessageCard), PagerDuty Events API v2; severity threshold filtering
- **NAS APK package support (16.9)** — `docker-compose.nas.yml`; `deploy/nas/asustor/` APK (apkg.info, lifecycle scripts, webman CGI); `scripts/build-asustor-apk.sh`; multi-arch `release.yml` CI publishing to GHCR
- **Deprioritized within Phase 16** — LDAP/SAML SSO (16.5) and distributed multi-node mode (16.8) remain out of scope; see [GAPS.md](GAPS.md)

## Phase 21 — Passive Proxy Mode

**Goal:** Enable lightweight traffic monitoring with optional persistence, supporting API discovery, compliance auditing, and low-resource deployments — without interception, manipulation, or DB write overhead.

- **`ProxyMode.Passive` (21.1)** — New enum value; proxy uses the explicit listener stack as transport but skips all rules, scripts, anomaly detection, and DB inserts. Switchable via settings modal ("Passive (observe-only)") or `PUT /api/proxy/settings`
- **Pass-through pipeline (21.2)** — `ExplicitProxyServer.InterceptHttpStreamAsync` detects `settings.Mode == Passive` and takes a fast path: forwards request/response bytes unchanged, publishes to `IPassiveProxyBuffer` and SignalR, and returns — no manipulation service calls
- **`PassiveProxyBuffer` singleton (21.3)** — Thread-safe in-memory ring buffer (10k cap; oldest evicted when full). Implements optional **device IP filter**: `SetDeviceFilter(ips)` restricts recording to traffic from specific client IPs, enabling targeted observation of individual devices or groups without recording all traffic
- **Session save/load (21.4)** — `POST /api/passive/sessions` snapshots the in-memory buffer to the database as a named `PassiveCaptureSession`. Optionally filtered by device IP (saves only matching entries). Optional `clearBufferAfterSave` flag. `GET /api/passive/sessions/{id}/captures` reloads the persisted captures
- **No DB writes in passive mode (21.5)** — `ICaptureRepository` is never resolved in passive sessions; the `PassiveCaptureSessionId` FK on `CapturedRequest` is populated only on explicit save. Migration `AddPhase21PassiveProxy` adds `PassiveCaptureSessions` table and nullable `PassiveCaptureSessionId` column
- **API discovery panel (21.6)** — New "Passive Capture" view mode in the dashboard with auto-refreshing (5s) endpoint frequency heatmap (method + host + path + bar chart + count), status code distribution with color-coded badges (2xx/3xx/4xx/5xx), top 10 hosts list, total buffered count, and a saved sessions table
- **Passive mode UI indicator (21.7)** — "Passive Mode" accent badge in the header when the proxy is running in passive mode; distinguishes from active interception at a glance
- **Device filter controls (21.8)** — Filter bar in the Passive Capture panel: enter comma-separated IP addresses to restrict the live buffer; empty = capture all devices. `PUT /api/passive/filter` / `DELETE /api/passive/filter` API endpoints. Save-session dialog respects the active filter
- **Tests (21.9)** — 10 new `PassiveProxyBufferTests`: ring buffer eviction, device filter accept/reject (single and multi-IP), filter clear, snapshot isolation, summary counts, and active filter exposure in summary response

## Phase 22 — Rich Media & Stream Content Replacement

**Goal:** Fix silent corruption when replacing binary content (images, video, audio) through the MITM proxy and add first-class support for SSE stream mocking, HTTP Range passthrough, tracking pixels, and an in-UI rule preview. Primary use case: replace advertising images, tracking pixels, video ads, and live ad-feed streams with researcher-controlled content.

- **Body-source abstraction (22.1, 22.8)** — New `IResponseBodySource` interface on `IoTSpy.Core`; `HttpMessage.ResponseBodySource` opt-in channel. When set, the proxy writer bypasses the string body and the UTF-8 round-trip that previously turned binary payloads into base64 text on the wire. `FileStreamBodySource` streams any size file directly from disk (unified path, no 4 MB threshold)
- **Proxy writer refactor (22.2)** — `WriteHttpHeadAsync` + `ApplyBodySourceHeaders` helpers in `ExplicitProxyServer`; stripped `Transfer-Encoding` + `Content-Encoding`, overwritten `Content-Type` + `Content-Length`, appended body-source `ExtraHeaders`
- **HTTP Range passthrough (22.3)** — `RangeHelper.TryParse` handles `bytes=S-E`, `bytes=S-`, `bytes=-N` (multi-range rejected); `RangeSlicedBodySource` serves 206 with `Content-Range` + `Accept-Ranges: bytes`, required by `<video>` elements
- **SSE stream mocking (22.4)** — `ContentReplacementAction.MockSseStream` + `SseStreamBodySource` replays `.sse` / `.ndjson` files with per-event flush, configurable inter-event delay, and optional loop-until-disconnect; EF migration `AddPhase22SseReplayConfig` added nullable `SseInterEventDelayMs` + `SseLoop` columns to `ContentReplacementRules`
- **Tracking pixel (22.5)** — `ContentReplacementAction.TrackingPixel` + `TrackingPixelBodySource` singleton emits a 43-byte 1×1 transparent GIF89a with no asset upload required
- **Asset library UI (22.6)** — New `AssetLibrary.tsx` with native HTML5 drag-drop + MIME allowlist + multi-file upload + media-kind badges + inline image/video/audio thumbnails; `ApiSpecPanel` gains an Assets toggle; `ReplacementRulesEditor` gets Pick-Asset modal + SSE delay/loop inputs
- **Rule preview endpoint + modal (22.7)** — `ReplacementPreviewService` applies a single rule against a synthetic or captured exchange in-memory (1 MB + 2 s cap) and returns modified status/headers/body + warnings; `POST /api/apispec/{specId}/rules/{ruleId}/preview`; `RulePreviewModal.tsx` wires it to the rule table. New `[AllowAnonymous] GET /api/apispec/assets/{filename}/content` serves assets with `PhysicalFile(enableRangeProcessing: true)` so `<img>` / `<video>` tags can load previews without the JWT header
- **Correction** — Content Replacement Rule CRUD lives on `ApiSpecController` (`/api/apispec/{specId}/rules`), not `ManipulationController` as the original roadmap implied; the new preview endpoint follows that routing. `ExtractContentType` / `UpdateContentType` in `ContentReplacer` previously only understood JSON-dict headers, silently missing real proxy CRLF headers — now handles both formats, so ContentType-match rules finally fire on live traffic
- **Tests (22.9)** — 34 new backend tests (`RangeHelperTests`, `BodySourcesTests`, `SseStreamBodySourceTests`, `ContentReplacerBinaryTests`, `ReplacementPreviewServiceTests`) incl. PNG round-trip regression proving base64-corruption is fixed; 2 new Vitest tests for `AssetLibrary`. All 608 backend + 13 frontend tests pass
