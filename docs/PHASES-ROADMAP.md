# IoTSpy — Roadmap & Future Phases

This document covers proposed future work (Phase 17, Phase 21+) and long-term enhancement candidates.

See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for all implemented phases 1–16 and 18–20.

---

## Phase 16 — Deployment & Operations ✅ COMPLETE

**Goal:** Make IoTSpy production-ready for team deployments with proper TLS, container orchestration, and operational tooling.

| # | Task | Status | Details |
|---|---|---|---|
| 16.1 | Kestrel HTTPS for the API | ✅ | `HttpsCertificateHolder` singleton + `CertesLetsEncryptService`; cert file or Let's Encrypt via `Certes`; HTTPS on port 5001 |
| 16.2 | Kubernetes Helm chart | ✅ | `deploy/helm/iotspy/` — Chart.yaml, values.yaml, Deployment, Service, ConfigMap, Secret, Ingress, PVC, HPA, ServiceAccount |
| 16.3 | Docker Compose improvements | ✅ | `docker-compose.prod.yml` with Postgres 17, pgAdmin, Traefik v3 reverse proxy with automatic TLS |
| 16.4 | Plugin system for protocol decoders | ✅ | `IPluginDecoder` + `IPluginRegistry` in Core; `PluginLoaderService` via `AssemblyLoadContext`; `PluginsController` REST API |
| 16.5 | LDAP / SAML SSO | — | Deprioritized (Low) |
| 16.6 | Metrics endpoint (Prometheus) | ✅ | `/metrics` via `prometheus-net.AspNetCore`; `IoTSpyMetrics` with proxy requests, scan durations, anomaly alerts, capture queue depth |
| 16.7 | Alerting integrations | ✅ | Slack (blocks API), Teams (MessageCard), PagerDuty Events API v2; severity threshold filtering |
| 16.8 | Distributed mode (multi-node) | — | Deprioritized (Low) |
| 16.9 | NAS APK package support | ✅ | `docker-compose.nas.yml`; `deploy/nas/asustor/` APK (apkg.info, lifecycle scripts, webman CGI); `scripts/build-asustor-apk.sh`; multi-arch `release.yml` CI publishing to GHCR |

---

## Proposed Phase (17) — Deprioritized

These phases were proposed in the original roadmap but deprioritized. They remain valid candidates for future implementation if team needs change.

### Phase 17 — Protocol Expansion (Non-IP IoT)

**Goal:** Extend coverage to wireless IoT protocols beyond TCP/IP networking.

| # | Task | Priority | Details |
|---|---|---|---|
| 17.1 | AMQP 1.0 decoder | Medium | Decode AMQP frames captured via transparent proxy or PCAP import; surface messages alongside MQTT |
| 17.2 | RTSP/RTP for IP cameras | Medium | Detect RTSP `DESCRIBE`/`SETUP`/`PLAY` sequences; capture SDP metadata; flag unauthenticated streams |
| 17.3 | Matter/Thread protocol support | Low | Passive decode of Matter commissioning and cluster messages; Thread network topology mapping (requires USB border router) |
| 17.4 | Zigbee passive capture | Low | USB Zigbee sniffer integration (e.g. RZUSBSTICK / CC2531) via `libusb`; decode ZDP/ZCL frames |
| 17.5 | Bluetooth LE advertisement decode | Low | HCI socket or BlueZ integration; decode BLE advertisements from IoT beacons; map to known vendor profiles (Eddystone, iBeacon, Tile) |
| 17.6 | Z-Wave frame decode | Low | Serial port integration with a Z-Wave controller; decode Z-Wave frames and map to device/command class |

---

## Roadmap — Phase 21+

### Phase 21 — Passive Proxy Mode (Toggle-able Observation)

**Goal:** Enable lightweight traffic monitoring with optional persistence, supporting API discovery, compliance auditing, and low-resource deployments without interception or filtering overhead.

| # | Task | Priority | Details |
|---|---|---|---|
| 21.1 | Passive mode enum & ProxySettings toggle | High | Add `ProxyMode.Passive` to `InterceptionMode` enum; add `IsPassive` boolean to `ProxySettings`; configuration via UI settings modal |
| 21.2 | Pass-through proxy pipeline | High | When passive: skip all `RulesEngine`, manipulation, anomaly detection, and breakpoint script execution; stream raw packets/requests directly to `PacketCaptureHub` without queuing for database |
| 21.3 | In-memory session capture | Medium | Capture traffic into in-memory buffers during passive session; populate UI in real-time; optionally persist to DB via "Save Session" action; discard on proxy stop if not saved |
| 21.4 | Session save/load | Medium | `POST /api/captures/save-session` — snapshot in-memory captures to database as a named investigation session; `GET /api/captures/load-session/{id}` to retrieve saved session; persist session metadata (timestamp, device, entry count) |
| 21.5 | Lightweight resource footprint | Medium | Eliminate database chatter in passive mode (no INSERT on every request); measure memory overhead of in-memory buffers (configurable max size, e.g. 10k captures per session) |
| 21.6 | API discovery visualization | Medium | New "Passive Capture Summary" panel in UI: endpoint frequency heatmap (GET /api/users: 50 requests, POST /api/auth: 20 requests), response code distribution, top domains/hostnames, suggested rule patterns |
| 21.7 | Passive mode UI indicator | Low | Show "🔍 Passive Mode" badge in header when proxy is running in passive mode; distinguish from active interception mode visually |
| 21.8 | Tests & documentation | Medium | Unit tests for passive pipeline (verify no rules/scripts execute); integration tests for session save/load; docs on use cases (compliance auditing, API learning, bandwidth-limited deployments) |

**Backend:** `IoTSpy.Proxy` — `PassivePipelineFilter` (skip manipulation stack); `IoTSpy.Core` — `CaptureSession` model; `IoTSpy.Storage` — `CaptureSessions` DbSet + migration. **Frontend:** passive mode indicator, session save dialog, endpoint frequency heatmap visualization.

---

## Future Enhancement Areas (Phases 22+)

Beyond Phase 21, potential candidates include Phases 16–17 (deployment, protocol expansion), or new features based on user needs and feedback. Examples:

- **Offline mode** — Cache captures, rules, and playback without network connectivity
- **Mobile app** — Native iOS/Android for field reconnaissance and live monitoring
- **Machine learning anomaly detection** — Replace Welford statistical baseline with trained ML models
- **Custom protocol decoders** — User-defined binary protocol parsers via scripting
- **Enterprise features** — RBAC refinement, data classification, compliance reporting (GDPR, HIPAA), encryption at rest
- **Multi-tenant** — Organizational namespaces, resource quotas, billing integration
