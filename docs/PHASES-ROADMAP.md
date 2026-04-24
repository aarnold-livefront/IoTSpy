# IoTSpy — Roadmap & Future Phases

This document covers planned future updates. 

See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for all implemented phases 1–16 and 18–22+.

> Phase 17 (Non-IP IoT protocol expansion) has been formally archived. See [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md).

---

## Future Enhancement Areas (Phases 23+)

Beyond Phase 22 (including post-phase content rules decoupling and Manipulation UI cleanup), potential candidates based on the codebase audit and Phase 17:

### API & Backend Polish
- **Bulk operations** — Batch enable/disable rules, cancel-all scans, bulk capture delete by filter; reduces multi-step workflows to single calls
- **Export everywhere** — See [Export Everywhere plan](PLAN-EXPORT-EVERYWHERE.md). Capture → streaming asset (`.sse`/`.ndjson`), fuzzer results, scan findings, and ruleset bundle export as portable JSON; enables sharing configurations across environments
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
