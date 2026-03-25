# Claude Code Skills

This directory contains Claude Code skill packages used when developing IoTSpy. Skills extend Claude's behavior with project-relevant expertise and are invoked automatically when relevant, or manually via `/skill-name`.

Each skill is a directory containing a `SKILL.md` with YAML frontmatter and markdown instructions.

## Skills

| Skill | Description |
|-------|-------------|
| `dotnet-engineer/` | Senior .NET engineering guidance — ASP.NET Core, EF Core, SignalR, Polly, xUnit/NSubstitute, performance, architecture patterns |
| `security-code-review/` | Systematic security code review — vulnerability analysis across the full attack surface (OWASP Top 10, auth, injection, etc.) |
| `threat-modeling/` | Structured threat modeling for systems, features, and architecture changes |

## Installing skills

Run the following from the repo root to install all skills into your local Claude Code:

```bash
# 1. Register the local marketplace (absolute path required)
claude plugin marketplace add "$(pwd)/.dev/claude-skills" --scope project

# 2. Install each skill
claude plugin install dotnet-engineer@iotspy-skills --scope project
claude plugin install security-code-review@iotspy-skills --scope project
claude plugin install threat-modeling@iotspy-skills --scope project
```

## Usage

Once installed, skills activate automatically when Claude detects relevant context. You can also invoke them explicitly:

- `/dotnet-engineer` — ask for .NET architecture guidance, EF Core help, etc.
- `/security-code-review` — review code for security vulnerabilities before merging
- `/threat-modeling` — model threats for a new feature or system design

## Updating skills

Edit the `SKILL.md` file in the relevant skill directory directly (both the top-level copy and `skills/<name>/SKILL.md`). Skills are loaded from disk, so changes take effect in the next session without reinstalling.
