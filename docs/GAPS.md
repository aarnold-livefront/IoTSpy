# IoTSpy — Known Gaps & Technical Debt

This document tracks remaining gaps, known limitations, and technical debt. Items are prioritized by severity and implementation effort. See [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for larger planned features.

---

## Active Gaps

| Gap | Description | Severity | Status | Notes |
|---|---|---|---|---|
| No LDAP / SAML SSO | Enterprise single sign-on not implemented | Low | Open | Deprioritized in Phase 16.5; valid candidate for future work |
| No distributed / multi-node mode | Single-instance proxy per deployment; horizontal scaling requires Redis backplane | Low | Open | Deprioritized in Phase 16.8; see Design Assumptions |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low | Open | See Phase 17 for future work |

---

## API Completeness Gaps

All previously open filtering gaps have been resolved — see Resolved Items below.

---

## Security Hardening

### Input Validation — Remaining Items

`FluentValidation.AspNetCore` is registered; validators exist for `CreateRuleDto`, `UpdateRuleDto`, `CreateBreakpointDto`, `StartScanDto` (port range, concurrency, timeout). All previously open items resolved — see Resolved Items below.

---

## Testing Gaps

| Component | Current Status | Gap |
|---|---|---|
| Frontend components | 7 spec files (~36 tests) | Most panels still untested; Manipulation/PacketCapture/Sessions panels now covered |
| End-to-end frontend | Auth + Captures + Dashboard + Manipulation specs (Playwright) | Load testing, security fuzzing |
| Load testing | None | Proxy throughput/latency baseline |
| Security fuzzing | Limited | AFL/libFuzzer on MQTT, DNS, CoAP parsers |

---

## Protocol Decoder Depth

Decoders exist for all major protocols but vary in depth:

| Protocol | Current depth | Enhancement opportunity |
|---|---|---|
| DNS | Basic query/response, label decompression | DNSSEC validation chain, EDNS0 options, DoH/DoT detection |
| CoAP | RFC 7252 message decode | Resource discovery (`.well-known/core`), Block-wise transfer, Observe option |
| gRPC | Schema-less LPM field extraction | `.proto` file upload for field name resolution; gRPC-Web detection |
| MQTT | Full 3.1.1/5.0 decode | Topic statistics, retained message tracking, QoS flow analysis |
| WebSocket | Frame decode | Message sequence reconstruction, sub-protocol detection (STOMP, WAMP) |

---

## Performance Considerations

### Known Hotspots

1. **Rule evaluation** — Regex caching with `ConcurrentDictionary<(Pattern, RegexOptions), Regex>` and `RegexOptions.Compiled` is in place in `RulesEngine`; the remaining gap is a compiled-rule list cache per pipeline invocation to avoid re-querying the database on every request.
2. **JSON schema inference** — Recursive object traversal on large payloads; cache schema per `(host, path, method)` tuple
3. **Packet capture ring buffer** — Configurable via `PacketCapture:RingBufferCapacity` in `appsettings.json` (default 10 000); large capture bursts may still drop packets if capacity is not tuned.

### Optimization Opportunities

- Request deduplication in proxy pipeline (same URL + headers within N ms → single capture)
- Rule caching layer with invalidation on CRUD operations (already noted in `ApiSpecMockService`)
- Bloom filter pre-screen before full rule evaluation

---

## Design Assumptions

These assumptions should be revisited if requirements change:

1. **Single-instance proxy per deployment** — Assumes one API process and one pair of proxy servers (explicit + transparent). Horizontal scaling requires Redis backplane (Phase 16.8).
2. **SQLite for development, Postgres for production** — Current schema and migrations support both, but testing is primary on SQLite.
3. **Browser-based dashboard** — Assumes web client; no native mobile apps or CLI tools planned.
4. **JWT + API key auth only** — No SAML/LDAP support (Phase 16.5 deprioritized; see Active Gaps).
5. **In-memory anomaly detector** — Resets on restart; no persistent baseline learning.
6. **Request-scoped repositories** — Each HTTP request gets a fresh EF Core DbContext; not suitable for long-running background tasks without scope management.

---

## Documentation Gaps

| Area | Status | Notes |
|---|---|---|
| Operator runbook | Basic | Need: troubleshooting guide, log interpretation, health check procedures |
| Deployment guides | Minimal | Docker Compose (dev) + Helm chart exist; need: bare-metal, systemd, production hardening guide |
| Performance tuning | None | Connection pool sizing, rate limit tuning, ring buffer sizing, cache strategies |
| Extension guides | Partial | Protocol decoders, AI providers covered; custom UI components and plugin development not covered |

---

## Security Audit Notes

- Root CA private key stored in local database (not HSM-protected); acceptable for research tool, not production
- API endpoints require Bearer token or API key; rate limiting enabled by default; dev overrides via `appsettings.Development.json`
- TLS MITM disabled in passthrough mode (`CaptureTls=false`); metadata extraction only
- SSL stripping requires explicit `SslStrip=true` flag; off by default
- Audit log immutable in design but not enforced at DB level; could add write-once table constraint
- Breakpoint scripts execute arbitrary C# (Roslyn) and JavaScript (Jint) — no sandbox restrictions; operator-role required but worth documenting explicitly

---

## Suggestions for Next Contributors

1. **Add frontend component tests** — manipulation, capture, and sessions panels have no spec coverage

See [AGENT-NOTES.md](AGENT-NOTES.md) for session setup and testing instructions.

---

## Resolved Items

### Gaps Batch 4 (2026-05-04)
- ~~Full-text search in request/response headers~~ — `?headerQ=` query param added to `GET /api/captures`; `CaptureFilter.HeaderSearch` field added; `CaptureRepository.ApplyFilter` searches `RequestHeaders` and `ResponseHeaders` columns; 3 new repository tests
- ~~Rule regex caching~~ — Already implemented: `RulesEngine` has `ConcurrentDictionary<(Pattern, RegexOptions), Regex>` with `RegexOptions.Compiled`; closed as already resolved
- ~~Ring buffer size hardcoded~~ — `PacketCapture:RingBufferCapacity` setting added to `appsettings.json` (default 10 000); `ScannerExtensions.AddIoTSpyScanner(IConfiguration?)` reads the value and passes it to `LockFreePacketRingBuffer`
- ~~Manipulation/Capture/Sessions panels untested~~ — `ManipulationPanel.test.tsx` (8 tests: all 7 tabs + active CSS class), `PanelPacketCapture.test.tsx` (10 tests: tabs, device selector, start/stop, error display), `SessionsPanel.test.tsx` (5 tests: list, create form, validation); frontend test count: 13 → 36
- ~~No Playwright E2E suite~~ — Suite existed in `frontend/tests/` with auth.spec.ts (7 tests), captures.spec.ts, dashboard.spec.ts; added `manipulation.spec.ts` with 3 smoke tests covering tab visibility, rule list rendering, and tab switching

### Address Remaining Gaps (2026-05-04)
- ~~`PacketCaptureController` no tests~~ — `PacketCaptureControllerTests.cs` added with 29 tests covering devices CRUD, start/stop capture, packet filter API, freeze-frame (POST+GET), delete, protocol distribution, communication patterns, suspicious activity, PCAP import (happy path, bad extension, failure), PCAP export (filtered/unfiltered/no-data), and analyzer freeze/unfreeze/status; total backend tests now 712
- ~~IP/hostname field validation~~ — Gap is obsolete: `StartScanDto` was redesigned to accept `Guid DeviceId` instead of a free-text target; the controller derives the IP from the stored device record, so no raw string needs format-validating
- ~~File upload MIME type magic-byte check~~ — Already resolved in Gaps Batch 3 (see below); stale reference removed from active Security Hardening section

### Gaps Batch 3 (2026-05-02)
- ~~Content replacement: binary & SSE~~ — `FileStreamBodySource`, `RangeSlicedBodySource`, `SseStreamBodySource` added in Phase 22; proxy writer uses `IResponseBodySource` to bypass UTF-8 string path; HTTP range slicing for video scrubbing; 15 dedicated tests
- ~~Scanner job filtering not exposed~~ — `GET /api/scanner/jobs?status=&deviceId=&createdAfter=` added; `IScanJobRepository.GetAllAsync` extended with filter params
- ~~Fuzzer job filtering not exposed~~ — `GET /api/manipulation/fuzzer/jobs?status=&captureId=` added; `IFuzzerJobRepository.GetAllAsync` extended with filter params
- ~~File upload MIME magic-byte check~~ — `POST /api/apispec/assets` now reads first 12 bytes; rejects uploads whose magic signature doesn't match the declared/inferred MIME type; returns 415 with a descriptive error

### Gaps Batch 2 (2026-05-02)
- ~~`DELETE /api/manipulation/rules` (bulk)~~ — `DELETE /api/manipulation/rules/bulk` added; body `{ids:[...], all:false}`; `IManipulationRuleRepository.DeleteManyAsync` uses `ExecuteDeleteAsync`
- ~~`DELETE /api/scanner/jobs` (bulk)~~ — `DELETE /api/scanner/jobs/bulk` added; body `{status?, completedBefore?}`; `IScanJobRepository.DeleteByFilterAsync` uses `ExecuteDeleteAsync`
- ~~Session filtering not exposed~~ — `GET /api/sessions?createdByMe=true` filters by `CurrentUserId` via updated `IInvestigationSessionRepository.GetAllAsync(bool, Guid?, CancellationToken)`
- ~~No input validation framework~~ — `FluentValidation.AspNetCore` 11.3.0 added; validators for `CreateRuleDto`, `UpdateRuleDto`, `CreateBreakpointDto`, `StartScanDto` with regex pre-compile check and port range validation
- ~~`ManipulationController` no tests~~ — `ManipulationControllerTests.cs` added with 21 tests covering rules CRUD, bulk ops, breakpoints, fuzzer error paths, AI mock
- ~~Pagination on rules/breakpoints endpoints~~ — Both `GET /api/manipulation/rules` and `GET /api/manipulation/breakpoints` return `{ items, total, page, pageSize, pages }` with `?page=&pageSize=` params
- ~~Session filtering not exposed~~ — Resolved; see above
- ~~Missing keyboard shortcuts~~ — `useKeyboardShortcuts` hook (Escape/Delete/Ctrl+S); inline Escape + Ctrl+S in `RulesEditor` and `BreakpointsEditor`
- ~~Frontend hooks on manual useState/useEffect~~ — React Query (`@tanstack/react-query` v5) adopted for admin UI and audit logging

### Security Headers & Rate Limiting (2026-05-02)
- ~~Missing HTTP security headers~~ — Middleware added in `Program.cs`: `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `X-Permitted-Cross-Domain-Policies`, `Content-Security-Policy`, and `Strict-Transport-Security` (production-only)
- ~~Rate limiting off by default~~ — `RateLimit.Enabled=true` in `appsettings.json`; `RateLimit.Enabled=false` in `appsettings.Development.json` disables it in dev
- ~~Missing `appsettings.Production.json` example~~ — Template added at `src/IoTSpy.Api/appsettings.Production.json` with Postgres, HTTPS, rate limiting, data retention, and Serilog file sink

### CA Certificate Customization (2026-05-02)
- ~~CA certificate CN/O/C/validity hardcoded~~ — Added `CaCommonName`, `CaOrganization`, `CaCountry`, `CaValidityYears` to `ProxySettings`; `CertificateAuthority.GenerateRootCa()` reads these values; EF migration `AddCaCustomizationFields` applies defaults; `SettingsModal.tsx` exposes a new "CA Certificate" section; `PUT /api/proxy/settings` accepts the new fields; `ICertificateAuthority.RegenerateRootCaAsync()` atomically clears caches and regenerates using current settings

### API & Backend Polish (prior session)
- ~~Bulk operations missing~~ — Bulk rule enable/disable (`PATCH /api/manipulation/rules/bulk`), cancel-all scans (`POST /api/scanner/jobs/cancel-all`), bulk capture delete by filter
- ~~Missing export endpoints~~ — Fuzzer export (`GET /api/manipulation/fuzzer/jobs/{id}/export`), scan export (`GET /api/scanner/jobs/{id}/export`), ruleset bundle export (`GET /api/manipulation/export`), ruleset import (`POST /api/manipulation/import`)
- ~~No audit trail for config changes~~ — `AuditEntry` extended with `OldValue`/`NewValue` JSON snapshots; all rule/spec/breakpoint mutations recorded with before/after diffs
- ~~ScannerController no tests~~ — `ScannerControllerTests.cs` added with 7 tests
- ~~SessionsController no tests~~ — `SessionsControllerTests.cs` added with 14 tests

### Frontend Usability (prior session)
- ~~Missing confirmation dialogs~~ — `RulesEditor`, `BreakpointsEditor`, `FuzzerPanel`, `ScanJobList` (delete), and `SessionsPanel` all use `ConfirmDialog`
- ~~Export buttons not wired~~ — `CaptureList` toolbar has CSV/JSON/HAR export dropdown
- ~~Empty states need guidance~~ — All major panels have onboarding hints (manipulation rules, fuzzer, scanner, sessions)
- ~~Frontend capture list performance~~ — `react-window` + `AutoSizer` for virtual scrolling; infinite scroll via `loadMore`

### Phase 20 (Admin UI & Body Viewer)
- ~~Stray draft components in `src/IoTSpy.React/`~~ — `PacketAnalysisView.tsx` and `NetworkDeviceSelector.tsx` deleted; unique functionality migrated into `PacketListFilterable.tsx`

### Phase 11 (Multi-user & UX)
- ~~No Core model tests~~ — `IoTSpy.Core.Tests` project added with 30+ model default/enum tests
- ~~No multi-user support~~ — Multi-user RBAC with `User` model, `UserRole` enum (Admin/Operator/Viewer), user management endpoints
- ~~Dashboard not responsive~~ — Responsive CSS with mobile breakpoints (480px, 768px, 1024px)
- ~~TLS passthrough/SSL strip untested~~ — `TlsClientHelloParserTests` (13 tests), `TlsServerHelloParserTests` (11 tests), `SslStripServiceTests` (14 tests)

### Phase 16 (Deployment & Operations)
- ~~No HTTPS for the API itself~~ — `HttpsCertificateHolder` + `CertesLetsEncryptService` added in Phase 16.1; HTTPS on port 5001 with cert file or Let's Encrypt via `Certes`