using System.Text.Json.Serialization;

namespace IoTSpy.Api.Services;

/// <summary>
/// Sidecar file (Name.manifest.json) that accompanies each plugin DLL.
/// See ADR 0001 for the signing workflow.
/// </summary>
public sealed class PluginManifest
{
    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; set; } = "";

    /// <summary>Lowercase hex SHA-256 of the DLL bytes.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    /// <summary>Base64 RSA-SHA256 (PKCS#1) or ECDSA-SHA256 signature over the raw SHA-256 hash bytes.</summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";

    /// <summary>Base64 DER-encoded X.509 certificate holding the author's public key.</summary>
    [JsonPropertyName("signerCertificate")]
    public string SignerCertificate { get; set; } = "";
}
