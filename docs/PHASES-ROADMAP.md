# IoTSpy — Roadmap & Future Phases

This document covers planned future phases (Phase 21+).

See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for all implemented phases 1–16 and 18–21.

> Phase 17 (Non-IP IoT protocol expansion) has been formally archived. See [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md).

---

See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for details.

---

## Roadmap — Phase 22+

---

### Phase 22 — Rich Media & Stream Content Replacement

**Status: IN PROGRESS** — branch `phase22-rich_media_content_replacement`. PR 1 (infra) complete on local working tree (uncommitted, 598 backend tests green). Resume from task table below; see `C:\Users\annag\.claude\plans\let-s-implement-phase-22-kind-quilt.md` for the full implementation plan and design decisions.

**Goal:** Complete the content replacement engine to correctly handle binary content types (images, video, audio) and SSE/NDJSON stream mocking with local files. Primary use case: replace advertising images, tracking pixels, video ads, and live ad-feed streams with researcher-controlled content.

**Background:** The existing `ContentReplacer` (`IoTSpy.Manipulation`) buffers response bodies as strings and rewrites headers. This works for JSON/HTML/text but fails for binary types because:
- Binary content converted through string encoding becomes corrupted
- No `Content-Length` recalculation for binary assets
- No HTTP range request (`Range:` / `206 Partial Content`) support required by video players
- SSE streams (`text/event-stream`) can't be replaced with a local file-based mock feed
- The `ReplacementRulesEditor.tsx` UI has no media preview or type-specific upload flow

| # | Task | Priority | Status | Details |
|---|---|---|---|---|
| 22.1 | Binary-safe replacement pipeline | High | ✅ Done (uncommitted) | `IResponseBodySource` + `HttpMessage.ResponseBodySource` + `FileStreamBodySource`; `ContentReplacer.ApplyAsync` now streams binary files through the new channel instead of base64-encoding into the UTF-8 string body. Also fixed a latent bug where `ExtractContentType` / `UpdateContentType` only understood JSON-dict headers, silently missing real proxy CRLF headers. |
| 22.2 | Correct MIME type & Content-Length headers | High | ✅ Done (uncommitted) | Proxy's new `ApplyBodySourceHeaders` helper strips `Transfer-Encoding` + `Content-Encoding`, overwrites `Content-Type` + `Content-Length` from the body source, and appends source-declared `ExtraHeaders`. |
| 22.3 | HTTP range request passthrough for video | Medium | ✅ Done (uncommitted) | `RangeHelper` parses `bytes=S-E`/`bytes=S-`/`bytes=-N` (rejects multi-range); `RangeSlicedBodySource` serves 206 with `Content-Range` + `Accept-Ranges: bytes`. |
| 22.4 | SSE stream mock from local file | Medium | ⏸️ TODO | New `ContentReplacementAction.MockSseStream`; `SseStreamBodySource` reads `.sse`/`.ndjson`, flushes per event with configurable delay + loop; needs EF migration adding `SseInterEventDelayMs int?` + `SseLoop bool?` columns to `ContentReplacementRules`. Emits `Connection: close` so existing proxy keep-alive loop terminates cleanly after final event. |
| 22.5 | Tracking pixel / 1×1 GIF built-in | Medium | ✅ Done (uncommitted) | `ContentReplacementAction.TrackingPixel` + `TrackingPixelBodySource.Instance` singleton (43-byte GIF89a). |
| 22.6 | Asset library UI improvements | Medium | ⏸️ TODO | New `AssetLibrary`, `AssetCard`, `AssetPickerButton`, `SseConfigFields`, `RulePreviewModal` components; drag-drop via native HTML5 DnD (no new deps); needs new `[AllowAnonymous] GET /api/apispec/assets/{filename}/content` route serving `PhysicalFile(..., enableRangeProcessing: true)` so `<img>`/`<video>` tags can load previews without the JWT header. |
| 22.7 | Rule test/preview mode | Medium | ⏸️ TODO | `POST /api/apispec/{specId}/rules/{ruleId}/preview` — accepts either a synthetic HttpMessage DTO or a captureId; new `ReplacementPreviewService` renders via in-memory `MemoryStream`; returns status, headers, text-body or base64-body, and a `Warnings` list (missing file, MIME mismatch, TE/CE stripped, range unparseable). |
| 22.8 | ContentReplacer streaming for large assets | Low | ✅ Done (uncommitted) | Unified with 22.1 per design decision — every binary `ReplaceWithFile` flows through `FileStreamBodySource`, regardless of size. No threshold branching. |
| 22.9 | Tests & documentation | Medium | 🚧 Partial | 24 new tests added: `RangeHelperTests` (10), `BodySourcesTests` (4), `ContentReplacerBinaryTests` (6), plus async/CRLF-header regression coverage. Still need: SSE replay tests, `ReplacementPreviewService` tests, frontend Vitest coverage, and docs updates (`PHASES-COMPLETED.md` move + `CODE-PATTERNS.md` entry on how to add a new `IResponseBodySource`). |

**Backend:** `IoTSpy.Manipulation` — `ContentReplacer.cs` (binary pipeline), `SseStreamReplacer.cs` (new); `IoTSpy.Core` — add `MockSseStream` and `TrackingPixel` to `ContentReplacementAction` enum; `IoTSpy.Api/Controllers/ManipulationController.cs` — preview endpoint. **Frontend:** `ReplacementRulesEditor.tsx` (media preview, SSE rule UI), `frontend/src/api/manipulation.ts` (preview API call). **No new EF migration required** — asset storage and rule schema unchanged.

#### Resume notes (2026-04-22)

**Current state:** branch `phase22-rich_media_content_replacement`, working tree dirty with PR 1 changes (see `git status`). All 598 backend tests pass. Nothing committed yet.

**Corrections discovered during implementation:**
- Replacement rule CRUD lives on `ApiSpecController` (`/api/apispec/{specId}/rules`), **not** `ManipulationController` as the original task 22.7 line implied. The preview endpoint should be `POST /api/apispec/{specId}/rules/{ruleId}/preview`.
- "No new EF migration required" was aspirational but not achievable once SSE config needed persistence. Decision: add `SseInterEventDelayMs int?` + `SseLoop bool?` nullable columns via a new migration when shipping 22.4.
- Pre-existing latent bug fixed as a freebie: `ExtractContentType` in `ContentReplacer` silently failed against real proxy CRLF headers (only parsed JSON-dict form), so `ContentType` rules never matched real traffic. Both `ExtractContentType` and `UpdateContentType` now handle both formats.

**To pick up next session:** start with task 22.4 (SSE + migration), then 22.7 (preview endpoint + `[AllowAnonymous]` asset-content route), then 22.6 (frontend). Plan file at `C:\Users\annag\.claude\plans\let-s-implement-phase-22-kind-quilt.md` has the design details and shipping order.

---

## Future Enhancement Areas (Phases 23+)

Beyond Phases 21–22, potential candidates based on the codebase audit and Phase 17:

### API & Backend Polish
- **Bulk operations** — Batch enable/disable rules, cancel-all scans, bulk capture delete by filter; reduces multi-step workflows to single calls
- **Export everywhere** — Fuzzer results, scan findings, and ruleset export as portable JSON; enables sharing configurations across environments
- **Consistent pagination** — All list endpoints return `{ items, total, pages }`; currently only captures and scanner jobs do
- **Configuration change audit trail** — Before/after diffs on rule, spec, policy, and breakpoint changes; extends `AuditEntry` with `OldValue`/`NewValue` fields
- **Manipulation rule import/export** — Import/export rulesets as self-contained JSON bundles (rules + breakpoints + replacement rules + API spec); useful for sharing research setups

### Scanner & Anomaly
- **Concurrent multi-device scanning** — Scan queue with configurable parallelism; currently single-device sequential
- **Scan findings correlation** — Group findings by vulnerability class, CVE, affected service; currently raw list only
- **Custom anomaly rules** — Declarative anomaly rules (similar to the manipulation rules engine) to flag specific traffic patterns; replaces purely statistical Welford baseline
- **Behavioral fingerprinting** — Persistent per-device baseline across proxy restarts; detect changes in device communication patterns over time

### Frontend Usability
- **Keyboard shortcuts** — `Delete` on selected row, `Ctrl+S` to save, `Escape` to close modals; global `useKeyboardShortcuts` hook
- **Security headers** — CSP, HSTS, `X-Frame-Options`, `X-Content-Type-Options` middleware in `Program.cs` (30-minute task)
- **Virtual scrolling** — Improve capture list performance with 100k+ rows
- **React Query / SWR migration** — Replace manual `useState`/`useEffect` fetch cycles with a caching/deduplication layer

### Protocol Decoder Depth
- **AMQP 1.0 decoder** — Message broker protocol increasingly used in IoT (Phase 17.1)
- **RTSP/RTP for IP cameras** — Detect unauthenticated camera streams (Phase 17.2)
- **DNS DNSSEC / DoH / DoT** — Detect encrypted DNS, validate DNSSEC chains
- **CoAP resource discovery** — `.well-known/core` parsing, Block-wise transfer, Observe option
- **gRPC `.proto` schema mapping** — Upload `.proto` files to resolve field names in captured gRPC messages

### Longer-Horizon
- **Offline mode** — Cache captures, rules, and playback without network connectivity
- **Mobile app** — Native iOS/Android for field reconnaissance and live monitoring
- **Machine learning anomaly detection** — Replace Welford statistical baseline with trained ML models
- **Custom protocol decoders** — User-defined binary protocol parsers via plugin scripting
- **Enterprise features** — RBAC refinement, data classification, compliance reporting (GDPR, HIPAA), encryption at rest
- **Multi-tenant** — Organizational namespaces, resource quotas, billing integration
