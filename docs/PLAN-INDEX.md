# IoTSpy — Planning & Documentation Index

This is the primary navigation hub for all project planning, architecture, and implementation guides. Each document is optimized for a specific use case.

---

## 📋 Navigation by Purpose

### For First-Time Contributors
1. Start here: **[AGENT-NOTES.md](AGENT-NOTES.md)** — Quick setup, testing, common gotchas
2. Then read: **[PHASES-COMPLETED.md](PHASES-COMPLETED.md)** — What's been built (summary)
3. Finally: **[GAPS.md](GAPS.md)** — What's left to do

### For Architecture Questions
→ **[DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)** — Why we chose X over Y, implementation notes, patterns

### For Project Status
→ **[README.md](../README.md)** — Feature list, quick start, API reference

### For Technical Deep Dives
→ **[docs/ARCHITECTURE.md](ARCHITECTURE.md)** — Full architecture spec, data flow, project structure

### For Future Planning
→ **[PHASES-ROADMAP.md](PHASES-ROADMAP.md)** — Future enhancement areas, deprioritized Phase 17, long-term vision

### For Known Issues
→ **[GAPS.md](GAPS.md)** — Technical debt, testing gaps, performance hotspots

---

## 📊 Current Status (at a glance)

| Metric | Value |
|---|---|
| **Phases complete** | 1–16, 18–22 + API & Backend Polish + Frontend Usability |
| **Backend tests** | 644 (all passing) |
| **Controllers** | 19 REST + 3 SignalR hubs |
| **Migrations** | 19 |
| **Protocols supported** | HTTP/HTTPS, MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB, Telemetry |
| **Proxy modes** | 3 (explicit, gateway, ARP spoof) |
| **Auth** | Multi-user RBAC (Admin/Operator/Viewer) |
| **Frontend** | Vite 6 + React 19 + TypeScript (82+ components) |

---

## 📁 Document Structure

### Primary Guides

| Document | Purpose | Audience | Length |
|---|---|---|---|
| **[AGENT-NOTES.md](AGENT-NOTES.md)** | Quick setup, testing, session handoff | Claude Code agents | ~400 lines |
| **[SKILLS-PLUGINS.md](SKILLS-PLUGINS.md)** | When/how to use skills and plugins | Claude Code agents | ~300 lines |
| **[PHASES-COMPLETED.md](PHASES-COMPLETED.md)** | All completed phases (1–16, 18–22, API & Backend Polish, Frontend Usability) | Contributors, architects | ~600 lines |
| **[PHASES-ROADMAP.md](PHASES-ROADMAP.md)** | Future enhancement areas (no numbered phases remaining) | Product managers, strategists | ~100 lines |
| **[PHASES-ARCHIVED.md](PHASES-ARCHIVED.md)** | Archived / formally deprioritized phases (Phase 17) | Reference only | ~50 lines |
| **[GAPS.md](GAPS.md)** | Known issues, API gaps, security hardening, tech debt | QA, tech leads | ~350 lines |
| **[DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)** | Architecture decisions, naming, patterns | Architects, senior engineers | ~400 lines |
| **[CODE-PATTERNS.md](CODE-PATTERNS.md)** | Protocol decoder patterns, common code conventions | Backend engineers | ~200 lines |
| **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** | Common debugging scenarios and fixes | All contributors | ~150 lines |
| **[QUICK-REF.md](QUICK-REF.md)** | Quick command and API reference cheat sheet | All contributors | ~100 lines |

### Reference Guides

| Document | Purpose |
|---|---|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Full technical architecture (in-depth) |
| **[README.md](../README.md)** | Feature list, quick start, API reference |
| **[CLAUDE.md](../CLAUDE.md)** | Project-specific Claude Code skills & commands |

---

## 🚀 Quick Links

### By Role

**Backend Engineer**
- Phase details: [PHASES-COMPLETED.md](PHASES-COMPLETED.md)
- Architecture: [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)
- Testing: [AGENT-NOTES.md](AGENT-NOTES.md) → "Testing Before Commit"
- Add feature checklist: [AGENT-NOTES.md](AGENT-NOTES.md) → "Adding Features: Checklist"

**Frontend Engineer**
- Phase 18-20 details: [PHASES-COMPLETED.md](PHASES-COMPLETED.md) → "Phase 18-20"
- Component setup: [AGENT-NOTES.md](AGENT-NOTES.md) → "Quick Setup"
- Dev workflow: [AGENT-NOTES.md](AGENT-NOTES.md) → "Hot Reload / Development Workflow"

**DevOps / Deployment**
- Phase 16 (complete): [PHASES-COMPLETED.md](PHASES-COMPLETED.md) → "Phase 16 — Deployment & Operations"
- Configuration: [AGENT-NOTES.md](AGENT-NOTES.md) → "Configuration Quick Reference"

**QA / Testing**
- Test strategy: [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) → "Testing Strategy"
- Testing gaps: [GAPS.md](GAPS.md) → "Testing Gaps"
- Test setup: [AGENT-NOTES.md](AGENT-NOTES.md) → "Testing Before Commit"

**Product / Strategist**
- Roadmap: [PHASES-ROADMAP.md](PHASES-ROADMAP.md)
- Design decisions: [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)
- Current gaps: [GAPS.md](GAPS.md)

---

## 📚 Document Sizes (optimized for token usage)

```
PLAN-INDEX.md           ~250 lines (this file)
AGENT-NOTES.md          ~400 lines (setup + checklist)
PHASES-COMPLETED.md     ~600 lines (all phases incl. polish + usability)
PHASES-ROADMAP.md       ~100 lines (future work — no numbered phases)
PHASES-ARCHIVED.md       ~50 lines (Phase 17, formally archived)
GAPS.md                 ~350 lines (issues + API gaps + security hardening)
DESIGN-DECISIONS.md     ~400 lines (patterns + rationale)
CODE-PATTERNS.md        ~200 lines (protocol decoder + code conventions)
TROUBLESHOOTING.md      ~150 lines (debugging scenarios)
QUICK-REF.md            ~100 lines (command + API cheat sheet)
ARCHITECTURE.md         ~700 lines (full architecture)
README.md               ~590 lines (features + API ref)
CLAUDE.md               ~100 lines (skills + commands)
```

**Total:** ~4,000 lines across 13 files, organized for efficient lookup without requiring full file read.

> Line counts are approximate and may drift as content evolves.

---

## 🔍 Finding What You Need

### "How do I...?"

| Question | Answer |
|---|---|
| Set up my dev environment? | [AGENT-NOTES.md](AGENT-NOTES.md#quick-setup) |
| Run tests? | [AGENT-NOTES.md](AGENT-NOTES.md#testing-before-commit) |
| Add a new feature? | [AGENT-NOTES.md](AGENT-NOTES.md#adding-features-checklist) |
| Create an EF Core migration? | [AGENT-NOTES.md](AGENT-NOTES.md#ef-core-migrations) |
| Use a skill or plugin? | [SKILLS-PLUGINS.md](SKILLS-PLUGINS.md#when-to-use-each-skill) |
| Know when to use `/dotnet-engineer`? | [SKILLS-PLUGINS.md](SKILLS-PLUGINS.md#dotnet-engineer) |
| Know when to use `/security-code-review`? | [SKILLS-PLUGINS.md](SKILLS-PLUGINS.md#security-code-review) |
| Add a protocol decoder? | [CODE-PATTERNS.md](CODE-PATTERNS.md#protocol-decoder-pattern) |
| Debug a problem? | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| Find archived/deprioritized phases? | [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md) |
| See known issues? | [GAPS.md](GAPS.md#active-gaps) |
| Understand the architecture? | [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md#core-design-decisions) |

### "What's the status of...?"

| Topic | Answer |
|---|---|
| Phases 1–16? | ✅ Complete — see [PHASES-COMPLETED.md](PHASES-COMPLETED.md) |
| Phases 18–22? | ✅ Complete — see [PHASES-COMPLETED.md](PHASES-COMPLETED.md) |
| API & Backend Polish? | ✅ Complete — see [PHASES-COMPLETED.md](PHASES-COMPLETED.md) |
| Frontend Usability? | ✅ Complete — see [PHASES-COMPLETED.md](PHASES-COMPLETED.md) |
| Phase 17? | 🗄️ Archived — see [PHASES-ARCHIVED.md](PHASES-ARCHIVED.md) |
| Future work? | 🔭 See [PHASES-ROADMAP.md](PHASES-ROADMAP.md) |
| Known bugs? | 🔗 See [GAPS.md](GAPS.md#active-gaps) |
| Tech debt? | 🔗 See [GAPS.md](GAPS.md#technical-debt-items) |

---

## 🎯 Recommended Next Steps

### For Starting Contributors
1. Clone repo and run `dotnet build` + `dotnet test` (see [AGENT-NOTES.md](AGENT-NOTES.md#quick-setup))
2. Read [PHASES-COMPLETED.md](PHASES-COMPLETED.md) phases 1-3 to understand the foundation
3. Pick a small issue from [GAPS.md](GAPS.md) (severity: Low) and fix it
4. Submit PR with clear description

### For New Feature Work
- Review [PHASES-ROADMAP.md](PHASES-ROADMAP.md) for enhancement areas (Scanner & Anomaly, Protocol Decoder Depth, Longer-Horizon)
- Check [GAPS.md](GAPS.md#active-gaps) for actionable technical debt
- Follow feature checklist in [AGENT-NOTES.md](AGENT-NOTES.md#adding-features-checklist)

### For Architecture Reviews
- Read [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)
- Cross-reference [GAPS.md](GAPS.md) for known limitations
- Suggest improvements via PR comments

---

## ❓ FAQs

**Q: Where do I find API documentation?**  
A: [README.md](../README.md#api-reference) has all endpoints; Scalar docs at `http://localhost:5000/scalar` in Development mode.

**Q: How do I add a new protocol decoder?**  
A: Follow "Backend feature" checklist in [AGENT-NOTES.md](AGENT-NOTES.md#backend-feature), implement `IProtocolDecoder<T>`, add tests.

**Q: Why is everything async?**  
A: ASP.NET Core best practice; avoids blocking threads; allows concurrent request handling.

**Q: Why split PLAN.md into multiple files?**  
A: Token efficiency — load only what you need; easier to navigate; less cognitive overhead.

**Q: What's the roadmap after Phase 21?**  
A: See [PHASES-ROADMAP.md](PHASES-ROADMAP.md#future-enhancement-areas-phases-22) for ideas (offline mode, mobile app, ML anomaly detection, etc.).

---

## 📞 Getting Help

- **Technical architecture:** Ask in PR comments; reference [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)
- **Setup issues:** Check [AGENT-NOTES.md](AGENT-NOTES.md#common-debugging-scenarios)
- **Feature design:** Discuss in GitHub issues; link to relevant phases
- **Performance concerns:** Check [GAPS.md](GAPS.md#performance-considerations)

---

**Last updated:** April 2026  
**Total phases completed:** 20 of 20 core features  
**Active contributors welcome** — start with [AGENT-NOTES.md](AGENT-NOTES.md) and [GAPS.md](GAPS.md)
