# IoTSpy — Implementation Plan & Session Handoff

This document is the single source of truth for any new contributor or Claude Code session to resume work. It covers what has been built, what remains, and how to pick up where the last session left off.

---

## Project context

IoTSpy is an IoT network security research platform. The goal is a self-hosted tool that can be pointed at any IoT device to capture, inspect, and manipulate its network traffic, then run automated pen-test checks against the device.

Full architecture: [`docs/architecture.md`](architecture.md)
README / quick start: [`README.md`](../README.md)

---

## Current statistics

| Metric | Value |
|---|---|
| Backend projects | 7 (Core, Proxy, Protocols, Scanner, Manipulation, Storage, Api) |
| Test projects | 8 (Core.Tests, Protocols.Tests, Manipulation.Tests, Scanner.Tests, Api.Tests, Proxy.Tests, Storage.Tests, Api.IntegrationTests) |
| Backend tests | 350+ (all passing) — includes Phase 10 decoders + Phase 11 multi-user/TLS/tests + Phase 12 API spec generation + Phase 20 admin/integration tests |
| Frontend tests | 11+ component tests via Vitest + React Testing Library |
| REST controllers | 15 (Auth, Proxy, Captures, Devices, Certificates, Scanner, Manipulation, PacketCapture, OpenRtb, ProtocolProxy, Report, ScheduledScan, Dashboard, ApiSpec, Admin) |
| HTTP endpoints | 100+ |
| SignalR hubs | 2 (TrafficHub, PacketCaptureHub) — TrafficHub extended with WebSocket frame + MQTT message + anomaly alert subscriptions |
| EF Core migrations | 13 (InitialCreate → AddProxyAutoStart) |
| Frontend components | 65+ TypeScript files across 16+ component directories |
| Protocols supported | HTTP/HTTPS, TLS passthrough (JA3/JA3S), WebSocket, MQTT 3.1.1/5.0 (passive decode + active proxy), DNS/mDNS, CoAP (passive decode + active proxy), gRPC/Protobuf, Modbus TCP, OpenRTB 2.5, Datadog, Firehose, Splunk HEC, Azure Monitor |
| Frontend design | Space Grotesk + IBM Plex Sans typography; teal (`#00c9b1`) accent color; radar SVG logo; responsive design (480px, 768px, 1024px breakpoints); dark/light theme toggle |
| Admin features | Database stats/purge, certificate management, audit log, user management, root CA regeneration (admin-only) |
| Body viewer | Pretty/Raw/Hex view modes; content-type-aware rendering (JSON syntax highlight, XML/HTML format, image preview); stream support (SSE/NDJSON with collapsible rows); automatic decompression (gzip/deflate/Brotli) |
| CI | GitHub Actions (`.github/workflows/ci.yml`) — build, test, lint, coverage on push/PR |

---

## What has been built

All phases 1–20 are complete. This includes the foundation (proxy/API/dashboard), all major features (protocols, scanning, manipulation, capture analysis), UX polish, admin operations, and modern frontend design.

### Phase 1 — Foundation

Proxy server + REST API + React dashboard scaffold. Explicit proxy (HTTP + HTTPS CONNECT), TLS MITM with BouncyCastle CA, Polly 8 resilience, JWT auth, SignalR live streaming, EF Core (SQLite/Postgres), Docker support.

### Phase 2 — Interception modes + protocols

MQTT 3.1.1/5.0 and DNS/mDNS decoders. Gateway redirect mode (iptables). ARP spoof mode (SharpPcap). SignalR filter subscriptions. Timeline swimlane view.

### Phase 3 — Pen-test suite

TCP port scan, service fingerprinting (banner grab + CPE), default credential testing (FTP/Telnet/MQTT), CVE lookup (OSV.dev), config audit (Telnet/UPnP/anon MQTT/exposed DB/HTTP admin). `ScannerController` + frontend.

### Phase 4 — Active manipulation

Declarative rules engine (regex match → modify/drop/delay). C# (Roslyn) and JavaScript (Jint) scripted breakpoints. Request replay. Mutation fuzzer (Random/Boundary/BitFlip). `ManipulationController` + frontend.

### Phase 5 — AI mock + advanced protocols

AI mock engine with pluggable providers (Claude/OpenAI/Ollama). CoAP UDP decoder. Telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor). Statistical anomaly detection (Welford online algorithm).

### Phase 6 — Packet capture & analysis

SharpPcap live capture with ring buffer (10k). Protocol distribution, communication patterns, suspicious activity detection (port scan, ARP spoof, DNS anomaly, retransmission burst). Hex dump + freeze frame. PCAP export. `PacketCaptureController` (14 endpoints) + `PacketCaptureHub` SignalR.

### OpenRTB inspection

OpenRTB 2.5 bid request/response decoding. PII detection + policy-based redaction. Inline detection in both proxy servers. `OpenRtbController` + frontend (traffic list, inspector, PII policy editor, audit log).

### Phase 7 — Test coverage & CI/CD

7 test projects, 248+ backend tests. Controller unit tests (NSubstitute), proxy service tests, repository integration tests (EF Core SQLite in-memory), `WebApplicationFactory` integration tests. Frontend: Vitest + React Testing Library (11 tests). GitHub Actions CI with Coverlet coverage.

### Phase 8 — Observability & production hardening

Health checks (`/health`, `/ready`). Serilog structured logging (console + rolling file, 7-day retention). Rate limiting (sliding window, 100/60s). `DataRetentionService` (configurable TTLs, disabled by default). `AnomalyDetector` wired into proxy pipeline with SignalR alerts. Graceful shutdown (connection draining). DB connection pooling.

### Phase 9 — Export & reporting

HTML/PDF scan report generation (`ReportController`). Capture export (CSV/JSON/HAR). Filtered PCAP export. Alerting (webhook/email via `IAlertingService`). Scheduled scans with cron expressions (`ScheduledScanController`).

### Phase 10 — Protocol expansion & active proxying

WebSocket interception (bidirectional frame relay + capture). MQTT broker proxy (TCP MITM, topic-level wildcard filtering, SignalR message publishing). CoAP UDP forward proxy (message decoding, device registration, capture). gRPC/Protobuf decoder (LPM framing + schema-less field extraction). Modbus TCP decoder (MBAP, function codes 1-16). `ProtocolProxyController` (6 endpoints).

### TLS Passthrough & SSL Stripping

**Goal:** HTTPS visibility for IoT devices where CA installation is not possible.

- `TlsClientHelloParser` — SNI extraction, cipher suite enumeration, JA3 fingerprint with GREASE filtering (RFC 8701)
- `TlsServerHelloParser` — ServerHello parsing (selected cipher, `supported_versions` for TLS 1.3, JA3S), Certificate extraction (TLS 1.2 only — encrypted in TLS 1.3)
- `HandleTlsPassthroughAsync` — in both `ExplicitProxyServer` and `TransparentProxyServer`: buffer ClientHello, parse SNI/JA3, relay, parse ServerHello/JA3S + Certificate, count bytes, record `CapturedRequest` with `Protocol=TlsPassthrough` and `TlsMetadataJson`
- `SslStripService` — intercept HTTP→HTTPS redirects, follow upstream TLS, strip HSTS headers, rewrite `https://` links in Location/Set-Cookie/CSP headers and HTML/JSON bodies
- `DnsCorrelationKey={ClientIp}→{SniHostname}` structured logging on all passthrough events for DNS-to-TLS correlation
- `AddTlsPassthroughAndSslStrip` migration — `TlsMetadataJson` on Captures, `SslStrip` on ProxySettings

### Phase 11 — UX, multi-user & technical debt

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

### API Spec Generation & Content-Aware Mocking

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

### Admin UI & Body Viewer

**Goal:** Operational admin page for database maintenance, certificate management, audit log review, and user management; plus SSE/NDJSON stream rendering in the body viewer.

- **`AdminController`** — admin-role gated at `/api/admin`; stats (row counts + estimated sizes), purge endpoints (captures by age/host/purgeAll, packets by age/purgeAll), export endpoints (captures + packets as JSON/CSV, full config as JSON); writes audit log entries on all destructive operations
- **`CertificatesController` regenerate endpoint** — `POST /api/certificates/root-ca/regenerate` (admin-only); purges all leaf and root certs, recreates the root CA, writes audit log entry
- **`AuthController` safety guards** — `DeleteUser` now blocks self-deletion and deletion of the last admin; `UpdateUser` now blocks demoting the last admin
- **Admin frontend** — `/admin` route (redirects non-admins to `/`); wrench icon in `Header` shown only to users with `role === 'admin'`; four tabs:
  - *Database* — stat cards with row counts/sizes/oldest entry; range-slider purge by age; host-filter purge; purge-all with confirm dialog; JSON/CSV export via authenticated download
  - *Certificates* — root CA metadata table; DER/PEM download links; regenerate-CA and purge-leaf-certs with confirm dialogs
  - *Audit Log* — paginated table of all audit entries (timestamp, user, action, entity, details, IP)
  - *Users* — user table with inline role select; create-user dialog; delete with confirm guard; self-delete hidden
- **`useCurrentUser()` hook** — reads `iotspy-user` from localStorage; populated on login, cleared on logout; used to gate the admin link and `/admin` route
- **Body viewer stream rendering** — `detectStream()` identifies SSE (`text/event-stream`) and NDJSON (`application/x-ndjson`, `application/jsonl`, or sniffed multi-line JSON); `parseSSE()` / `parseNDJSON()` produce `StreamEvent[]`; `StreamEventRow` component: collapsible per-event row with chevron, index, label, byte count, optional SSE metadata, and JSON syntax-highlighted or plain body; event count badge in toolbar; expand/collapse-all toggle
- **Tests added** — `AdminControllerTests` (10 tests), `CertificatesControllerTests` (2 tests), `UserSafetyGuardsTests` (3 tests)

### Phase 12 — API Spec Generation & Content-Aware Mocking

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

### Phase 18 — React Frontend Performance & Correctness

**Goal:** Fix React best-practices issues identified in code audit; improve rendering performance and eliminate memory leaks.

- **Duplicate hook removal** — Removed duplicate `useCaptures` from `ManipulationPanel`; now accepts captures as prop from `DashboardPage` so SignalR live-updates are not missed
- **Dependency array fixes** — Added `analysis` to `useEffect` dependency array in `PanelPacketCapture` (stale closure fix)
- **Callback optimization** — Removed needless arrow-function wrappers around stable callbacks in `OpenRtbPanel`; pass `rtb.refreshEvents` directly to prevent defeating `React.memo`
- **API client consolidation** — Replaced raw `fetch` calls with shared `apiFetch` client in `usePacketCapture` for consistent auth/error handling
- **Component memoization** — Wrapped `CaptureRow` in `React.memo` to short-circuit re-renders on unchanged rows during SignalR events
- **Key stabilization** — Replaced array index keys with `tick.position` in `TimelineSwimlaneView` to avoid reconciliation artifacts on zoom
- **Constant hoisting** — Moved `statusBadge` colors map to module scope in `ApiSpecPanel`
- **Styling** — Added comprehensive `manipulation.css` with full styles for all manipulation/apispec components

### Phase 18 — Frontend Design & Usability Overhaul

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

### Phase 19 — Bugfixes, UI Polish, iOS TLS Compatibility & Proxy Auto-start

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
- **Documentation updates** — README, AGENT.md, CLAUDE.md, PLAN.md, architecture.md updated with Linux `setcap` procedure, JSON enum serialization requirement, iOS TLS cert requirements, EF Core SQLite migration quirks

### Phase 20 — Admin UI & Body Viewer Stream Rendering

**Goal:** Complete operational admin page with database maintenance, user management, and support for SSE/NDJSON stream rendering in body viewer.

- **Admin dashboard** — `/admin` route with four management tabs (Database, Certificates, Audit, Users)
- **Database tab** — Row count and size stats; date-range and hostname-filter purge controls; JSON/CSV export of captures/packets; purge-all confirmation guard
- **Certificates tab** — Root CA metadata display; DER/PEM download links; regenerate root CA endpoint with audit logging; purge leaf certs
- **Audit tab** — Paginated table of all audit entries (timestamp, user, action, entity, details, source IP)
- **Users tab** — User list with inline role selector; create user dialog; delete with confirmation guards (blocks self-delete and last-admin demotion/deletion)
- **Admin controller** — `POST /api/admin/purge`, `GET /api/admin/export`, `POST /api/certificates/root-ca/regenerate`; admin-role gated; all destructive operations audit-logged
- **Stream rendering** — `detectStream()` identifies SSE (`text/event-stream`) and NDJSON (`application/x-ndjson`, `application/jsonl`); `StreamEventRow` component renders collapsible per-event rows with chevron, index, label, byte count, optional SSE metadata; expand/collapse-all toggle
- **Integration tests** — `AdminControllerTests` (10 tests), `CertificatesControllerTests` (2 tests), `UserSafetyGuardsTests` (3 tests)

---

### Post-Phase-11 Stabilization — Bugfixes & Polish

#### Timeline tab crash & enum serialization fix

- **Root cause:** `Program.cs` had `JsonStringEnumConverter` on `AddControllers()` but *not* on `AddSignalR().AddJsonProtocol()`. Live-streamed captures came through with numeric protocol values (`0`, `1`, …); `getStatusClass` called `.toLowerCase()` on a number, throwing `TypeError`. No `ErrorBoundary` existed, so React unmounted the entire app tree.
- **Fixes:**
  - Added `JsonStringEnumConverter` to `AddSignalR().AddJsonProtocol()` in `Program.cs`
  - Added defensive `typeof protocol === 'string'` guard in `TimelineSwimlaneView.tsx`
  - Created `frontend/src/components/common/ErrorBoundary.tsx` (class component, `getDerivedStateFromError`) and wrapped each view panel in `DashboardPage.tsx`

#### Timeline device label panel — resizable column

- Labels column was fixed-width and overflowed long device names/IPs
- Added `labelsWidth` state + mouse-drag resize handle (100–320 px range) in `TimelineSwimlaneView.tsx`
- Updated `timeline.css`: `.timeline-label-row` stacks name/IP vertically with `text-overflow: ellipsis`; `.timeline-labels-resize` drag handle at right edge

#### Settings modal improvements

- **Proxy mode selector** was `disabled` — removed the `disabled` attribute so all three modes (Explicit, GatewayRedirect, ArpSpoof) are selectable; added conditional extra fields for each mode
- **"Relaunch Welcome Guide"** button added to settings modal (`showWizard` state renders `<OnboardingWizard>` inline)
- **Auto-start proxy** — new `AutoStart` boolean on `ProxySettings`; exposed in settings UI; `ProxyService.StartAsync` auto-starts when `AutoStart=true` on server launch

#### Capture list timestamp clip

- Timestamp column grid width widened from `56px` → `76px` in `capture-list.css` to prevent locale-aware time strings (e.g. `"12:34:56 PM"`) being clipped

#### EF Core migration — `AddProxyAutoStart`

- Migration `20260326120000_AddProxyAutoStart` adds `AutoStart BOOLEAN DEFAULT 0` column to `ProxySettings`
- Includes stub `.Designer.cs` file (required for EF Core migration discovery)
- `IoTSpyDbContextModelSnapshot.cs` updated with `AutoStart` property

#### Linux packet capture — setcap procedure

- SharpPcap requires `CAP_NET_RAW` + `CAP_NET_ADMIN`. `setcap` **rejects symlinks** (`/usr/bin/dotnet`); must target the real binary:
  ```bash
  sudo setcap cap_net_raw,cap_net_admin+eip "$(readlink -f $(which dotnet))"
  # typically resolves to: /usr/share/dotnet/dotnet
  ```
- Documented in README.md and AGENT.md under "Known operational requirements"

#### Apple iOS/macOS TLS certificate compatibility

- iOS 16+ / iOS 26 rejects full-form Authority Key Identifier (keyId + DirName + serial)
- `CertificateAuthority` now uses keyid-only AKI via `SubjectPublicKeyInfoFactory`
- Leaf cert validity capped at 397 days (Apple enforces ≤ 398-day limit)
- IP address SANs use `GeneralName.IPAddress` (not `DnsName`)
- Documented in AGENT.md under "Known operational requirements"

---

## Remaining gaps and technical debt

Items that are still open. These inform the roadmap.

| Gap | Description | Severity |
|---|---|---|
| No HTTPS for the API itself | The API serves on plain HTTP; TLS termination is assumed external | Low |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low |

**Resolved in Phase 11:**
- ~~No Core model tests~~ — `IoTSpy.Core.Tests` project added with 30+ model default/enum tests
- ~~No multi-user support~~ — Multi-user RBAC with `User` model, `UserRole` enum (Admin/Operator/Viewer), user management endpoints
- ~~Dashboard not responsive~~ — Responsive CSS with mobile breakpoints (480px, 768px, 1024px)
- ~~TLS passthrough/SSL strip untested~~ — `TlsClientHelloParserTests` (13 tests), `TlsServerHelloParserTests` (11 tests), `SslStripServiceTests` (14 tests)

---

## Roadmap — what comes next (Phases 21+)

Future enhancement phases are planned in this section as the project evolves. Some candidate areas for future work include:

- **Threat Intelligence** — Shodan/VirusTotal integration, local CVE database caching, MISP threat feed ingestion, passive OS fingerprinting, DGA domain detection
- **PCAP Analysis** — PCAP/pcapng file upload and import, session reconstruction (TCP stream reassembly), diff view for imported vs. live captures
- **API Key Management** — Service account API keys with scope-based access control, key rotation and revocation, API key frontend management
- **Team Collaboration** — Shared investigation sessions, in-session annotations, presence indicators, activity feeds, session export
- **Deployment** — Kestrel HTTPS for API, Kubernetes Helm chart, Docker Compose production setup, plugin system for custom decoders
- **Protocol Expansion** — AMQP 1.0, RTSP/RTP, Matter/Thread, Zigbee, Bluetooth LE, Z-Wave support
- **Operations & Metrics** — LDAP/SAML SSO, Prometheus metrics endpoint, Slack/Teams/PagerDuty alerting, distributed multi-node deployment

Phase scoping and prioritization for future work will be determined based on user needs and feedback.

---

## Key design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Proxy modes | Three (explicit, gateway, ARP) all feed one capture pipeline | Uniform storage/analysis regardless of interception method |
| Storage | SQLite default, Postgres pluggable | Zero-config for local use; scales with Postgres |
| Auth | Multi-user JWT with RBAC (Admin/Operator/Viewer), PBKDF2 hash in DB | Team tool with role-based access; backward-compatible with legacy single-user mode |
| TLS MITM | BouncyCastle (pure .NET) | No native dependency; works cross-platform |
| TLS passthrough | Custom handshake parser + relay | Extract metadata (JA3/JA3S/SNI/cert) without breaking the connection |
| SSL stripping | HTTP-level intercept + upstream TLS fetch | Visibility into HTTPS traffic for devices that can't install CAs |
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
  "Resilience": { /* timeouts, retry, circuit-breaker — see architecture.md */ },
  "AiMock": { "Provider": "claude|openai|ollama", "Model": "...", "ApiKey": "" },
  "Serilog": { "MinimumLevel": "Information" },
  "RateLimit": { "Enabled": true, "PermitLimit": 100, "WindowSeconds": 60 },
  "DataRetention": {
    "Enabled": false,             // opt-in; set true to enable automatic cleanup
    "CaptureRetentionDays": 30,
    "PacketRetentionDays": 7,
    "ScanJobRetentionDays": 90,
    "OpenRtbEventRetentionDays": 14
  }
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

- `IoTSpy.Proxy` has explicit + transparent proxy servers, TLS MITM CA, TLS passthrough (JA3/JA3S), SSL stripping, MQTT broker proxy, CoAP proxy, WebSocket interception, ARP spoofing, iptables helper, Polly resilience. References `IoTSpy.Protocols` for decoder access.
- `IoTSpy.Protocols` has MQTT, DNS/mDNS, CoAP, WebSocket, gRPC/Protobuf, Modbus TCP, OpenRTB, and four telemetry decoders (Datadog, Firehose, Splunk HEC, Azure Monitor).
- `IoTSpy.Scanner` has port scan, fingerprinting, credential testing, CVE lookup, config audit, and packet capture.
- `IoTSpy.Manipulation` has rules engine, scripted breakpoints (C#/JS), replay, fuzzer, AI mock engine, packet capture analyzer, OpenRTB PII service, API spec generation, content replacement, and LLM-enhanced spec refinement.
- EF Core migrations are in `src/IoTSpy.Storage/Migrations/` — 12 migrations applied: InitialCreate, AddPhase2ProxySettings, AddPhase3Scanner, AddPhase4ManipulationFix, AddOpenRtbInspection, AddPacketCapture, AddMissingPhase7Changes, AddPhase9ScheduledScans, AddBodyCaptureDefaults, AddTlsPassthroughAndSslStrip, AddPhase11MultiUserAndAudit, AddApiSpecAndContentReplacement. Run `dotnet ef migrations add <Name> --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` from the repo root.
- **Multi-user RBAC** — `User` model with `UserRole` enum (Admin/Operator/Viewer); `IUserRepository`; JWT claims include `sub` (user ID) + `role`; admin-only user CRUD in `AuthController`; backward-compatible with legacy single-user auth (falls back to `ProxySettings.PasswordHash` when no `User` record matches).
- **Audit log** — `AuditEntry` model + `IAuditRepository`; tracks login, user CRUD; admin-only GET `/api/auth/audit`.
- **Dashboard customization** — `DashboardLayout` model + `IDashboardLayoutRepository`; per-user saved layouts with JSON config; `DashboardController` CRUD.
- `DateTimeOffset` properties are stored as Unix milliseconds (`long`) via a `ValueConverter` in `IoTSpyDbContext` — required for SQLite `ORDER BY` compatibility.
- `AnomalyDetector` is wired into both proxy servers. After each captured request, `IAnomalyDetector.Record()` is called; alerts published via `IAnomalyAlertPublisher` → `SignalRAnomalyPublisher` → `TrafficHub` group `"anomaly-alerts"`.
- `DataRetentionService` runs as a background `IHostedService`. Disabled by default (`DataRetention:Enabled: false`).
- TLS passthrough records `CapturedRequest` entries with `Protocol=TlsPassthrough` and `TlsMetadataJson` (serialized `TlsMetadata` model). All passthrough log entries include `DnsCorrelationKey={ClientIp}→{SniHostname}` for DNS packet capture correlation.
- SSL stripping is enabled via `ProxySettings.SslStrip=true`. It only applies to plain HTTP connections — intercepts HTTPS redirects, follows upstream TLS, strips HSTS, rewrites `https://` links.
