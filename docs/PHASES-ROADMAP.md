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

**Goal:** Complete the content replacement engine to correctly handle binary content types (images, video, audio) and SSE/NDJSON stream mocking with local files. Primary use case: replace advertising images, tracking pixels, video ads, and live ad-feed streams with researcher-controlled content.

**Background:** The existing `ContentReplacer` (`IoTSpy.Manipulation`) buffers response bodies as strings and rewrites headers. This works for JSON/HTML/text but fails for binary types because:
- Binary content converted through string encoding becomes corrupted
- No `Content-Length` recalculation for binary assets
- No HTTP range request (`Range:` / `206 Partial Content`) support required by video players
- SSE streams (`text/event-stream`) can't be replaced with a local file-based mock feed
- The `ReplacementRulesEditor.tsx` UI has no media preview or type-specific upload flow

| # | Task | Priority | Details |
|---|---|---|---|
| 22.1 | Binary-safe replacement pipeline | High | Refactor `ContentReplacer.ApplyAsync` to use `ReadOnlyMemory<byte>` / `Stream` instead of `string` for `ReplaceWithFile` and `ReplaceWithUrl` actions; bypass all string encoding for `image/*`, `video/*`, `audio/*`, `application/octet-stream` content types |
| 22.2 | Correct MIME type & Content-Length headers | High | When replacing with a local asset file: set `Content-Type` from the asset's stored MIME type (not the original response); recalculate and set `Content-Length`; strip `Transfer-Encoding: chunked` and `Content-Encoding` if present |
| 22.3 | HTTP range request passthrough for video | Medium | Detect `Range:` request header; slice the local asset file accordingly; respond with `206 Partial Content` + `Content-Range` + `Accept-Ranges: bytes`; required for video `<video>` elements and most media players |
| 22.4 | SSE stream mock from local file | Medium | New `ContentReplacementAction.MockSseStream`; read a local `.sse` or `.ndjson` file from `./data/assets/`; replay events with configurable inter-event delay (ms); loop or play-once mode; correct `text/event-stream` headers; supports replacing live ad-feed SSE streams with researcher-controlled event sequences |
| 22.5 | Tracking pixel / 1×1 GIF built-in | Medium | New `ContentReplacementAction.TrackingPixel`; respond with a hardcoded 1×1 transparent GIF (43 bytes) without needing an asset file upload; ideal for blocking/replacing tracking beacons silently |
| 22.6 | Asset library UI improvements | Medium | `ReplacementRulesEditor.tsx`: add media type badge and inline preview (thumbnail for images, duration for video/audio, event count for SSE files); drag-and-drop multi-file upload with MIME validation; distinguish text/binary/stream asset categories |
| 22.7 | Rule test/preview mode | Medium | `POST /api/manipulation/replacement-rules/{id}/preview` — apply a single replacement rule against a synthetic or captured request/response without proxying real traffic; returns the modified response for inspection; surfaces mismatches (wrong MIME type, encoding issues) before rules go live |
| 22.8 | ContentReplacer streaming for large assets | Low | For assets > 4 MB: stream from file directly into the proxy response pipeline instead of loading into memory; prevents OOM on large video file replacements |
| 22.9 | Tests & documentation | Medium | Binary round-trip tests (PNG, MP4, WebM); SSE replay tests; range request tests; update content replacement documentation with image/video/audio/SSE examples and gotchas |

**Backend:** `IoTSpy.Manipulation` — `ContentReplacer.cs` (binary pipeline), `SseStreamReplacer.cs` (new); `IoTSpy.Core` — add `MockSseStream` and `TrackingPixel` to `ContentReplacementAction` enum; `IoTSpy.Api/Controllers/ManipulationController.cs` — preview endpoint. **Frontend:** `ReplacementRulesEditor.tsx` (media preview, SSE rule UI), `frontend/src/api/manipulation.ts` (preview API call). **No new EF migration required** — asset storage and rule schema unchanged.

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
