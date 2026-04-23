# IoTSpy — Implementation Plan

This is your entry point for all project documentation. Find what you need below.

---

## 🎯 Pick Your Task

### "I want to set up and start developing"
→ **[AGENT-NOTES.md](AGENT-NOTES.md)** — Quick setup in 3 commands, testing procedures

```bash
export Auth__JwtSecret="your-32-char-secret" && dotnet build && dotnet test
```

### "I need to add a new feature"
→ **[CODE-PATTERNS.md](CODE-PATTERNS.md)** — Where to put code + examples (controller, repo, decoder, etc.)

→ **[QUICK-REF.md](QUICK-REF.md)** — Common commands (migrations, git, build)

### "Something is broken or not working"
→ **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** — Solutions for 30+ common issues

### "I want to understand the architecture"
→ **[DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)** — Why we chose X, naming rules, critical notes

### "I need to use skills or plugins"
→ **[SKILLS-PLUGINS.md](SKILLS-PLUGINS.md)** — When/how to use `/dotnet-engineer`, `/security-code-review`, `/threat-modeling`, etc.

### "What should I work on next?"
→ **[GAPS.md](GAPS.md)** — Known issues, API gaps, security hardening, tech debt

→ **[PHASES-ROADMAP.md](PHASES-ROADMAP.md)** — Phase 23+ roadmap

→ **[PHASES-ARCHIVED.md](PHASES-ARCHIVED.md)** — Archived Phase 17 (non-IP IoT)

### "What's been completed?"
→ **[PHASES-COMPLETED.md](PHASES-COMPLETED.md)** — All 23 active phases with details and test counts

### "Show me everything at once"
→ **[PLAN-INDEX.md](PLAN-INDEX.md)** — Complete navigation hub with role-based guides

---

## 📊 Current Project Status

| Metric | Value |
|---|---|
| **Phases complete** | 1–16, 18–22 + content rules decoupling (23 of 23 active phases) ✅ |
| **Backend tests** | 608+ (all passing) |
| **REST controllers** | 19 |
| **SignalR hubs** | 3 |
| **Protocols** | HTTP/HTTPS, MQTT, DNS, CoAP, WebSocket, gRPC, Modbus, OpenRTB |
| **Next phase** | 23+ (roadmap only) |

---

## 📚 Documentation Map

### Getting Started (Start here if new)
1. **[AGENT-NOTES.md](AGENT-NOTES.md)** (400 lines)
   - Setup, testing, debugging, feature checklist
   - Best for: "How do I get started?"

2. **[QUICK-REF.md](QUICK-REF.md)** (200 lines)
   - Command shortcuts, recipes, common operations
   - Best for: "What's the command for X?"

### Development
3. **[CODE-PATTERNS.md](CODE-PATTERNS.md)** (400 lines)
   - Where code goes + copy-paste examples
   - Best for: "I'm adding a controller/decoder/hook"

4. **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** (350 lines)
   - Solutions for 30+ problems with step-by-step fixes
   - Best for: "X is broken, how do I fix it?"

5. **[SKILLS-PLUGINS.md](SKILLS-PLUGINS.md)** (300 lines)
   - When/how to use `/dotnet-engineer`, `/security-code-review`, `/threat-modeling`
   - Best for: "When should I use a skill? How do I invoke it?"

### Planning & Design
6. **[DESIGN-DECISIONS.md](DESIGN-DECISIONS.md)** (400 lines)
   - Architecture decisions, naming, patterns, quirks
   - Best for: "Why did we choose X?"

7. **[PHASES-COMPLETED.md](PHASES-COMPLETED.md)** (500 lines)
   - All 21 active phases with descriptions
   - Best for: "What did Phase 15 do?"

8. **[PHASES-ROADMAP.md](PHASES-ROADMAP.md)** (100 lines)
   - Future phases (21+) and deprioritized Phase 17
   - Best for: "What's next?"

9. **[GAPS.md](GAPS.md)** (350 lines)
   - Known issues, API gaps, security hardening, tech debt, suggestions
   - Best for: "What should I work on?"

### Comprehensive Reference
10. **[PLAN-INDEX.md](PLAN-INDEX.md)** (250 lines)
    - Full navigation hub
    - Best for: "I want to see everything"

### External References
- **[ARCHITECTURE.md](ARCHITECTURE.md)** — Full technical architecture
- **[../README.md](../README.md)** — Features, API reference, quick start
- **[../CLAUDE.md](../CLAUDE.md)** — Project-specific commands

---

## 🚀 For Agents: Recommended Workflow

1. **First task?** Read [AGENT-NOTES.md](AGENT-NOTES.md#quick-setup) + [QUICK-REF.md](QUICK-REF.md)
2. **Adding code?** Check [CODE-PATTERNS.md](CODE-PATTERNS.md#where-does-my-code-go)
3. **Something wrong?** Search [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
4. **Need context?** Read [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) + [GAPS.md](GAPS.md)
5. **Want details?** See [PHASES-COMPLETED.md](PHASES-COMPLETED.md) + [PHASES-ROADMAP.md](PHASES-ROADMAP.md)

---

## 💡 Key Information

**JWT Secret (required for running):**
```bash
export Auth__JwtSecret="minimum-32-characters-long-string"
```

**Run everything:**
```bash
# Terminal 1: Backend
dotnet watch --project src/IoTSpy.Api

# Terminal 2: Frontend
cd frontend && npm run dev

# Terminal 3: Tests (optional)
dotnet test --watch
```

**Quick test before commit:**
```bash
dotnet build && dotnet test
cd frontend && npm test && npm run build
```

---

## 📍 File Locations

| What | Where |
|---|---|
| API backend | `src/IoTSpy.Api` |
| Domain models | `src/IoTSpy.Core` |
| Database | `src/IoTSpy.Storage` |
| Proxy servers | `src/IoTSpy.Proxy` |
| Business logic | `src/IoTSpy.{Scanner,Manipulation,Protocols}` |
| Tests | `src/IoTSpy.*.Tests` |
| Frontend | `frontend/src` |
| Documentation | `docs/` |

---

## ❓ FAQ

**Q: Where do I add my new code?**  
A: See [CODE-PATTERNS.md](CODE-PATTERNS.md#where-does-my-code-go) — it's a lookup table

**Q: How do I run tests?**  
A: See [QUICK-REF.md](QUICK-REF.md#build--test)

**Q: Something broke, how do I debug?**  
A: See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

**Q: What's the architecture?**  
A: See [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) or [ARCHITECTURE.md](ARCHITECTURE.md)

**Q: What should I work on next?**  
A: See [GAPS.md](GAPS.md) or [PHASES-ROADMAP.md](PHASES-ROADMAP.md)

---

**Pro tip:** Bookmark [QUICK-REF.md](QUICK-REF.md) — you'll use it every day!
