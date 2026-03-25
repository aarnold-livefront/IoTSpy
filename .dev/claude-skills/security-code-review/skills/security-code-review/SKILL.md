---
name: security-code-review
description: >
  Security-focused code review that systematically checks for vulnerabilities across the full attack surface — not just the most obvious issue. Use this skill whenever someone asks you to review code for security, check if something is safe, audit an endpoint or function, or look for bugs before merging. Also trigger when the user pastes code and asks "is this OK?", "any issues here?", or "does this look right?" — even without the word "security", because security bugs often hide in code that looks superficially fine. Works for any language, but especially strong on web APIs, authentication flows, file handling, and network-facing code.
---

# Security Code Review

When reviewing code for security, your job is to find what can go wrong — across the *whole* piece of code, not just the most obvious issue. A common failure mode is fixating on one clear vulnerability and missing three subtler ones. Resist it.

## Your review process

**Step 1 — Understand what the code is doing**
Before looking for bugs, understand the intent: what does this code accept as input, what does it do with it, what does it produce, and who calls it? This shapes which vulnerability classes are relevant.

**Step 2 — Trace every input to its use**
Follow each user-controlled or external input through the code. Ask: is it validated? Is it sanitized for the right context? Is it used in a sensitive operation (file path, SQL query, shell command, HTML output, redirect URL)? Untrusted data flowing into a sensitive sink without appropriate sanitization is the core pattern behind injection, path traversal, XSS, SSRF, and most other input-class bugs.

**Step 3 — Check resource and operational concerns**
Security isn't only about confidentiality and integrity — availability matters too. Look for:
- Unbounded memory allocation (reading entire files/responses into memory, no size limit)
- Missing timeouts on I/O or external calls
- Operations that can be made expensive by a caller (N+1 patterns, algorithmic complexity, large payload acceptance)
- Missing rate limiting on costly endpoints

**Step 4 — Check authorization and authentication**
Even if auth is handled at a higher layer (e.g., `[Authorize]` on the controller), ask: does the code verify the *caller's right to access this specific resource*, not just that they're authenticated? Broken object-level authorization (BOLA/IDOR) is among the most common API vulnerabilities — a valid token for user A shouldn't grant access to user B's data.

**Step 5 — Check error handling and information leakage**
Does the code expose stack traces, internal paths, database errors, or other implementation details to callers? Does it behave differently in ways that leak information (timing differences, distinct error messages for "user not found" vs "wrong password")?

**Step 6 — Check cryptography and secrets**
Is sensitive data encrypted at rest and in transit? Are secrets hardcoded? Are cryptographic primitives being used correctly (correct mode, IV handling, key length)? Are tokens/passwords being logged?

## How to structure your output

Lead with a **verdict**: safe / has issues / critical issues.

Then list findings in descending severity order. For each finding:
- **What**: name the vulnerability class (path traversal, IDOR, unbounded memory read, etc.)
- **Where**: point to the specific line or pattern
- **Why it matters**: explain the realistic impact and how an attacker could exploit it — don't just name the CWE
- **Fix**: give a concrete, correct fix, not a vague recommendation

If something looks intentionally risky but acceptable in context (e.g., TLS validation disabled for a research tool), note it as a design decision rather than a bug, and flag what would need to change if the context changed.

End with a **coverage note** if there are things you couldn't assess from the snippet alone (missing auth layer, downstream validation, etc.).

## What good looks like

A good review catches the path traversal *and* the unbounded `ReadAllBytesAsync`. It doesn't treat them as equally critical — path traversal is critical, memory exhaustion is medium — but it mentions both, because the goal is a complete picture, not a single finding.

Don't skip resource and operational concerns just because they feel less "exciting" than injection bugs. DoS is a security issue.
