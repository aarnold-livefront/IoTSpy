# IoTSpy — Roadmap & Future Phases

This document covers planned future work.

See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for all completed work including phases 1–16, 18–22, API & Backend Polish, and Frontend Usability enhancements.

> Phase 17 (Non-IP IoT protocol expansion) has been formally archived. See [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md).

---

## Future Enhancement Areas

### Scanner & Anomaly
- **Concurrent multi-device scanning** — Scan queue with configurable parallelism; currently single-device sequential
- **Scan findings correlation** — Group findings by vulnerability class, CVE, affected service; currently raw list only
- **Custom anomaly rules** — Declarative anomaly rules (similar to the manipulation rules engine) to flag specific traffic patterns; replaces purely statistical Welford baseline
- **Behavioral fingerprinting** — Persistent per-device baseline across proxy restarts; detect changes in device communication patterns over time

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
