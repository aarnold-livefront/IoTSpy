# IoTSpy — Implementation Plan (Minimal Index)

This document is a navigation hub for all planning and implementation documentation.

**→ [Start here: PLAN-INDEX.md](PLAN-INDEX.md)** for full navigation, links to all guides, and role-based recommendations.

---

## Quick Links

| Document | Purpose |
|---|---|
| **[PLAN-INDEX.md](PLAN-INDEX.md)** | Main navigation hub (start here) |
| **[AGENT-NOTES.md](AGENT-NOTES.md)** | Quick setup, testing, session handoff |
| **[PHASES-COMPLETED.md](PHASES-COMPLETED.md)** | All implemented phases (1-15, 18-20) |
| **[PHASES-ROADMAP.md](PHASES-ROADMAP.md)** | Future phases & deprioritized work |
| **[DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)** | Architecture, naming, patterns |
| **[GAPS.md](GAPS.md)** | Known issues & technical debt |
| **[architecture.md](architecture.md)** | Full technical architecture |
| **[../README.md](../README.md)** | Features, quick start, API reference |

---

## Current Status

- **Phases complete:** 1–15, 18–20 ✅
- **Backend tests:** 517 (all passing)
- **Controllers:** 16 REST + 3 SignalR hubs
- **Protocols:** HTTP/HTTPS, MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB
- **Next phase:** 21 (Passive Proxy Mode) — see [PHASES-ROADMAP.md](PHASES-ROADMAP.md)

---

## For Claude Code Sessions

**→ [AGENT-NOTES.md](AGENT-NOTES.md)** — Contains setup, testing, feature checklist, debugging scenarios.

Start with:
```bash
export Auth__JwtSecret="your-32-char-secret"
dotnet build
dotnet test
dotnet run --project src/IoTSpy.Api
```

---

## For New Contributors

1. Read [PLAN-INDEX.md](PLAN-INDEX.md) (navigation)
2. Follow [AGENT-NOTES.md](AGENT-NOTES.md) (setup & testing)
3. Review [GAPS.md](GAPS.md) (what to work on)
4. Check [PHASES-COMPLETED.md](PHASES-COMPLETED.md) for context

---

## Key Project Information

**Location:** `docs/` directory  
**Architecture:** [architecture.md](architecture.md)  
**Features:** [../README.md](../README.md)  
**Skills:** [../CLAUDE.md](../CLAUDE.md) for project-specific commands  

All documentation is cross-linked for easy navigation.
