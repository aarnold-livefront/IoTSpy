# IoTSpy — Skills & Plugins Guide

Instructions for when and how to use Claude Code skills and project-specific plugins to enhance development workflow.

---

## Table of Contents
1. [Quick Reference](#quick-reference)
2. [Global Skills](#global-skills)
3. [Project-Specific Skills](#project-specific-skills)
4. [When to Use Each Skill](#when-to-use-each-skill)
5. [How to Invoke](#how-to-invoke)
6. [Skill Decision Matrix](#skill-decision-matrix)

---

## Quick Reference

| Skill | Type | When to Use | Example |
|---|---|---|---|
| `/dotnet-engineer` | Project | ASP.NET Core/EF Core architecture guidance | "Design a new repository" |
| `/security-code-review` | Project | Security review before merging | "Review for OWASP issues" |
| `/threat-modeling` | Project | Threat analysis for new features | "What are the attack vectors?" |
| `/review` | Global | General code review | Use before major PRs |
| `/security-review` | Global | Security audit | Use before sensitive PRs |
| `/simplify` | Global | Code quality cleanup | "Simplify this code" |
| `/update-config` | Global | Config/settings changes | "Allow npm commands" |
| `/loop` | Global | Recurring tasks | "Run tests every 5m" |
| `/claude-api` | Global | Claude API/SDK work | Only if working with API client |
| `/init` | Global | CLAUDE.md setup | Only for new projects |

---

## Global Skills

Available in any Claude Code session (project-agnostic).

### `/review`
**Purpose:** General code review before merging  
**When:** Before submitting PRs that touch critical code  
**Example:**
```
/review
# Agent will review current branch changes for quality
```

**Covers:**
- Best practices
- Code style
- Potential bugs
- Performance issues

---

### `/security-review`
**Purpose:** Security audit of code changes  
**When:** Before merging anything handling auth, data, or external input  
**Example:**
```
/security-review
# Agent will audit for OWASP Top 10 vulnerabilities
```

**Covers:**
- SQL injection / command injection
- XSS / CSRF
- Authentication/authorization flaws
- Sensitive data exposure
- Cryptography issues
- Dependency vulnerabilities

---

### `/simplify`
**Purpose:** Refactor code for clarity and efficiency  
**When:** After feature is working; before code review  
**Example:**
```
/simplify
# Agent will review code for reuse, quality, efficiency
```

**Covers:**
- Dead code removal
- DRY violations
- Inefficient patterns
- Readability improvements

**Note:** Use AFTER functionality is complete, not during active development

---

### `/update-config`
**Purpose:** Modify Claude Code settings and hooks  
**When:** Setting up IDE integrations, permissions, environment variables  
**Example:**
```
/update-config
# Configure settings.json, add permissions, set env vars
```

**Handles:**
- Permission management
- Environment variables
- Hook configuration
- IDE settings

---

### `/loop`
**Purpose:** Run tasks on a recurring schedule  
**When:** Monitoring long-running tasks (builds, deployments, test suites)  
**Example:**
```
/loop 5m dotnet test
# Run tests every 5 minutes
```

**Use cases:**
- Watch for build failures
- Monitor deployment health
- Track test suite regressions
- Poll for CI status

---

### `/claude-api`
**Purpose:** Build/debug Claude API integration  
**When:** Only if working on code that uses Anthropic SDK  
**Example:**
```
/claude-api
# Help with Claude API implementation, caching, etc.
```

**Covers:**
- Anthropic SDK usage
- Prompt engineering
- Token counting
- Structured outputs
- Prompt caching

**Note:** Rarely needed for IoTSpy (already has AI mock integration)

---

### `/keybindings-help`
**Purpose:** Configure keyboard shortcuts  
**When:** Customizing IDE keybindings  
**Example:**
```
/keybindings-help
# Customize keyboard shortcuts
```

---

### `/session-start-hook`
**Purpose:** Set up initialization hooks for web sessions  
**When:** Setting up Claude Code on the web for this project  
**Example:**
```
/session-start-hook
# Create SessionStart hook to run setup on web
```

---

### `/init`
**Purpose:** Initialize CLAUDE.md for new projects  
**When:** Only for NEW projects (not needed here)  
**Example:**
```
/init
# Create initial CLAUDE.md
```

---

## Project-Specific Skills

Located in `.dev/claude-skills/`. Install once from repo root:

```bash
# 1. Register local marketplace
claude plugin marketplace add "$(pwd)/.dev/claude-skills" --scope project

# 2. Install each skill
claude plugin install dotnet-engineer@iotspy-skills --scope project
claude plugin install security-code-review@iotspy-skills --scope project
claude plugin install threat-modeling@iotspy-skills --scope project
```

### `/dotnet-engineer`
**Purpose:** ASP.NET Core + EF Core architecture guidance  
**When:** Designing backend features, troubleshooting .NET issues  
**Example:**
```
/dotnet-engineer
Design a new controller for handling device status updates
```

**Covers:**
- ASP.NET Core patterns (DI, middleware, filters)
- EF Core best practices (queries, migrations, relationships)
- SignalR hub design
- Polly resilience patterns
- xUnit / NSubstitute testing
- Multi-tenant or RBAC patterns

**You should use this for:**
- ✅ Designing a new REST endpoint
- ✅ Setting up a new repository
- ✅ Creating a SignalR hub
- ✅ Configuring EF Core migrations
- ✅ Debugging EF Core issues
- ✅ Planning resilience strategy
- ✅ Writing integration tests
- ❌ Frontend code (use `/review` instead)
- ❌ Protocol decoders (general architecture applies)

---

### `/security-code-review`
**Purpose:** OWASP Top 10 + auth/injection vulnerability review  
**When:** Before submitting PR, especially for auth/data features  
**Example:**
```
/security-code-review
Review the new API key endpoint for security issues
```

**Covers:**
- OWASP Top 10 vulnerabilities
- Authentication/authorization flaws
- SQL injection & command injection
- XSS & CSRF
- Sensitive data exposure
- Cryptographic weaknesses
- Access control issues
- Input validation

**You should use this for:**
- ✅ Auth controller endpoints
- ✅ API key management code
- ✅ User CRUD operations
- ✅ Admin-only endpoints
- ✅ Data export features
- ✅ Password hashing logic
- ✅ Token generation
- ⚠️  Before merging ANY user-facing endpoint
- ❌ Decoder implementations (less critical)

---

### `/threat-modeling`
**Purpose:** Structured threat analysis for new features  
**When:** Designing new feature (especially involving users, data, or external systems)  
**Example:**
```
/threat-modeling
Analyze threats for the new collaboration/real-time sharing feature
```

**Covers:**
- Attack vectors
- Threat actors
- Data flow risks
- Authentication/authorization gaps
- Denial of service vectors
- Supply chain risks
- Mitigation strategies

**You should use this for:**
- ✅ New features involving user data
- ✅ Network-exposed APIs
- ✅ Features with multi-user access
- ✅ Data export/sharing features
- ✅ Admin operations
- ✅ External integrations
- ⚠️  Phase 21+ (Passive Mode)
- ❌ Pure decoder/protocol work
- ❌ Internal utility services

---

## When to Use Each Skill

### Scenario 1: Adding a new REST endpoint
```
1. Design with /dotnet-engineer (architecture)
2. Implement following CODE-PATTERNS.md
3. Review with /review (code quality)
4. Security audit with /security-code-review (if touching auth/data)
5. /simplify if code is complex
6. Commit
```

### Scenario 2: Implementing new feature (e.g., Phase 21)
```
1. /threat-modeling (analyze risks upfront)
2. /dotnet-engineer (design architecture)
3. CODE-PATTERNS.md (follow patterns)
4. /review (code quality)
5. /security-code-review (before PR)
6. /simplify (optional cleanup)
7. Commit & PR
```

### Scenario 3: Fixing a bug
```
1. Reproduce & diagnose
2. /review if unclear on fix approach
3. Fix using CODE-PATTERNS.md as reference
4. Test thoroughly
5. /simplify if touching related code
6. Commit
```

### Scenario 4: Urgent security fix
```
1. /security-review (identify scope)
2. /dotnet-engineer if architecture unclear
3. Implement fix
4. /security-code-review (verify fix)
5. Test
6. Commit (mark as SECURITY)
```

### Scenario 5: Adding protocol decoder
```
1. /dotnet-engineer (decoder pattern guidance)
2. CODE-PATTERNS.md ("Protocol Decoder Pattern")
3. Implement following pattern
4. /review (code quality)
5. Test with real packet fixtures
6. Commit
```

### Scenario 6: Setting up new environment
```
1. /update-config (permissions, env vars)
2. /session-start-hook (if web session)
3. AGENT-NOTES.md (setup steps)
4. Run commands from QUICK-REF.md
```

---

## Skill Decision Matrix

```
What am I doing?           Best skill(s)              Fallback
─────────────────────────────────────────────────────────────────
Adding endpoint            /dotnet-engineer           /review
Adding repository          /dotnet-engineer           /review
Adding controller          /dotnet-engineer           /review
Adding SignalR hub         /dotnet-engineer           /review
EF Core migration          /dotnet-engineer           /review
Auth code                  /security-code-review      /dotnet-engineer
User CRUD                  /security-code-review      /dotnet-engineer
API keys / tokens          /security-code-review      /dotnet-engineer
Data export                /security-code-review      /review
New feature (big)          /threat-modeling           /dotnet-engineer
Decoder implementation     /dotnet-engineer           /review
Frontend code              /review                    /simplify
Code cleanup               /simplify                  /review
Before PR                  /security-code-review      /review
General review             /review                    /simplify
Bug fix                    /review (if unclear)       (none needed)
Protocol work              /dotnet-engineer           /review
Test writing               /dotnet-engineer           /review
─────────────────────────────────────────────────────────────────
When uncertain → /dotnet-engineer (most general)
When security involved → /security-code-review
When designing → /threat-modeling
When polishing → /simplify
```

---

## How to Invoke

### Using the `/` shorthand (easiest)
```
/dotnet-engineer
Design a repository for the new feature

/security-code-review
Check the auth code for vulnerabilities

/threat-modeling
What are the risks in the new collaboration feature?
```

The skill system will automatically:
- Load the skill definition
- Expand the prompt
- Run the skill with full context

### Using the Skill tool (explicit)
```
/skill dotnet-engineer
"Design repository pattern for managing API keys"

/skill security-code-review
"Review the auth controller for OWASP issues"

/skill threat-modeling
"Analyze risks in the passive mode feature"
```

---

## Best Practices

### ✅ DO
- Use `/dotnet-engineer` early (design phase, not after coding)
- Use `/security-code-review` **before pushing** (not after merge)
- Use `/threat-modeling` **during feature design** (not after implementation)
- Chain skills: design → code → review → simplify → commit
- Save skill output in comments (reference for future work)

### ❌ DON'T
- Use `/dotnet-engineer` for frontend code (use `/review` instead)
- Ignore `/security-code-review` for auth/data features
- Skip `/threat-modeling` for user-facing features
- Use `/simplify` during active development (creates conflicts)
- Use `/loop` without clear stopping condition (can waste tokens)
- Use `/claude-api` unless you're actually working with Claude API

---

## Skill Output Usage

When a skill completes, save the output in:
- Code comments (for decisions made)
- Commit messages (reference architecture review)
- PR descriptions (link to security review)

Example commit:
```
Add API key management endpoints

Reviewed with /security-code-review for:
- Hash storage safety
- Scope validation
- Token generation entropy
- Audit logging

All OWASP checks passed.

https://claude.ai/code/session_01T6WuGUXCVN5FiXGTruvFx5
```

---

## Project-Specific Skill Details

See `.dev/claude-skills/README.md` for:
- Full skill source code
- Detailed capability documentation
- Advanced usage examples
- Customization options

Install them once, then use via `/skill-name` shorthand in any session.

---

## Summary: Quick Decision

**Before you code:** Ask yourself these questions

1. **Is this a new backend feature?** → Use `/dotnet-engineer`
2. **Does it handle auth, data, or users?** → Use `/security-code-review` before PR
3. **Is it a brand new feature (Phase 21+)?** → Use `/threat-modeling` upfront
4. **Is my code complex/unclear?** → Use `/simplify` after it works
5. **Before submitting PR?** → Use `/security-code-review` + `/review`

**Default workflow:**
```
Design → Code (per patterns) → Review → Security check → Simplify → Commit
  ↓       (follow docs)        ↓          ↓             ↓         ↓
 /dotnet  CODE-PATTERNS    /review  /security-review /simplify  git commit
```

See [PLAN.md](PLAN.md) for task-driven guide, [CODE-PATTERNS.md](CODE-PATTERNS.md) for implementation patterns.
