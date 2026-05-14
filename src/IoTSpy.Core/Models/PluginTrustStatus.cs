namespace IoTSpy.Core.Models;

public enum PluginTrustStatus
{
    /// <summary>Manifest present, hash matches, signature valid, signer cert trusted.</summary>
    Trusted,
    /// <summary>Signature valid but signer cert thumbprint not in TrustedSignerThumbprints.</summary>
    Untrusted,
    /// <summary>No companion .manifest.json file found alongside the DLL.</summary>
    ManifestMissing,
    /// <summary>Manifest file could not be parsed.</summary>
    ManifestInvalid,
    /// <summary>DLL SHA-256 does not match manifest sha256 field.</summary>
    HashMismatch,
    /// <summary>Signature verification failed (wrong key, corrupted manifest, etc.).</summary>
    SignatureInvalid
}
