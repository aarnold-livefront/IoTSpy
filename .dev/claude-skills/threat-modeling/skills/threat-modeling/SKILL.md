---
name: threat-modeling
description: >
  Structured threat modeling for systems, features, and architecture changes. Use this skill whenever someone asks for a threat model, security design review, or wants to think through what could go wrong with a new feature or system before building it. Also trigger when someone describes a design and asks "is this secure?", "what are the risks?", "how could this be abused?", or "what do I need to think about security-wise?" — even if they don't use the phrase "threat model". Works for any system: APIs, microservices, authentication flows, data pipelines, IoT/embedded, networked devices.
---

# Threat Modeling

Threat modeling answers one question: *what could go wrong, and does it matter enough to fix?* Frameworks help structure the thinking — they don't replace it. Start by understanding the system, then apply structured analysis to find threats you might otherwise miss.

## The process

### 1. Understand the system first

Before naming threats, establish:
- **What does it do?** What's the core function, what data does it handle, what's the happy path?
- **Who are the actors?** Legitimate users, admins, external services, anonymous callers — and potential adversaries
- **What's valuable?** What would an attacker want to steal, corrupt, disrupt, or impersonate?
- **What are the trust boundaries?** Where does data or control flow from one trust level to another — across a network, across a process boundary, between user roles, between internal and external systems?

Sketch the data flows and trust boundaries explicitly, even if just in prose. Threats live at boundaries.

### 2. Enumerate threats with STRIDE

For each trust boundary crossing and each significant component, work through STRIDE:

- **Spoofing** — Can an actor falsely claim an identity? (Credential theft, token replay, DNS spoofing, ARP poisoning)
- **Tampering** — Can data be modified in transit or at rest without detection? (MITM, SQL injection, parameter manipulation, log tampering)
- **Repudiation** — Can an actor deny an action they took? (Missing audit logs, unsigned operations, log deletion)
- **Information Disclosure** — Can sensitive data reach an unauthorized party? (IDOR, over-permissive APIs, error messages, side channels)
- **Denial of Service** — Can an actor degrade or deny availability? (Resource exhaustion, algorithmic complexity, missing rate limits, state exhaustion)
- **Elevation of Privilege** — Can an actor gain more access than intended? (Role bypass, JWT manipulation, path traversal to protected resources, deserialization)

For each threat that applies: name it, describe the realistic attack path (who does what, from where), and estimate impact and likelihood.

### 3. Layer in OWASP and ATT&CK where relevant

STRIDE is comprehensive but abstract. Supplement with:

**OWASP Top 10** — A grounding check on the most common real-world web/API vulnerabilities: broken access control, cryptographic failures, injection, insecure design, security misconfiguration, vulnerable components, authentication failures, software integrity failures, logging failures, SSRF. Run through these as a sanity check on your STRIDE output.

**MITRE ATT&CK (including ICS/IoT)** — Useful when the system interacts with networks, devices, or operational infrastructure. ATT&CK frames threats in terms of attacker TTPs (tactics, techniques, procedures), which is helpful for understanding realistic attack chains rather than isolated vulnerabilities. For IoT/OT systems, ATT&CK for ICS adds techniques like protocol abuse, firmware manipulation, and lateral movement through device trust chains.

Use these as lenses, not checklists. A threat that doesn't have a plausible attacker or realistic impact isn't worth prioritizing just because a framework mentions it.

### 4. Prioritize by risk

Not all threats are equal. Prioritize by:
- **Impact**: What's the worst realistic outcome if this is exploited? (Data loss, service outage, lateral movement, compliance breach)
- **Likelihood**: How easy is this to exploit, given realistic attacker capability and access?
- **Exploitability**: Is there a known technique or does it require sophisticated custom work?

Flag at minimum: *critical* (exploit likely, impact severe), *high* (one or both elevated), *medium* (real but limited impact or low likelihood), *low* (theoretical, unlikely to matter in practice). Be honest — if something is low priority, say so rather than burying it in a uniform list.

### 5. Recommend mitigations

For each significant threat, propose a mitigation. Good mitigations are:
- **Specific**: not "add authentication" but "add resource-level authorization checks verifying the requesting user owns the resource before returning it"
- **Proportionate**: the control should match the risk; don't recommend HSMs for low-value data
- **Practical**: acknowledge operational burden; a control that won't be maintained is worse than no control

## Output format

Structure your output as:

**System summary** — 2–3 sentences on what you're analyzing and what's in scope

**Trust boundary map** — prose or list of where trust level changes

**Threat findings** — grouped by severity (critical → high → medium → low), each with: threat name, attack path, impact, likelihood, recommended mitigation

**Out of scope / assumptions** — what you couldn't assess, what you assumed

Keep it actionable. A threat model that produces a list of 40 equally-weighted items is less useful than one that clearly identifies the 3 things that matter most and explains why.
