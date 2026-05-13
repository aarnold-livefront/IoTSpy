# Code Review Findings — Status & Remaining Work

Original review: 2026-05-09. This file is the **live status board** for the multi-angle review (backend correctness/security, frontend code & responsive UX, documentation accuracy, feature/use-case gaps). Items move from "Remaining" to "Completed" as PRs merge.

When picking up new work, consult the **Recommended next PRs** section at the bottom.

Severity legend: **Critical** = ship blocker · **High** = next-sprint · **Medium** = should-have · **Low** = nice-to-have.

---

## Status snapshot (2026-05-09 → present)

| State | Count | Items |
|---|---|---|
| ✅ Completed | 28 | #1, #2, #3, #5, #6, #7, #9, #10, #11, #12, #14, #15, #17, #18, #19, #20, #21, #22, #23, #24, #25, #26, #27, #28, #47, #49, #50 |
| ⏳ In-flight (open PR) | 0 | — |
| 🟥 Critical remaining | 2 | #4, #8 |
| 🟧 High remaining | 2 | #13, #16 |
| 🟨 Medium remaining | 20 | #29–#46, #48 |
| 🟩 Low remaining | 9 | #51–#59 |

PR history that closed items: **#59** (day-0 hotfixes), **#60** (doc accuracy), **#61** (test backfill), **#62** (responsive pass), **#63** (security hardening), **#66** (modal system: #20, #21, #49, #50).

---

## 🟥 Critical — remaining

### 4. No scan scope enforcement
`ScannerController.StartScan` accepts any `DeviceId`; `ScannerService` scans whatever IP is on the device record. No CIDR allowlist, no local-subnet check, no authorization gate. A misconfigured rule could scan arbitrary internet hosts.
**Slot:** new `ScanScope` model + enforcement in `ExplicitProxyServer` and `ScannerService`. Add CIDR allowlist + acknowledgment of authorization on first scan-start of an engagement. Audit-log the scope decision on each scan job. Pairs naturally with #45 (consent gate) — they share the same UX onboarding step.

### 8. No crash resilience for live captures
`LockFreePacketRingBuffer` is in-memory only; API restart loses every queued packet. No WAL checkpoint, no DB flush.
**Slot:** `PacketCaptureService` — periodic background flush of ring-buffer contents to the `Packets` table, or write packets to DB synchronously and use the ring buffer purely as a SignalR broadcast cache.

---

## 🟧 High — remaining

### Security & correctness

**13. Replay/Fuzzer silently bypass TLS validation** — `ManipulationExtensions.cs:41-44, :51-53` set `ServerCertificateCustomValidationCallback => true` per named-client (always-on), not per-request. Make it an opt-in `BypassTlsValidation` flag on `StartReplayDto` / `StartFuzzerDto` with audit-log warning. Surface the toggle in the Replay/Fuzzer UI.

**16. No code signing on plugin loader** — `PluginLoaderService` loads arbitrary `.dll`s with no hash/signature check. At minimum, document the trust boundary in the admin UI; better, require a manifest with SHA-256 and check against an allowlist. Decision needed on key-mgmt approach before implementation.

---

## 🟨 Medium — remaining

### Incomplete shipped features

**29. HAR export emits empty `headers: []`** — `CapturesController.cs:166-184`. Columns `RequestHeaders` / `ResponseHeaders` exist; deserialize them into the HAR output. Without this, the HAR is technically valid but useless to Chrome DevTools / mitmproxy.

**30. `ExportConfig` omits `ContentReplacementRule` and `ProtoSchema`** — `AdminController.cs:159`. Backup/restore silently loses standalone content rules (post-Phase 22) and gRPC proto schemas (Batch 6). Extend the export.

**31. No `ImportConfig` endpoint exists** — `/api/manipulation/import` only restores rulesets. Full config round-trip impossible. Add `POST /api/admin/import/config`. Pairs with #30.

**32. `ScheduledScan` has no FK to last-run scan job, no failure flag** — admins can't tell whether the 2 AM scan succeeded. Add `LastRunScanJobId`, `LastRunStatus`, `LastRunError` columns + migration.

**33. Report covers scan findings only** — `ReportService.cs:50` only loads `ScanJob` + `ScanFinding`. No captures, TLS metadata, annotations, MQTT/DNS messages. Not a usable pen-test deliverable. Redesign report sections + template system.

**34. `ProtoParser.FromJson` is a fragile hand-rolled parser** — `ProtoParser.cs:89-106` splits on `,` and `:` strings; breaks on legal proto field names containing those chars. Replace with `System.Text.Json.JsonSerializer`.

**35. `AdminController.GetStats` uses magic-number storage estimates** — `count * 2048L` and `count * 512L`. Use `PRAGMA page_count * page_size` for SQLite, `pg_relation_size` for Postgres.

### Missing user stories (high-value)

**36. Capture-to-curl** — table-stakes in Proxyman / mitmproxy; not present. Add `GET /api/captures/{id}/curl` + UI button on `RequestTab` / `ResponseTab`.

**37. HAR import** — export exists but no `POST /api/captures/import/har`. Developers can't seed IoTSpy from a browser-captured HAR.

**38. Replay against override base URL** — `StartReplayDto.Host` exists but UX/coverage unclear. Verify end-to-end and surface in the UI.

**39. Body full-text search on captures** — `?q=` searches URL/method/host, `?headerQ=` searches headers, no body content search. Wire SQLite FTS5 (already available) on `RequestBody` / `ResponseBody` columns.

**40. In-app SignalR notification when a rule/breakpoint fires** — `AlertingService` does external webhooks/email/Slack only. Add an in-app channel via the existing collaboration hub.

**41. User mgmt UI may be incomplete** — `UsersTab.tsx` exists but create/edit wiring to `POST /api/auth/users` not verified. Audit the file before assuming work is needed.

**42. DB backup/restore endpoints absent** — admin UI has purge only. Add `GET /api/admin/backup` (SQLite `.backup` / `pg_dump` invocation) + `POST /api/admin/restore`.

**43. `DataRetentionService` thresholds are config-file-only** — no runtime API. Add `GET / PUT /api/admin/retention` + UI in DatabaseTab.

### Protocol coverage gaps

**44. AMQP 1.0** (Azure IoT Hub, ActiveMQ — high IoT relevance), **RTSP/RTP** (IP cameras — frequent IoT finding), **MQTT-SN** (constrained-device UDP variant), **DoH/DoT detection** (devices increasingly evade DNS inspection). All on roadmap, none shipped. Slot: `IoTSpy.Protocols`. One PR per protocol.

### Trust & safety

**45. No "I am authorized" consent gate** in onboarding — `OnboardingWizard.tsx`. Add an acknowledgment step + audit-log on first run. Pairs with #4.

**46. Audit log DELETE not blocked** — `AuditWriteOnceTrigger` covers UPDATE only. Adding `BEFORE DELETE` would conflict with `DataRetentionService.DeleteOlderThanAsync`. Decision needed: either retention writes to an immutable archive sink before deleting, or DELETE-protect entirely and stop pruning audit. Not a one-line fix.

**48. No per-user data isolation** — Viewer-role users see all captures/scans across the instance. For shared-instance deployments this leaks data. Add row-level ownership filter on capture/scan/device queries. Touches many controllers; bundle as a single multi-controller PR.

---

## 🟩 Low — remaining

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

## ✅ Completed

Each entry includes the closing PR. Use `gh pr view <num>` for the full diff and test plan.

### Critical (4 of 6)

- **#1 — `GET /api/packet-capture/status` was a hardcoded stub** [PR #59] — now reads `IPacketCaptureService.IsCaptureActive`. Test split into `_ReflectsServiceIsCaptureActive` + `_ReturnsFalseWhenCaptureInactive`.
- **#2 — Admin role-case mismatch silently 403'd everyone** [PR #59] — `[Authorize(Roles = "Admin")]` → `"admin"` on `PluginsController.Reload` and `SessionsController.Delete`. Regression test in `PluginsControllerTests.Reload_RequiresAdminRole_LowercaseToMatchAuthServiceClaim`.
- **#3 — Path traversal via `ReplacementFilePath`** [PR #59] — new `AssetsPaths.ResolveReplacementFilePath` rejects path separators / `..` and pins resolved paths inside `AssetsDirectory`. Five-payload `[Theory]` regression guard in `ContentRulesControllerTests.Create_RejectsReplacementFilePathOutsideAssetsDirectory`.
- **#5 — Five CSS variables undefined globally** [PR #59] — `--color-text-secondary`, `--color-error-bg`, `--color-bg-alt`, `--color-accent`, `--color-input-bg` aliased in both theme blocks of `variables.css`.
- **#6 — `PanelPacketCapture` infinite-render risk** [PR #62] — destructured stable `useCallback` refs; `useEffect` no longer depends on the unstable `analysis` object literal.
- **#7 — `PanelPacketCapture` 3-column layout broken <768px** [PR #62] — `responsive.css` stacks `.ppc-root` vertically; inspector capped at 50 vh.

### High security & correctness (6 of 8)

- **#9 — `ProtoSchemasController.Upload` had no size limit** [PR #63] — `[RequestSizeLimit(1 MB)]` + length guard on both upload paths.
- **#10 — `ProtoSchemasController.Delete` was not Admin-restricted** [PR #63] — `[Authorize(Roles = "admin")]` added; regression test asserts the attribute via reflection.
- **#11 — Admin purge endpoints OOM-risky** [PR #63] — `ToListAsync()` + `RemoveRange()` + `SaveChangesAsync()` replaced with `ExecuteDeleteAsync(ct)`.
- **#12 — `ManipulationRuleCache` TOCTOU race** [PR #63] — switched to `IMemoryCache.GetOrCreateAsync`.
- **#14 — Audit write-once trigger SQLite-only** [PR #63] — migration now branches on `migrationBuilder.ActiveProvider`; emits plpgsql trigger function for Npgsql, preserves existing SQLite trigger.
- **#15 — `ScannerController` accepted unbounded `PortRange`** [PR #63] — `PortScanner.MaxResolvedPorts = 10_000` cap; controller rejects port-range strings >256 chars.

### High frontend / UX (5 of 9)

- **#17 — Four shipped controllers had zero tests** [PR #61] — 49 new tests across `ContentRulesController`, `ProtoSchemasController`, `PluginsController`, `ProtocolProxyController`. Includes day-0 regression guards.
- **#18 — `PanelPacketCapture` swallowed export failures silently** [PR #62] — `setExportError` + `role="alert"` banner.
- **#19 — `PacketInspector` close button was `<button>x</button>`** [PR #62] — `aria-label="Close packet inspector"` and renders `×`.
- **#20 (partial) — Inline-styled Batch-6 components** [PR #62] — `PluginsTab` migrated to `admin.css` conventions + `plugins-tab.css`; `ProtocolProxyPanel` fully migrated to new `protocol-proxy.css`. Remainder (RulePreviewModal, ContentRulesPanel modal, AssetLibrary, PacketInspector body) is the modal-system PR (#20 remainder, above).
- **#22 — `ProtocolProxyPanel` un-overridable inline grid** [PR #62] — extracted to `.protocol-proxy-grid` / `.protocol-proxy-form-grid`; mobile breakpoint collapses to single column.
- **#23 — `ScannerPanel` body lacked base flex declarations** [PR #62] — base rules added to `scanner.css`; responsive override now has something to override.
- **#24 — Admin tabs (6) clipped on mobile** [PR #62] — `overflow-x: auto` + `flex-shrink: 0` in mobile breakpoint.
- **#25 — `PluginsTab` table missing `admin-table-wrap`** [PR #62] — table wrapped; assembly-path `<code>` no longer overflows page.
- **#20 (complete) — Inline-styled modals** [PR #66] — `RulePreviewModal`, `ContentRulesPanel` modal, `AssetLibrary`, `PacketInspector` fully migrated: component-scoped CSS files (`modal.css` extensions, `content-rules.css`, `asset-library.css`, `packet-inspector.css`), all `style={{}}` with hex literals replaced by `var(--color-*)` tokens.
- **#21 — Focus traps in modals** [PR #66] — new `useFocusTrap` hook (Tab/Shift+Tab cycles within container, auto-focuses first element on open); applied to `RulePreviewModal`, `AssetPickerModal` inside `ContentRulesPanel`. Escape handler uses stable `useRef` pattern.
- **#49 — Tab groups lack ARIA semantics** [PR #66] — `PacketInspector`, `ManipulationPanel`, `PanelPacketCapture` now have `role="tablist"` / `role="tab"` / `aria-selected` / `aria-controls` / `role="tabpanel"`. Tests updated to query `role="tab"`.
- **#50 — Capture list rows not keyboard-accessible** [PR #66] — `RulePreviewModal` capture list rows: `role="listbox"` container, each row has `role="option"` + `aria-selected` + `tabIndex={0}` + `onKeyDown` Enter/Space handler.

### High doc accuracy (3 of 3)

- **#26 — Test count contradictions across CLAUDE.md / AGENT.md / README.md / ARCHITECTURE.md** [PR #60, bumped in follow-up to PR #61] — locked at 765 (grep `[Fact]/[Theory]` count). Re-bump after each test-adding PR.
- **#27 — Controller list said 19, claimed 20** [PR #60] — added `ProtoSchemas` to enumeration; locked at 20.
- **#28 — Manipulation panel "7 tabs" claim** [PR #60] — corrected to 8 (gRPC Schemas added in Batch 6).

### Trust & safety (1 of 4)

- **#47 — Scanner `MaxConcurrency` defaulted to 100** [PR #63] — default lowered to 25; user values clamped to ≤100.

---

## Recommended next PRs

The original sequencing has been followed; remaining PRs in priority order:

1. **Crash resilience PR** — #8 alone. New `PacketCaptureCheckpointService` (or fold into existing `PacketCaptureService`) that batch-flushes the ring buffer to the `Packets` table on a 1-second cadence. Decide upfront: synchronous write-then-broadcast, or async durable buffer. Affects `PacketCaptureService`, possibly `Packets` table indices, and the integration test suite.

2. **Scope-enforcement + consent gate PR** — bundle #4 + #45. They share the onboarding-wizard UX and the audit-log surface. New `ScanScope` model + repo, CIDR allowlist enforcement at `ScannerService.StartScanAsync` boundary, and an "I am authorized to test this network" acknowledgment step. Largest design surface of all remaining items — consider a design doc first.

3. **Replay/Fuzzer TLS opt-in PR** — #13. DTO field, controller parse, audit-log emit, UI checkbox. Smaller than it looks.

4. **Incomplete-feature bundle (medium)** — #29 (HAR headers), #30 + #31 (config round-trip), #32 (scheduled-scan history), #34 (ProtoParser System.Text.Json), #35 (storage estimates). All small, all safe; consider a single "polish" PR or split per concern.

5. **User-story PRs (medium, high-value)** — pick one per persona-PR:
   - Researcher: #36 (capture-to-curl), #39 (body search), #55 (capture diff)
   - Pen-tester: #38 (replay base URL), #57 (CVSS override), #56 (project concept)
   - Developer: #37 (HAR import)
   - Admin: #41 (UsersTab audit), #42 (backup/restore), #43 (retention runtime API), #40 (in-app alerts)

6. **Per-user data isolation PR** — #48. Touches many controllers; bundle as a single multi-controller PR with a row-level ownership filter helper in `IoTSpy.Storage`.

7. **Plugin signing PR** — #16. Needs a key-mgmt design decision before code; write a short ADR first.

8. **Audit DELETE protection PR** — #46. Needs a retention-vs-immutability decision (immutable archive sink vs. drop pruning); ADR before code.

9. **Protocol coverage** — #44. One PR per protocol; AMQP and RTSP first (highest IoT relevance).

10. **Operational observability** — #51 (OTEL), #52 (more metrics), #53 (Grafana dashboards), #54 (runbook). Each independent; can be picked up in any order.

11. **SSO/OIDC** — #59. Largest single feature in the Low tier; only do this if a customer is asking.

Each item above is sized to fit a focused PR. Avoid bundling across categories — the cleaner the diff, the easier the review.

## Verification commands

After any test-adding PR, re-bump the `[Fact]/[Theory]` grep count in CLAUDE.md, AGENT.md, README.md, ARCHITECTURE.md:

```bash
grep -rE "^\s*\[(Fact|Theory)" --include="*.cs" src/IoTSpy.*.Tests src/IoTSpy.Api.IntegrationTests | wc -l
ls src/IoTSpy.Api/Controllers | wc -l
ls src/IoTSpy.Storage/Migrations/*.cs | grep -vE "(Designer|Snapshot)" | wc -l
grep -rE "\[Http" --include="*.cs" src/IoTSpy.Api/Controllers | wc -l
```
