# IoTSpy — Skills & Plugins Guide

When and how to use Claude Code skills while developing IoTSpy. This guide is **workflow-only** — the skills themselves describe what they cover. For "what does this skill do?" read the SKILL.md inside each plugin under `.dev/claude-skills/<plugin>/skills/<name>/SKILL.md`.

---

## Quick Reference

| Skill | Type | Use when |
|---|---|---|
| `/dotnet-engineer` | Project | Designing or debugging .NET code (architecture, EF Core, SignalR, Polly, tests) |
| `/security-code-review` | Project | Reviewing code for security — methodology and IoTSpy-specific risks. **Canonical security review for this repo.** |
| `/threat-modeling` | Project | Modeling threats for a new feature or design change |
| `/iotspy-context` | Project | Working in this repo — pair with any of the above for IoTSpy-specific facts and risks |
| `/review` | Global | General code review |
| `/simplify` | Global | Refactor for clarity after code works |
| `/update-config` | Global | Settings, hooks, permissions |
| `/loop` | Global | Recurring tasks |
| `/claude-api` | Global | Anthropic SDK work (rarely needed here) |
| `/init` | Global | New CLAUDE.md (already exists) |

> **On security review:** the global `/security-review` skill (auto-audit of pending branch changes) still exists, but for IoTSpy use `/security-code-review` — it carries the methodology, applies cleanly when paired with `/iotspy-context`, and gives consistent output across reviewers. Treat the global one as a fallback for quick ad-hoc audits, not the recommended workflow.

Project skills live in `.dev/claude-skills/`. See [`.dev/claude-skills/README.md`](../.dev/claude-skills/README.md) for install commands and the canonical skill list.

---

## Workflow recipes

### Adding a new REST endpoint

1. `/dotnet-engineer` (+ `/iotspy-context` if unsure of project conventions) — design the controller/repository
2. Implement following [`CODE-PATTERNS.md`](CODE-PATTERNS.md)
3. `/review` — code quality
4. `/security-code-review` — if it touches auth/data
5. `/simplify` — if the code grew complex
6. `dotnet test`, then commit

### Implementing a new feature (e.g., next phase)

1. `/threat-modeling` + `/iotspy-context` — analyze risks upfront, including dual-use tool considerations
2. `/dotnet-engineer` — architecture
3. Implement per [`CODE-PATTERNS.md`](CODE-PATTERNS.md)
4. `/review` then `/security-code-review` (with `/iotspy-context`) before PR
5. `/simplify` (optional)
6. `dotnet test`, commit, PR

### Fixing a bug

1. Reproduce and diagnose first
2. `/dotnet-engineer` if the fix path is unclear
3. Fix, add a regression test, run tests
4. `/simplify` if surrounding code needs cleanup
5. Commit

### Urgent security fix

1. `/security-code-review` + `/iotspy-context` — identify scope and confirm IoTSpy-specific risk surface (single-user JWT, scripted breakpoints, captured-data exposure)
2. `/dotnet-engineer` if architecture is involved
3. Implement
4. `/security-code-review` + `/iotspy-context` — verify the fix lands cleanly and didn't open a new surface
5. Tests, commit (mark `SECURITY:`)

### Adding a protocol decoder

1. `/dotnet-engineer` + `/iotspy-context` — pattern guidance
2. Follow "Protocol Decoder Pattern" in [`CODE-PATTERNS.md`](CODE-PATTERNS.md)
3. Implement, add packet-fixture tests
4. `/review`
5. Commit

### Setting up a new environment / web session

1. `/update-config` — permissions, env vars
2. `/session-start-hook` — for Claude Code on the web
3. See `docs/AGENT-NOTES.md` and `docs/QUICK-REF.md`

---

## Decision matrix

```
What am I doing?           Best skill(s)                            Fallback
────────────────────────────────────────────────────────────────────────────
Adding endpoint            /dotnet-engineer + /iotspy-context        /review
Adding repository          /dotnet-engineer + /iotspy-context        /review
SignalR hub                /dotnet-engineer + /iotspy-context        /review
EF Core migration          /dotnet-engineer + /iotspy-context        /review
Auth code                  /security-code-review + /iotspy-context   /dotnet-engineer
User CRUD / API keys       /security-code-review                     /dotnet-engineer
Scripted breakpoint code   /security-code-review + /iotspy-context   /threat-modeling
Capture/replay/export      /security-code-review + /iotspy-context   /review
New big feature            /threat-modeling + /iotspy-context        /dotnet-engineer
Decoder implementation     /dotnet-engineer                          /review
Frontend code              /review                                   /simplify
Code cleanup               /simplify                                 /review
Before any PR              /review (+ /security-code-review)         —
────────────────────────────────────────────────────────────────────────────
When uncertain → start with /dotnet-engineer + /iotspy-context
When security involved → add /security-code-review
When designing a new feature → add /threat-modeling
```

---

## How to invoke

```
/dotnet-engineer
Design a repository for managing API keys.

/security-code-review
Review the auth controller changes on this branch.

/threat-modeling
What are the risks in the new "share capture by link" feature?

/iotspy-context
What conventions matter for adding a new SignalR hub here?
```

Skills also activate automatically when relevant context is detected.

---

## Best practices

**Do**
- Use `/dotnet-engineer` *during design*, not after coding
- Use `/security-code-review` *before pushing*, not after merge
- Use `/threat-modeling` *during feature design*, not after implementation
- Pair `/iotspy-context` with any of the project skills when working in this repo so IoTSpy-specific risks (dual-use tool, single-user JWT, scripted breakpoints, captured-data confidentiality) get factored in
- Chain skills: design → code → review → simplify → commit

**Don't**
- Use `/dotnet-engineer` for frontend code (use `/review`)
- Skip `/security-code-review` for auth, capture/export, or scripted-breakpoint changes
- Skip `/threat-modeling` for user-facing or network-exposed features
- Use `/simplify` mid-development (creates merge churn)
- Use `/loop` without a clear stopping condition

---

## Saving skill output

Reference skill output in commit messages and PR descriptions when it shaped a decision. Example:

```
Add API key management endpoints

Reviewed with /security-code-review + /iotspy-context for:
- Hash storage and BCrypt cost factor
- Scope validation against the single-user JWT model
- Audit logging coverage
- Captured-data redaction in audit entries
```

---

## See also

- [`.dev/claude-skills/README.md`](../.dev/claude-skills/README.md) — install and update instructions
- Each plugin's `SKILL.md` under `.dev/claude-skills/<plugin>/skills/<name>/SKILL.md` — what the skill covers
- [`CODE-PATTERNS.md`](CODE-PATTERNS.md) — implementation patterns
- [`PLAN.md`](PLAN.md) — task-driven guide
- `CLAUDE.md` and `AGENT.md` — current project state and operational requirements
