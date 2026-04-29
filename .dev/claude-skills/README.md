# Claude Code Skills

Skill packages used when developing IoTSpy. Skills extend Claude's behavior with project-relevant expertise and activate automatically when relevant, or via `/skill-name`.

Each skill is a directory containing a `SKILL.md` with YAML frontmatter and markdown instructions. Skills live at `<plugin>/skills/<name>/SKILL.md`.

## Skills

| Skill | Description |
|-------|-------------|
| `dotnet-engineer/` | Senior .NET engineering guidance — ASP.NET Core, EF Core, SignalR, Polly, xUnit/NSubstitute (project-agnostic) |
| `security-code-review/` | Systematic security review across input handling, authz, resources, errors, crypto, secrets, and supply chain |
| `threat-modeling/` | Structured threat modeling — STRIDE + OWASP + ATT&CK, calibrated severity, dual-use tool considerations |
| `iotspy-context/` | IoTSpy-specific architecture, conventions, and security caveats — companion to the three skills above |

The first three skills are deliberately project-agnostic so they don't drift as IoTSpy evolves. `iotspy-context` carries the project-specific layer and points back to `CLAUDE.md` / `AGENT.md` for facts that change (test counts, migration count, completed phases) instead of duplicating them.

## Installing skills

From the repo root:

```bash
# 1. Register the local marketplace (absolute path required)
claude plugin marketplace add "$(pwd)/.dev/claude-skills" --scope project

# 2. Install each skill
claude plugin install dotnet-engineer@iotspy-skills --scope project
claude plugin install security-code-review@iotspy-skills --scope project
claude plugin install threat-modeling@iotspy-skills --scope project
claude plugin install iotspy-context@iotspy-skills --scope project
```

## Usage

Skills activate automatically when relevant context is detected. Invoke explicitly with:

- `/dotnet-engineer` — .NET architecture, EF Core, SignalR, Polly, testing
- `/security-code-review` — security review before merging
- `/threat-modeling` — threat model for a new feature or design
- `/iotspy-context` — IoTSpy-specific facts and risks (use alongside the others when working in this repo)

## Updating skills

Edit `<plugin>/skills/<name>/SKILL.md` directly. Skills are loaded from disk; changes take effect in the next session without reinstalling.

When IoTSpy state changes (test counts, migration count, completed phases), update `CLAUDE.md` and `AGENT.md` — the `iotspy-context` skill points back to those rather than duplicating.
