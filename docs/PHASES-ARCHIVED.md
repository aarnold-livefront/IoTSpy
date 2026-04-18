# IoTSpy — Archived / Deprecated Phases

This document holds phases that have been formally deprioritized and are unlikely to be implemented in the near term. They are preserved here for reference.

See [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for active future phases.

---

## Phase 17 — Protocol Expansion (Non-IP IoT) ⏸️ Archived

**Goal:** Extend coverage to wireless IoT protocols beyond TCP/IP networking.

**Why deprioritized:** These protocols require physical USB hardware (Zigbee sticks, Z-Wave controllers, Bluetooth HCI access) and platform-specific drivers that are difficult to test in CI. The core use-case coverage (HTTP/HTTPS MITM, MQTT, CoAP, DNS, WebSocket, gRPC, Modbus) satisfies the majority of IoT research scenarios without specialized hardware. Phase 17 tasks are candidates for community contributions when the hardware ecosystem is better established.

| # | Task | Priority | Details |
|---|---|---|---|
| 17.1 | AMQP 1.0 decoder | Medium | Decode AMQP frames captured via transparent proxy or PCAP import; surface messages alongside MQTT |
| 17.2 | RTSP/RTP for IP cameras | Medium | Detect RTSP `DESCRIBE`/`SETUP`/`PLAY` sequences; capture SDP metadata; flag unauthenticated streams |
| 17.3 | Matter/Thread protocol support | Low | Passive decode of Matter commissioning and cluster messages; Thread network topology mapping (requires USB border router) |
| 17.4 | Zigbee passive capture | Low | USB Zigbee sniffer integration (e.g. RZUSBSTICK / CC2531) via `libusb`; decode ZDP/ZCL frames |
| 17.5 | Bluetooth LE advertisement decode | Low | HCI socket or BlueZ integration; decode BLE advertisements from IoT beacons; map to known vendor profiles (Eddystone, iBeacon, Tile) |
| 17.6 | Z-Wave frame decode | Low | Serial port integration with a Z-Wave controller; decode Z-Wave frames and map to device/command class |

**Revive criteria:** Hardware abstraction layer available, CI emulation or mock driver support added, or community contributor with physical test hardware.
