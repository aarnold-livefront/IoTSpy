---
name: security-code-review
description: Systematic security review across the full attack surface — input handling, authorization, resource use, errors, crypto, secrets, and dependencies. Use when asked to review code for security, audit an endpoint, or check whether something is safe before merging. Also trigger on "is this OK?" / "any issues here?" — security bugs hide in code that looks fine.
---

# Security Code Review

Your job is to find what can go wrong across the *whole* piece of code, not just the most obvious issue. The common failure mode is fixating on one clear vulnerability and missing three subtler ones. Resist it.

## Your review process

**Step 1 — Understand what the code does**
Before looking for bugs, understand the intent: what does it accept as input, what does it do with it, what does it produce, who calls it? This shapes which vulnerability classes are relevant.

**Step 2 — Trace every input to its use**
Follow each user-controlled or external input through the code. Ask: is it validated? Is it sanitized for the right context? Is it used in a sensitive sink (file path, SQL query, shell command, HTML output, redirect URL, deserializer)? Untrusted data flowing into a sensitive sink without context-appropriate sanitization is the core pattern behind injection, path traversal, XSS, SSRF, and most input-class bugs.

**Step 3 — Check resource and operational concerns**
DoS is a security issue. Look for:
- Unbounded memory allocation (reading entire files/responses into memory, no size limit)
- Missing timeouts on I/O or external calls
- Operations a caller can make expensive (N+1 patterns, algorithmic complexity, regex catastrophic backtracking, large payload acceptance)
- Missing rate limiting on costly endpoints

**Step 4 — Check authentication, authorization, and resource ownership**
Authentication ("you are who you claim") and authorization ("you may do this") are different. Even when `[Authorize]` is at a higher layer, check **resource-level authorization**: does the code verify the caller owns or may access *this specific resource*, not just that they're authenticated? Broken object-level authorization (BOLA/IDOR) is among the most common API vulnerabilities — a valid token for user A must not grant access to user B's data.

Also check: any place the code executes user-supplied scripts or expressions (Roslyn, Jint, JS eval, dynamic LINQ) is essentially RCE-by-design — confirm it's gated behind admin auth and sandboxed, or call it out as a design risk.

**Step 5 — Check error handling and information leakage**
Does the code expose stack traces, internal paths, database errors, or implementation details to callers? Does it behave differently in ways that leak information (timing differences, distinct error messages for "user not found" vs "wrong password")?

**Step 6 — Check cryptography and secrets**
Is sensitive data encrypted at rest and in transit? Are secrets hardcoded or committed (.env, appsettings, test fixtures)? Are cryptographic primitives used correctly (correct mode, IV/nonce uniqueness, key length, password hashing with a slow KDF and a sane work factor)? Are tokens/passwords being logged? Are required minimum key/secret lengths enforced (and the enforcement actually reachable)?

**Step 7 — Check dependencies and supply chain**
Are third-party packages pinned? Are there known-vulnerable versions in lockfiles? Does the code load remote resources (scripts, schemas, models) from URLs that could be tampered with? Are integrity checks (SRI, checksums, signed packages) used where they should be? OWASP A06 lives here.

## How to structure your output

Lead with a **verdict**: safe / has issues / critical issues.

Then list findings in descending severity order. For each finding:
- **What**: name the vulnerability class (path traversal, IDOR, unbounded memory read, etc.)
- **Where**: point to the specific line or pattern
- **Why it matters**: explain the realistic impact and how an attacker could exploit it — don't just name the CWE
- **Fix**: give a concrete, correct fix, not a vague recommendation

If something looks risky but is acceptable in context (e.g., TLS validation disabled in a research tool, certificate pinning skipped for a dev proxy), note it as a **design decision**, not a bug, and flag what would need to change if the context changed.

End with a **coverage note** for things you couldn't assess from the snippet alone (missing auth layer, downstream validation, callers, configuration).

## Worked example (abbreviated)

Code under review: a controller action that streams a file from disk by name and reads it into memory.

```
Verdict: critical issues

[Critical] Path traversal — `Path.Combine(root, name)` with `name` from query string; `..` segments escape `root`. Fix: resolve to absolute path and assert it starts with `Path.GetFullPath(root)`; reject otherwise.

[High] Unbounded read — `File.ReadAllBytesAsync(path)` materializes the whole file. A 10 GB file pins memory. Fix: stream with `FileStreamResult` and a MaxRequestBodySize-bounded pipeline.

[Medium] Information disclosure — exception handler returns `ex.Message` to caller. Leaks paths and EF errors. Fix: log the exception, return a generic 500 problem-detail.

[Low / design] No rate limit on this endpoint — accepted in context (admin-only), but flag if it ever becomes anonymous.

Coverage note: did not see the calling middleware; if `[Authorize]` is missing the path traversal escalates from data-disclosure to remote read of arbitrary host files.
```

The point: catch the path traversal *and* the unbounded read *and* the info-leak. Severity-ordered, concrete fixes, honest scope.
