# IoTSpy — Known Gaps & Technical Debt

This document tracks remaining gaps, known limitations, and technical debt. Items are prioritized by severity and implementation effort. See [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for larger planned features.

---

## Active Gaps

| Gap | Description | Severity | Status | Notes |
|---|---|---|---|---|
| Customization of CA Certificate | Customize CN, Organization, and validity properties on the proxy CA cert | Medium | Open | UX enhancement; currently uses hardcoded values |
| Content replacement: binary & SSE | `ContentReplacer` doesn't correctly handle image/video replacement or SSE stream mocking with local files | High | Open | See Phase 22 in PHASES-ROADMAP.md |
| No LDAP / SAML SSO | Enterprise single sign-on not implemented | Low | Open | Deprioritized in Phase 16.5; valid candidate for future work |
| No distributed / multi-node mode | Single-instance proxy per deployment; horizontal scaling requires Redis backplane | Low | Open | Deprioritized in Phase 16.8; see Design Assumptions |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low | Open | See Phase 17 for future work |

---

## Frontend Usability Gaps

Quick wins — no schema changes required unless noted.

### Missing Confirmations

Delete operations outside of Admin tabs lack confirmation dialogs. The admin pattern (`UsersTab.tsx`, `DatabaseTab.tsx`) already works and should be applied consistently:

- `ManipulationController` rule and breakpoint deletes (`RulesEditor.tsx`, `BreakpointsEditor.tsx`)
- Fuzzer job cancellation/delete (`FuzzerPanel.tsx`)
- Scanner job cancel (`ScannerPanel.tsx`)
- Capture list clear button (`CaptureList.tsx`)
- Session delete (`SessionsPanel.tsx`)

### Export Buttons Not Wired Up

The backend already implements `GET /api/captures/export?format=csv|json|har` and `GET /api/admin/export/config` but `CaptureList.tsx` exposes no export controls. Backend work: none. Frontend: add a dropdown export button to the capture list toolbar.

Similarly, fuzzer results and scan findings have no export path at all (see API Completeness below).

### Pagination UI Incomplete

`CapturesController` returns `pages` and `total` in every response, and `useCaptures.ts` has `loadMore`, but the UI has no "page N of M" indicator or jump-to-page control. Users with large capture histories have no way to navigate to specific time windows efficiently.

`/api/manipulation/rules` and `/api/manipulation/breakpoints` return flat arrays with no pagination at all — a performance problem at scale (see API Completeness below).

### Empty States Need Next-Step Guidance

The capture list has a helpful onboarding hint. Other panels show only "No data":

- Manipulation rules and breakpoints panels — hint: "Create a rule to modify matching traffic"
- Scanner panel with no jobs — hint: "Select a device and start a port scan"
- Sessions panel with no sessions — hint: "Create a session to start collaborating"
- Fuzzer panel with no jobs — hint: "Select a captured request to begin fuzzing"

### Missing Keyboard Shortcuts

No keyboard navigation in any data grid or list. Common expectations: `Delete` on selected row, `Escape` to close modals, `Enter` to confirm dialogs, `Ctrl+S` to save rule/breakpoint editor. A global `useKeyboardShortcuts` hook could cover all panels.

### Session Filtering Not Exposed

`GET /api/sessions` returns all sessions regardless of creator. No `?createdBy=me` filter exists in the repository layer (`IInvestigationSessionRepository`) or the API. Operators with many sessions have no way to see "my sessions" without scrolling through everything.

---

## API Completeness Gaps

### Missing Bulk Operations

All list-mutating endpoints are single-item only. High-volume workflows (clearing a ruleset, cancelling all scans) require N individual HTTP calls:

| Missing endpoint | Affected controller | Use case |
|---|---|---|
| `DELETE /api/manipulation/rules` (by id list or `?all=true`) | `ManipulationController` | Clear/reset ruleset |
| `PATCH /api/manipulation/rules/enabled` (bulk toggle) | `ManipulationController` | Enable/disable a group of rules |
| `POST /api/scanner/jobs/cancel-all` | `ScannerController` | Abort all running scans |
| `DELETE /api/scanner/jobs` (bulk by status/date) | `ScannerController` | Purge old scan history |
| `DELETE /api/captures/bulk` (by device/date/host) | `CapturesController` | Targeted data cleanup outside admin page |

### Missing Export Endpoints

| Data | Controller | Gap |
|---|---|---|
| Fuzzer results | `ManipulationController` | No export; DB only |
| Scan findings | `ScannerController` | No export; DB only |
| Manipulation rules + breakpoints as portable JSON | `ManipulationController` | Import/export for sharing rulesets between environments |
| OpenRTB PII policies | `OpenRtbController` | No export; DB only |

The admin config export (`GET /api/admin/export/config`) covers some config tables but is admin-role gated and not suitable for operator-level ruleset sharing.

### Missing Pagination on List Endpoints

`/api/manipulation/rules` and `/api/manipulation/breakpoints` return unbounded flat arrays. At 1000+ rules (achievable with generated rulesets), these responses become large and slow. Both need `?page=&pageSize=` params and `{ items, total, pages }` response shape, consistent with `CapturesController`.

### Incomplete Filtering on Key Endpoints

| Endpoint | Available filters | Missing |
|---|---|---|
| `GET /api/scanner/jobs` | None found in `ScanJobRepository` | Filter by `status`, `deviceId`, `createdAfter` |
| `GET /api/openrtb/events` | Minimal | Filter by `adomain`, `device`, `hasViolation`, date range |
| `GET /api/manipulation/fuzzer/jobs` | None | Filter by `status`, `targetCapture` |
| `GET /api/captures` | Good (host, protocol, status, date, device) | Full-text search in request/response headers |

### No Audit Trail for Configuration Changes

`AuditEntry` tracks user authentication and CRUD on users/API keys, but configuration changes are invisible:

- Rule creation/update/delete
- Breakpoint script changes (security-sensitive — scripts execute code)
- API spec activation/deactivation
- Content replacement rule changes
- OpenRTB PII policy changes

Before/after JSON diffs on these operations would make configuration drift auditable. Model change: add `OldValue` and `NewValue` (nullable text) to `AuditEntry`.

---

## Security Hardening

### Missing HTTP Security Headers

No security headers middleware in `Program.cs`. A single `app.Use(...)` call or `NWebsec` package covers all of these:

| Header | Value needed | Risk without |
|---|---|---|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self'` | XSS via injected scripts |
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-Content-Type-Options` | `nosniff` | MIME-type sniffing attacks |
| `Strict-Transport-Security` | `max-age=31536000` | SSL stripping against the dashboard itself |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | URL leakage |

### Rate Limiting Disabled by Default

The sliding-window rate limiter is fully configured in `Program.cs` (~line 174) but off by default. It should default-on for production, with the option to disable in development via `appsettings.Development.json`. No endpoint-specific limits exist (e.g., login attempts should be stricter than GET endpoints).

### No Input Validation Framework

Controllers use ad-hoc `if (string.IsNullOrWhiteSpace(...))` guards. No formal validation layer means:
- Port range inputs not validated (e.g., port 99999 accepted)
- Regex patterns in rules not validated before storage (an invalid regex will crash rule evaluation at runtime)
- IP/hostname fields accept arbitrary strings
- File upload MIME type only checked by extension, not magic bytes

Recommendation: FluentValidation with validators per request DTO; add regex pre-compile check in `ManipulationRule` validation.

### Missing Production Config Example

No `appsettings.Production.json` example exists. New deployers must reverse-engineer production settings from code. Minimum needed: DB connection string, TLS cert path, CORS origin, rate limit enable, JWT secret source.

---

## Testing Gaps

| Component | Current Status | Gap | Notes |
|---|---|---|---|
| `ManipulationController` | No test file | Full controller test suite needed | Only `ApiSpecControllerTests` covers manipulation area |
| `ScannerController` | No test file | Happy path + error cases | Scanner service is async/cancellable; edge cases matter |
| `SessionsController` | No dedicated tests | Collaboration flow, permission enforcement | Phase 15 tests were in integration suite only |
| `PacketCaptureController` | Minimal | Progress streaming, freeze-frame, filter API | Complex controller with many endpoints |
| Frontend components | ~3 spec files for 54 components | Manipulation, capture, sessions panels untested | Vitest + React Testing Library pattern already established |
| End-to-end frontend | None | Cypress or Playwright suite | Would require Docker orchestration for backend |
| Load testing | None | Proxy throughput/latency baseline | Critical for production planning |
| Chaos engineering | None | Proxy crash, DB unavailable scenarios | Validates graceful degradation |
| Security fuzzing | Limited | AFL/libFuzzer on MQTT, DNS, CoAP parsers | Could discover parsing edge cases |

---

## Protocol Decoder Depth

Decoders exist for all major protocols but vary in depth. These are not blockers, but deeper implementations improve research value:

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

1. **Rule evaluation** — 100+ rules on 10k captures → full table scan each time; regex caching or compiled-rule cache needed
2. **JSON schema inference** — Recursive object traversal on large payloads; cache schema per `(host, path, method)` tuple
3. **Packet capture ring buffer** — 10k packet hard limit; large capture bursts will drop packets; consider configurable or dynamic sizing
4. **Frontend capture list** — Virtual scrolling would improve responsiveness with 100k+ rows
5. **`ContentReplacer.ApplyAsync`** — No streaming; entire response body loaded into memory; problematic for large video/binary replacements

### Optimization Opportunities

- Request deduplication in proxy pipeline (same URL + headers within N ms → single capture)
- Rule caching layer with invalidation on CRUD operations (already noted in `ApiSpecMockService`)
- Bloom filter pre-screen before full rule evaluation
- Frontend: React Query / SWR for automatic cache, deduplication, and background refetch (currently all hooks use manual `useState`/`useEffect` fetch cycles)

---

## Design Assumptions

These assumptions should be revisited if requirements change:

1. **Single-instance proxy per deployment** — Assumes one API process and one pair of proxy servers (explicit + transparent). Horizontal scaling requires Redis backplane (Phase 16.8).
2. **SQLite for development, Postgres for production** — Current schema and migrations support both, but testing is primary on SQLite.
3. **Browser-based dashboard** — Assumes web client; no native mobile apps or CLI tools planned.
4. **JWT + API key auth only** — No SAML/LDAP support (Phase 16.5 deprioritized; see Active Gaps).
5. **In-memory anomaly detector** — Resets on restart; no persistent baseline learning.
6. **Request-scoped repositories** — Each HTTP request gets a fresh EF Core DbContext; not suitable for long-running background tasks without scope management.
7. **Text-only content replacement** — `ContentReplacer` buffers entire response body as string; binary content types (image, video, audio) require a streaming binary pipeline (see Phase 22).

---

## Documentation Gaps

| Area | Status | Notes |
|---|---|---|
| Operator runbook | Basic | Need: troubleshooting guide, log interpretation, health check procedures |
| Deployment guides | Minimal | Docker Compose (dev) + Helm chart exist; need: bare-metal, systemd, production hardening guide |
| Production config example | Missing | `appsettings.Production.json` template with all required settings |
| Performance tuning | None | Connection pool sizing, rate limit tuning, ring buffer sizing, cache strategies |
| Content replacement guide | Partial | How to match and replace JSON/text covered; binary (image/video/audio) and SSE not documented |
| Extension guides | Partial | Protocol decoders, AI providers covered; custom UI components and plugin development not covered |

---

## Security Audit Notes

- Root CA private key stored in local database (not HSM-protected); acceptable for research tool, not production
- API endpoints require Bearer token or API key; no rate limiting on `/api/auth/setup` (mitigated by one-time-only check)
- TLS MITM disabled in passthrough mode (`CaptureTls=false`); metadata extraction only
- SSL stripping requires explicit `SslStrip=true` flag; off by default
- Audit log immutable in design but not enforced at DB level; could add write-once table constraint
- Breakpoint scripts execute arbitrary C# (Roslyn) and JavaScript (Jint) — no sandbox restrictions; operator-role required but worth documenting explicitly

---

## Resolved Items

### Phase 16 (Deployment & Operations)
- ~~No HTTPS for the API itself~~ — `HttpsCertificateHolder` + `CertesLetsEncryptService` added in Phase 16.1; HTTPS on port 5001 with cert file or Let's Encrypt via `Certes`

### Phase 20 (Admin UI & Body Viewer)
- ~~Stray draft components in `src/IoTSpy.React/`~~ — `PacketAnalysisView.tsx` and `NetworkDeviceSelector.tsx` deleted; unique functionality migrated into `PacketListFilterable.tsx`

### Phase 11 (Multi-user & UX)
- ~~No Core model tests~~ — `IoTSpy.Core.Tests` project added with 30+ model default/enum tests
- ~~No multi-user support~~ — Multi-user RBAC with `User` model, `UserRole` enum (Admin/Operator/Viewer), user management endpoints
- ~~Dashboard not responsive~~ — Responsive CSS with mobile breakpoints (480px, 768px, 1024px)
- ~~TLS passthrough/SSL strip untested~~ — `TlsClientHelloParserTests` (13 tests), `TlsServerHelloParserTests` (11 tests), `SslStripServiceTests` (14 tests)

---

## Suggestions for Next Contributors

1. **Start with Phase 21** (Passive Mode) — Well-scoped, clear requirements, minimal API changes
2. **Or Phase 22** (Rich Media Content Replacement) — High-value for ad/tracking research; well-defined scope
3. **Quick security wins** — Security headers and default rate limiting are 30-minute tasks with immediate production-readiness value
4. **Add missing controller tests** — `ManipulationControllerTests`, `ScannerControllerTests`, `SessionsControllerTests` would meaningfully improve coverage
5. **CA certificate customization** — Medium-severity UX improvement, fully self-contained
6. **Avoid Phase 17.3+** (Zigbee/BLE) — Requires specialized hardware; hard to test in CI
7. **Keep `IoTSpy.Core` dep-free** — New models/interfaces should go there; no infrastructure references

See [AGENT-NOTES.md](AGENT-NOTES.md) for session setup and testing instructions.
