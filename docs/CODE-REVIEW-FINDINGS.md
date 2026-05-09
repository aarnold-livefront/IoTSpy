# Code Review Findings — 2026-05-09

Multi-angle review (backend correctness/security, frontend code & responsive UX, documentation accuracy, feature/use-case gaps). Items marked **[FIXED]** were addressed in the day-0 hotfix PR; everything else is queued for future agents/PRs.

Severity legend: **Critical** = ship blocker, **High** = next-sprint, **Medium** = should-have, **Low** = nice-to-have.

---

## CRITICAL

### [FIXED] 1. `GET /api/packet-capture/status` was a hardcoded stub
`src/IoTSpy.Api/Controllers/PacketCaptureController.cs:74` returned `{ isCapturing = false }` unconditionally. Now reads `_captureService.IsCaptureActive`. Existing test was asserting only the field name — extended to assert true/false in both states.

### [FIXED] 2. Admin role-case mismatch silently bypassed authz
`AuthService` and `ApiKeyAuthenticationHandler` emit role claim as lowercase `"admin"`, but `PluginsController.Reload` and `SessionsController.Delete` used `[Authorize(Roles = "Admin")]`. Role match is case-sensitive — both endpoints 403'd for everyone, including real admins. Fixed to lowercase.

### [FIXED] 3. Path traversal via `ReplacementFilePath`
`ContentRulesController.Create/Update` and `ApiSpecController.CreateRule/UpdateRule` accepted arbitrary file paths from authenticated users; `ContentReplacer` then opened them directly. Any authenticated user could exfiltrate `/etc/passwd`, the SQLite DB, or app secrets. Fixed by `AssetsPaths.ResolveReplacementFilePath` which rejects path separators / `..` and pins the resolved path inside `AssetsDirectory`.

### [FIXED] 5. CSS variables `--color-text-secondary`, `--color-error-bg`, `--color-bg-alt`, `--color-accent`, `--color-input-bg` were undefined globally
Referenced across `ProtocolProxyPanel`, `PluginsTab`, `GrpcSchemasPanel`, `scanner.css`, `passive.css`, `header.css`. Result: invisible text, transparent error banners, "Start Scan" button text floating on a transparent background. Added aliases to both theme blocks in `frontend/src/styles/variables.css`.

### 4. No scan scope enforcement
`ScannerController` accepts any `DeviceId`; `ScannerService` scans whatever IP is on the device record. No CIDR allowlist, no local-subnet check, no authorization gate. A misconfigured rule could scan arbitrary internet hosts.
**Slot:** new `ScanScope` model + enforcement in `ExplicitProxyServer` and `ScannerService`. Add CIDR allowlist + acknowledgment of authorization on first scan-start of an engagement. Audit-log the scope decision on each scan job.

### 6. `PanelPacketCapture` stale-closure infinite-render risk
`frontend/src/components/panels/PanelPacketCapture.tsx:28-33` includes the unstable `analysis` object literal in `useEffect` deps. React 19 strict mode will spiral.
**Fix:** depend only on the destructured `useCallback` refs:
```tsx
const { loadProtocols, loadPatterns, loadSuspicious } = analysis
useEffect(() => {
  if (activeTab === 'protocols') loadProtocols()
  else if (activeTab === 'patterns') loadPatterns()
  else if (activeTab === 'suspicious') loadSuspicious()
}, [activeTab, loadProtocols, loadPatterns, loadSuspicious])
```

### 7. `PanelPacketCapture` 3-column layout has no mobile breakpoint
Fixed 280px sidebar + 400px inspector forces ~800px min-width. Broken below 768px.
**Slot:** `frontend/src/styles/responsive.css` — add `@media (max-width: 768px) { .ppc-root { flex-direction: column } .ppc-sidebar, .ppc-inspector { width: 100% } }`.

### 8. No crash resilience for live captures
`LockFreePacketRingBuffer` is in-memory only; API restart loses every queued packet. No WAL checkpoint, no DB flush.
**Slot:** `PacketCaptureService` — periodic background flush of ring-buffer contents to the `Packets` table, or write packets to DB synchronously and use the ring buffer purely as a SignalR broadcast cache.

---

## HIGH

### Security & correctness

**9. `ProtoSchemasController.Upload` has no size limit** — `ProtoSchemasController.cs:32-53` buffers entire body in memory, stores in DB. Add `[RequestSizeLimit(1 * 1024 * 1024)]` and a length guard before invoking `ProtoParser`. Compare to `ApiSpecController.UploadAsset` which correctly uses `[RequestSizeLimit(50 * 1024 * 1024)]`.

**10. `ProtoSchemasController.Delete` is not Admin-restricted** — analogous destructive ops (breakpoint delete, CA regen) are admin-only. Add `[Authorize(Roles = "admin")]`.

**11. Admin purge endpoints OOM-risky** — `AdminController.cs:70-71`, `:99-100` use `ToListAsync()` + `RemoveRange`. Replace with `query.ExecuteDeleteAsync(ct)` (single SQL DELETE, no change-tracker hydration).

**12. `ManipulationRuleCache` TOCTOU race** — `ManipulationRuleCache.cs:19-33` does `TryGetValue` + `Set` without sync; `Invalidate` between them gets overwritten. Use `IMemoryCache.GetOrCreateAsync` with a factory so atomicity is internal.

**13. Replay/Fuzzer silently bypass TLS validation** — `ManipulationExtensions.cs:41-44, :51-53` set `ServerCertificateCustomValidationCallback => true` per named-client (always-on), not per-request. Make it an opt-in `BypassTlsValidation` flag on `StartReplayDto`/`StartFuzzerDto` with audit log warning.

**14. Audit write-once trigger is SQLite-only** — `AuditWriteOnceTrigger` migration uses `RAISE(ABORT, ...)` and `CREATE TRIGGER IF NOT EXISTS`; will fail on Postgres. The audit-immutability claim silently does not hold in prod Postgres deployments. Gate on `migrationBuilder.IsNpgsql()/IsSqlite()` and emit provider-appropriate trigger DDL.

**15. `ScannerController.StartScan` accepts unbounded `PortRange`** — `ParsePortRange` silently truncates garbage; pathological strings can resolve to 65535 ports × `MaxConcurrency=100` = 6.5M concurrent TCP connects per scan job. Cap port count and string length in the controller; cap `MaxConcurrency` server-side.

**16. No code signing on plugin loader** — `PluginLoaderService` loads arbitrary `.dll`s with no hash/signature check. At minimum, document the trust boundary in the admin UI; better, require a manifest with SHA-256 and check against an allowlist.

### Backend missing tests

**17. Four shipped controllers have zero tests** — `ContentRulesController`, `ProtoSchemasController`, `PluginsController`, `ProtocolProxyController`. Issues #2, #3, #9, #10 would all have been caught by basic happy-path tests. **Slot:** new files under `src/IoTSpy.Api.Tests/Controllers/`.

### Frontend correctness

**18. `PanelPacketCapture` swallows export failures silently** — `PanelPacketCapture.tsx:57-74` `catch {}` no user feedback. Set `error` state with `'Export failed — check server logs.'`.

**19. `PacketInspector` close button is `<button>x</button>`** — no accessible name. Add `aria-label="Close inspector"` (`PacketInspector.tsx:37`).

**20. `RulePreviewModal`, `ContentRulesPanel`, `AssetLibrary`, `PacketInspector`, `GrpcSchemasPanel`, `ProtocolProxyPanel`, `PluginsTab` are 100% inline-styled with hardcoded hex colors** — Batch 5 migrated `PanelPacketCapture` to a CSS module; Batch 6 work didn't follow the pattern. Light theme broken in all of them. Inline styles are also unreachable from `responsive.css` media queries. **Slot:** create one CSS module per component, replace inline styles with classes, replace hex literals with `var(--color-*)`.

**21. No focus trap in any modal** — `RulePreviewModal`, inline `ContentRulesPanel` modal, `AssetLibrary` picker. Tab leaks focus to background. Use native `<dialog>` element or set `inert` on `.app-main` when a modal is open.

### Responsive (small-screen) issues

**22. `ProtocolProxyPanel` two-column grid is inline-styled** — `ProtocolProxyPanel.tsx:72` `gridTemplateColumns: '1fr 1fr'` cannot be overridden by media query. At 375px each panel is ~177px, unusable. Extract to a CSS class with mobile breakpoint to `1fr`.

**23. `ScannerPanel` body lacks base flex declarations** — `responsive.css` has the `flex-direction: column` override but `scanner.css` never declares the base `display: flex` — override collapses to nothing. Add base rules for `.scanner-panel__body`, `.scanner-panel__left`, `.scanner-panel__right`.

**24. Admin tabs (6) have no overflow/wrap** — `admin.css` lines 22-27. On 375px viewports, tabs clip or force horizontal page scroll. Add `overflow-x: auto; -webkit-overflow-scrolling: touch` and `white-space: nowrap` on `.admin-tab` in the `≤768px` block.

**25. `PluginsTab` table missing `admin-table-wrap`** — `PluginsTab.tsx:38`. Assembly-path `<code>` cells force overflow on narrow viewports. Wrap the table in a div with `admin-table-wrap` class (already defined with `overflow-x: auto`).

### Documentation drift

**26. CLAUDE.md test count contradictions** — line 72 says "715", line 82 says "760", AGENT.md says "683", actual is **722** ([Fact]/[Theory] count via grep). Lock the count in both files and re-run the verification command at the bottom of CLAUDE.md.

**27. CLAUDE.md line 44 lists 19 controllers, line 83 claims 20** — missing `ProtoSchemas` from the enumeration. Add it.

**28. CLAUDE.md line 125 says Manipulation panel has "7 tabs"** — actually 8 (gRPC Schemas added in Batch 6, documented on line 92 of the same file). Update line 125.

---

## MEDIUM

### Incomplete shipped features

**29. HAR export emits empty `headers: []`** — `CapturesController.cs:166-184`. Columns `RequestHeaders`/`ResponseHeaders` exist; deserialize them into the HAR output. Without this, the HAR is technically valid but useless to Chrome DevTools/mitmproxy.

**30. `ExportConfig` omits `ContentReplacementRule` and `ProtoSchema`** — `AdminController.cs:159`. Backup/restore silently loses standalone content rules (post-Phase 22) and gRPC proto schemas (Batch 6). Extend the export.

**31. No `ImportConfig` endpoint exists** — `/api/manipulation/import` only restores rulesets. Full config round-trip impossible. Add `POST /api/admin/import/config`.

**32. `ScheduledScan` has no FK to last-run scan job, no failure flag** — admins can't tell whether the 2 AM scan succeeded. Add `LastRunScanJobId`, `LastRunStatus`, `LastRunError` columns + migration.

**33. Report covers scan findings only** — `ReportService.cs:50` only loads `ScanJob` + `ScanFinding`. No captures, TLS metadata, annotations, MQTT/DNS messages. Not a usable pen-test deliverable. Redesign report sections + template system.

**34. `ProtoParser.FromJson` is a fragile hand-rolled parser** — `ProtoParser.cs:89-106` splits on `,` and `:` strings; breaks on legal proto field names containing those chars. Replace with `System.Text.Json.JsonSerializer`.

**35. `AdminController.GetStats` uses magic-number storage estimates** — `count * 2048L` and `count * 512L`. Use `PRAGMA page_count * page_size` for SQLite, `pg_relation_size` for Postgres.

### Missing user stories (high-value)

**36. Capture-to-curl** — table-stakes in Proxyman/mitmproxy; not present. Add `GET /api/captures/{id}/curl` + UI button on `RequestTab`/`ResponseTab`.

**37. HAR import** — export exists but no `POST /api/captures/import/har`. Developers can't seed IoTSpy from a browser-captured HAR.

**38. Replay against override base URL** — `StartReplayDto.Host` exists but UX/coverage unclear. Verify end-to-end and surface in the UI.

**39. Body full-text search on captures** — `?q=` searches URL/method/host, `?headerQ=` searches headers, no body content search. Wire SQLite FTS5 (already available) on `RequestBody`/`ResponseBody` columns.

**40. In-app SignalR notification when a rule/breakpoint fires** — `AlertingService` does external webhooks/email/Slack only. Add an in-app channel via the existing collaboration hub.

**41. User mgmt UI may be incomplete** — `UsersTab.tsx` exists but create/edit wiring to `POST /api/auth/users` not verified. Audit the file.

**42. DB backup/restore endpoints absent** — admin UI has purge only. Add `GET /api/admin/backup` (SQLite `.backup` / `pg_dump` invocation) + `POST /api/admin/restore`.

**43. `DataRetentionService` thresholds are config-file-only** — no runtime API. Add `GET/PUT /api/admin/retention` + UI in DatabaseTab.

### Protocol coverage gaps

**44. AMQP 1.0** (Azure IoT Hub, ActiveMQ — high IoT relevance), **RTSP/RTP** (IP cameras — frequent IoT finding), **MQTT-SN** (constrained-device UDP variant), **DoH/DoT detection** (devices increasingly evade DNS inspection). All on roadmap, none shipped. Slot: `IoTSpy.Protocols`.

### Trust & safety

**45. No "I am authorized" consent gate** in onboarding — `OnboardingWizard.tsx`. Add an acknowledgment step + audit-log on first run.

**46. Audit log DELETE not blocked** — `AuditWriteOnceTrigger` covers UPDATE only. Add a `BEFORE DELETE` trigger that aborts unless the caller is a designated archive role.

**47. Scanner `MaxConcurrency` defaults to 100** with no global cap — aggressive on shared networks. Lower default to 25; add server-side global cap (`Scanner:MaxGlobalConcurrency`).

**48. No per-user data isolation** — Viewer-role users see all captures/scans across the instance. For shared-instance deployments this leaks data. Add row-level ownership filter on capture/scan/device queries.

### Accessibility

**49. Tab-button groups lack ARIA tab semantics** — `PacketInspector`, `ManipulationPanel`, `PanelPacketCapture` use plain `<button>` without `role="tab"`/`aria-selected`/`role="tablist"`. Add roles.

**50. Clickable `<div>` rows in `RulePreviewModal` capture list** — no keyboard access. Add `tabIndex={0}`, `role="option"`, `onKeyDown` (Enter/Space), wrap list in `role="listbox"`.

---

## LOW

**51. OpenTelemetry tracing absent** — Serilog only. For multi-container deployments, cross-service trace correlation is missing.

**52. Prometheus metric surface thin** — 6 metrics. Missing: breakpoint hits, fuzzer throughput, rule match rate, per-protocol capture volume, DB query latency, SignalR connection count.

**53. No bundled Grafana dashboards** despite Helm chart shipping.

**54. No on-call runbook in `docs/`** — what to do when the proxy stops intercepting, how to recover from corrupt SQLite, how to rotate JWT secret without logging everyone out.

**55. Capture diff endpoint absent** (`GET /api/captures/diff?a=&b=`).

**56. Project/workspace concept absent** — no aggregate over Device + Session + ScanJob to namespace per-engagement artifacts.

**57. CVSS override unavailable on findings** — `ScanFinding.CvssScore` is OSV-derived and read-only. Add `PATCH /api/scanner/findings/{id}` for tester override.

**58. Scheduled scans against a target list** — `ScheduledScan` is FK'd to a single `Device`. Support tag/CIDR-based target lists.

**59. SSO/OIDC** — JWT-only with local password store. Blocks enterprise adoption.

---

## Recommended PR sequencing for future agents

1. **Test backfill PR** (#17): write tests for the four untested controllers. Surfaces #2, #3, #9, #10 inherently and locks the day-0 fixes.
2. **Security hardening PR** (#9-#16, #45-#47): scope enforcement is the biggest dual-use gap; rest is hardening of existing surface.
3. **Responsive pass PR** (#6, #18-#25): once the missing CSS variables are added (done), the rest is media-query work plus extracting inline styles from Batch-6 components into CSS modules.
4. **Documentation accuracy PR** (#26-#28): re-derive counts via the verification command in CLAUDE.md and lock them.
5. **Incomplete-feature PR** (#29-#35): HAR headers, config import/export completeness, scheduled-scan history, parser hardening.
6. **Feature/UX PRs** (#36-#43, #49-#50): per persona, scoped individually.
7. **Protocol coverage** (#44): one PR per protocol; AMQP and RTSP first.
8. **Operational** (#51-#54): dashboards + runbook + OTEL.

Each item above is sized to fit a single focused PR; do not bundle across categories.
