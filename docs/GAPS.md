# IoTSpy — Known Gaps & Technical Debt

This document tracks remaining gaps, known limitations, and technical debt. Items are prioritized by severity and impact on usability or production readiness.

---

## Active Gaps

| Gap | Description | Severity | Status | Notes |
|---|---|---|---|---|
| No HTTPS for the API itself | The API serves on plain HTTP; TLS termination is assumed external (reverse proxy, k8s ingress) | Low | Open | See Phase 16.1 for future work |
| No Bluetooth/Zigbee/Z-Wave | IoT protocols beyond IP-based networking are not supported | Low | Open | See Phase 17 for future work |
| Customization of CA Certificate | Add support for customizing Name, Organization, and other properties on the proxy CA certificate | Medium | Open | UX enhancement; currently uses hardcoded values |

---

## Resolved Items

### Phase 20 (Admin UI & Body Viewer)
- ~~Stray draft components in `src/IoTSpy.React/`~~ — `PacketAnalysisView.tsx` and `NetworkDeviceSelector.tsx` deleted; unique functionality (`showOnlyErrors` / `showOnlyRetransmissions` filters) migrated into `PacketListFilterable.tsx`

### Phase 11 (Multi-user & UX)
- ~~No Core model tests~~ — `IoTSpy.Core.Tests` project added with 30+ model default/enum tests
- ~~No multi-user support~~ — Multi-user RBAC with `User` model, `UserRole` enum (Admin/Operator/Viewer), user management endpoints
- ~~Dashboard not responsive~~ — Responsive CSS with mobile breakpoints (480px, 768px, 1024px)
- ~~TLS passthrough/SSL strip untested~~ — `TlsClientHelloParserTests` (13 tests), `TlsServerHelloParserTests` (11 tests), `SslStripServiceTests` (14 tests)

---

## Technical Debt Items

### Moderate Priority

| Item | Description | Effort | Impact |
|---|---|---|---|
| Upgrade to .NET 11 | .NET 11 releases Spring 2026; plan for compatibility review and security updates | Medium | Maintenance |
| Frontend dependency updates | React 20 and TypeScript 5.7+ may have breaking changes; monitor release notes | Medium | Stability |
| Serilog configuration | Move from code-based to JSON config file for easier environment-specific logging | Low | Operational |

### Low Priority

| Item | Description | Effort | Impact |
|---|---|---|---|
| SignalR reconnection logic | Add exponential backoff and auto-reconnect for unstable networks | Medium | UX |
| PCAP file compression | Support `.pcapng.gz` upload for bandwidth-constrained networks | Low | UX |
| Rule engine optimization | Profile rule matching performance under 100k+ captures; consider regex caching | Low | Performance |
| Cache invalidation patterns | Review `ApiSpecMockService` dual-layer cache for correctness under concurrent updates | Low | Correctness |

---

## Design Assumptions

These assumptions should be revisited if requirements change:

1. **Single-instance proxy per deployment** — Assumes one API process and one pair of proxy servers (explicit + transparent). Horizontal scaling requires Redis backplane (Phase 16.8).
2. **SQLite for development, Postgres for production** — Current schema and migrations support both, but testing is primary on SQLite.
3. **Browser-based dashboard** — Assumes web client; no native mobile apps or CLI tools planned.
4. **JWT + API key auth only** — No SAML/LDAP support yet (Phase 16.5).
5. **In-memory anomaly detector** — Resets on restart; no persistent baseline learning.
6. **Request-scoped repositories** — Each HTTP request gets a fresh EF Core DbContext; not suitable for long-running background tasks without scope management.

---

## Testing Gaps

| Component | Current Status | Target | Notes |
|---|---|---|---|
| End-to-end frontend tests | Vitest component tests; no Cypress/Playwright | Full E2E suite | Would require Docker orchestration for backend |
| Load testing | Baseline not established | Establish proxy throughput/latency limits | Critical for production planning |
| Chaos engineering | None | Resilience scenarios (proxy crash, DB unavailable) | Validates graceful degradation |
| Security fuzzing | Limited | AFL/libFuzzer on parsers (MQTT, DNS, CoAP) | Could discover parsing edge cases |

---

## Documentation Gaps

| Area | Status | Notes |
|---|---|---|
| Operator runbook | Basic | Need: troubleshooting guide, log interpretation, health check procedures |
| API reference completeness | Good | All endpoints documented in README; Scalar docs (dev-only) |
| Deployment guides | Minimal | Only Docker Compose (dev); need: Kubernetes, bare-metal, systemd |
| Performance tuning | None | Need: connection pool sizing, rate limit tuning, cache strategies |
| Extension guides | Partial | How to add protocol decoders, manipulation rules, AI providers covered; custom UI components not covered |

---

## Performance Considerations

### Known Hotspots

1. **Rule evaluation** — 100+ rules on 10k captures → full table scan each time; consider indexing or caching
2. **JSON schema inference** — Recursive object traversal on large payloads; could cache schema per (host, path, method) tuple
3. **Packet capture ring buffer** — 10k packet limit; large bursts may drop packets; consider dynamic sizing
4. **Frontend capture list** — Virtual scrolling could improve UI responsiveness with 100k+ rows

### Optimization Opportunities

- Implement request deduplication in proxy pipeline (same URL + headers → single capture)
- Add rule caching layer with invalidation on CRUD operations
- Profile and optimize `ContentReplacer.ApplyAsync` for large response bodies (> 10 MB)
- Consider bloom filters for anomaly detection false-positive filtering

---

## Security Audit Notes

- Root CA private key stored in local database (not HSM-protected); acceptable for research tool, not production
- API endpoints require Bearer token or API key; no rate limiting on `/api/auth/setup` (mitigated by one-time-only check)
- TLS MITM disabled in passthrough mode (`CaptureTls=false`); metadata extraction only
- SSL stripping requires explicit `SslStrip=true` flag; off by default
- Audit log immutable in design but not enforced at DB level; could add write-once table constraint

---

## Suggestions for Next Contributors

1. **Start with Phase 21** (Passive Mode) — Well-scoped, clear requirements, minimal API changes
2. **Or Phase 16.1** (API HTTPS) — Short PR, improves production readiness
3. **Avoid Phase 17.3+** (Zigbee/BLE) — Requires specialized hardware; hard to test in CI
4. **Keep `IoTSpy.Core` dep-free** — New models/interfaces should go there; no infrastructure references

See [AGENT-NOTES.md](AGENT-NOTES.md) for session setup and testing instructions.
