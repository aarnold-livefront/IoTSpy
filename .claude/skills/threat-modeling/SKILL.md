---
name: threat-modeling
description: Structured threat modeling for systems, features, and architecture changes — STRIDE plus OWASP/ATT&CK, calibrated by impact and likelihood. Use when asked for a threat model or design security review, or when someone describes a design and asks "is this secure?" / "how could this be abused?" / "what could go wrong?"
---

# Threat Modeling

Threat modeling answers one question: *what could go wrong, and does it matter enough to fix?* Frameworks structure the thinking — they don't replace it. Understand the system first, then apply structured analysis to surface threats you might otherwise miss.

## The process

### 1. Understand the system

Before naming threats, establish:
- **What does it do?** Core function, data handled, happy path
- **Who are the actors?** Legitimate users, admins, external services, anonymous callers — and potential adversaries
- **What's valuable?** What would an attacker want to steal, corrupt, disrupt, or impersonate?
- **What are the trust boundaries?** Where does data or control flow from one trust level to another — across a network, a process boundary, between user roles, between internal and external systems?

Sketch data flows and trust boundaries explicitly, even in prose. Threats live at boundaries.

### 2. Enumerate threats with STRIDE

For each trust-boundary crossing and each significant component, work through:

- **Spoofing** — false identity claims (credential theft, token replay, DNS spoofing, ARP poisoning)
- **Tampering** — undetected modification of data in transit or at rest (MITM, SQL injection, parameter manipulation, log tampering)
- **Repudiation** — denying an action was taken (missing audit logs, unsigned operations, log deletion)
- **Information Disclosure** — sensitive data reaching unauthorized parties (IDOR, over-permissive APIs, error messages, side channels)
- **Denial of Service** — degrading or denying availability (resource exhaustion, algorithmic complexity, missing rate limits, state exhaustion)
- **Elevation of Privilege** — gaining more access than intended (role bypass, JWT manipulation, path traversal, deserialization)

For each threat that applies: name it, describe the realistic attack path (who does what, from where), estimate impact and likelihood.

### 3. Layer in OWASP and ATT&CK

STRIDE is comprehensive but abstract. Supplement with:

**OWASP Top 10** — sanity check on the most common real-world web/API vulnerabilities: broken access control, cryptographic failures, injection, insecure design, security misconfiguration, vulnerable components, authentication failures, software integrity failures, logging failures, SSRF.

**MITRE ATT&CK (including ICS/IoT)** — useful when the system touches networks, devices, or operational infrastructure. ATT&CK frames threats in terms of attacker TTPs, helping you see realistic attack chains rather than isolated vulnerabilities. ATT&CK for ICS adds protocol abuse, firmware manipulation, and lateral movement through device trust chains.

Use these as lenses, not checklists. A threat without a plausible attacker or realistic impact isn't worth prioritizing just because a framework names it.

### 4. Prioritize by risk — calibrated, not uniform

Don't return a flat list of 40 equally-weighted items. Anchor severity to concrete outcomes for *this* system:

| Severity | Anchor |
|---|---|
| **Critical** | Persistent attacker control, large-scale data exposure, auth bypass that grants admin, or undetected manipulation of operational systems |
| **High** | Bypass of a primary security control, theft of data for one user/tenant, or remote DoS that takes the system offline |
| **Medium** | Real but limited impact (single-record disclosure, partial DoS, requires already-compromised account) |
| **Low** | Theoretical, requires unrealistic attacker access, or is mitigated by defense-in-depth elsewhere |

Be honest. If something is low priority, say so rather than padding the list.

### 5. Recommend mitigations

For each significant threat, propose a mitigation that is:
- **Specific** — not "add authentication" but "add resource-level authorization checks verifying the requesting user owns the resource before returning it"
- **Proportionate** — match the control to the risk; don't recommend HSMs for low-value data
- **Practical** — acknowledge operational burden; a control that won't be maintained is worse than no control

## Dual-use tools

If the system *intentionally* does things that would be malicious in another context — TLS interception, traffic modification, port scanning, packet capture, fuzzing, scripted breakpoints — its threat model is different from a normal web app. Cover at minimum:

- **Misuse by the operator** (deliberate or accidental) — what blast radius does the tool grant a privileged user?
- **Compromise of the tool itself** — if the proxy/scanner/MITM is breached, captured data and intercepted credentials are at stake; lateral risk into intercepted networks may be material
- **Captured-data confidentiality** — encryption at rest, retention, access logging, redaction
- **Outbound legality and consent** — note when the tool's operation depends on authorization that lives outside the code (engagement scope, rules of engagement, lab isolation)

Don't bury these in generic STRIDE. They're the highest-impact threats for that class of system.

## Output format

**System summary** — 2–3 sentences on what's analyzed and what's in scope.

**Trust boundary map** — prose or list of where trust level changes.

**Threat findings** — grouped by severity (critical → high → medium → low). Each: threat name, attack path, impact, likelihood, recommended mitigation.

**Out of scope / assumptions** — what you couldn't assess; what you assumed.

A threat model that clearly identifies the 3 things that matter most and explains why is more useful than one with 40 equally-weighted items.

## Worked example (abbreviated)

> System: a small "share by link" feature — authenticated user creates a token-bearing URL that lets recipients view a single document for 7 days.

```
System summary: Token-bearing URLs scoped to one document, time-limited.
Trust boundaries: anonymous internet → API; storage of document blobs.

Critical
  Token guessing/brute force — if tokens are short or low-entropy, an attacker
  enumerates URLs and reads documents. Mitigation: 128-bit random tokens,
  rate-limit token-validation endpoint, lock after N failed attempts per IP.

High
  Token leak via logs/referer — tokens in URL path/query are logged by proxies
  and forwarded as Referer when documents include external resources. Mitigation:
  put token in fragment and validate via JS, OR use a short-lived exchange where
  the URL token is swapped for a session cookie on first hit.

Medium
  Revocation gap — owner deletes the doc but issued tokens still validate until
  expiry, depending on storage. Mitigation: validate against a "live document"
  check, not just token signature.

Low
  Side-channel timing on token validation — feasible but bounded; constant-time
  compare resolves it.

Out of scope: authn for the issuing user (assumed sound); virus scanning of
shared blobs (separate control).
```

The point: anchored severities, concrete mitigations, the 3–4 things that matter — not a flat OWASP recital.
