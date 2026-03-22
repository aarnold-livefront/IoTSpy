# Claude Code Skills

This directory contains Claude Code skill packages used when developing IoTSpy. Skills extend Claude's behavior with project-relevant expertise and are invoked automatically by Claude when relevant, or manually via `/skill-name`.

## Skills

| Skill | Description |
|-------|-------------|
| `dotnet-engineer.skill` | Senior .NET engineering guidance — ASP.NET Core, EF Core, SignalR, Polly, xUnit/NSubstitute, performance, architecture patterns |
| `security-code-review.skill` | Systematic security code review — vulnerability analysis across the full attack surface (OWASP Top 10, auth, injection, etc.) |
| `threat-modeling.skill` | Structured threat modeling for systems, features, and architecture changes |

## Installing skills

Run the following from the repo root to install all skills into your local Claude Code:

```bash
claude skills install .dev/claude-skills/dotnet-engineer.skill
claude skills install .dev/claude-skills/security-code-review.skill
claude skills install .dev/claude-skills/threat-modeling.skill
```

Or install them individually as needed.

## Usage

Once installed, skills activate automatically when Claude detects relevant context. You can also invoke them explicitly:

- `/dotnet-engineer` — ask for .NET architecture guidance, EF Core help, etc.
- `/security-code-review` — review code for security vulnerabilities before merging
- `/threat-modeling` — model threats for a new feature or system design
