# ADR 0001 — Plugin signing via author certificate

**Date:** 2026-05-14  
**Status:** Accepted  
**Deciders:** Annalise  
**Closes:** CODE-REVIEW-FINDINGS #16

---

## Context

`PluginLoaderService` loads arbitrary `.dll` files from a configured directory using `AssemblyLoadContext`. Any DLL placed in that directory — including a malicious one — is loaded and executed with full process privileges. An admin-role user, or an attacker who can write to the plugins directory, could use this to run arbitrary code.

## Decision

**Plugin authors sign their DLLs.** Each DLL ships with a companion `<Name>.manifest.json` that contains:

| Field | Contents |
|---|---|
| `assemblyName` | DLL filename without extension |
| `sha256` | Lowercase hex SHA-256 of the DLL bytes |
| `signature` | Base64 RSA-SHA256 (PKCS#1) or ECDSA-SHA256 signature over the raw SHA-256 hash bytes |
| `signerCertificate` | Base64 DER-encoded X.509 certificate holding the author's public key |

IoTSpy administrators configure trusted signer certificate thumbprints in `appsettings.json`:

```json
"Plugins": {
  "Directory": "plugins",
  "RequireSignedPlugins": false,
  "TrustedSignerThumbprints": [
    "AABBCC..."
  ]
}
```

**Verification steps on load:**

1. Manifest file present → else `ManifestMissing`
2. SHA-256 of DLL bytes matches `sha256` in manifest → else `HashMismatch`
3. `signature` verifies against `sha256` bytes using cert's public key → else `SignatureInvalid`
4. Cert SHA-256 thumbprint is in `TrustedSignerThumbprints` (if list is non-empty) → else `Untrusted`

**Enforcement mode** (`RequireSignedPlugins`):

- `false` (default): trust status is computed and surfaced in the admin UI, but unsigned/untrusted plugins still load. Operators can audit trust before enforcing.
- `true`: plugins with any non-`Trusted` status are refused; the admin UI shows the rejection reason.

## Alternatives considered

1. **Static SHA-256 allowlist** — simple, but requires allowlist update for every plugin rebuild; doesn't identify the author.
3. **Trust-on-first-use** — doesn't prevent a malicious first load.
4. **Admin UI attestation only** — no code-level enforcement; rejected as insufficient.

## Consequences

- Plugin authors need a self-signed or CA-issued certificate and a signing script/tool.
- Admins must obtain the author's cert thumbprint out-of-band and add it to config.
- `PluginInfo` gains `TrustStatus` and `SignerSubject` fields visible in the admin UI and API.
- No change to plugin DLL format; the manifest is a sidecar file.
- The `RequireSignedPlugins=false` default means zero breaking change for existing deployments.
