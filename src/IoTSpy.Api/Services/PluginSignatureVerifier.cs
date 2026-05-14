using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IoTSpy.Core.Models;

namespace IoTSpy.Api.Services;

/// <summary>
/// Verifies the integrity and trust of a plugin DLL against its companion manifest.
/// See ADR 0001 for the full signing protocol.
/// </summary>
public sealed class PluginSignatureVerifier
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlySet<string> _trustedThumbprints;
    private readonly ILogger<PluginSignatureVerifier> _logger;

    public int TrustedThumbprintCount => _trustedThumbprints.Count;

    public PluginSignatureVerifier(IConfiguration config, ILogger<PluginSignatureVerifier> logger)
    {
        _logger = logger;
        var raw = config.GetSection("Plugins:TrustedSignerThumbprints").Get<string[]>() ?? [];
        _trustedThumbprints = raw
            .Select(t => t.ToUpperInvariant().Replace(":", "").Replace(" ", ""))
            .ToHashSet();
    }

    /// <summary>
    /// Returns the trust status of the DLL at <paramref name="dllPath"/> and the
    /// subject of the signing certificate (if one was present and parseable).
    /// </summary>
    public (PluginTrustStatus Status, string? SignerSubject) Verify(string dllPath)
    {
        var manifestPath = Path.ChangeExtension(dllPath, ".manifest.json");
        if (!File.Exists(manifestPath))
            return (PluginTrustStatus.ManifestMissing, null);

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(
                File.ReadAllText(manifestPath), _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Plugin manifest {Path} is not valid JSON: {Error}", manifestPath, ex.Message);
            return (PluginTrustStatus.ManifestInvalid, null);
        }

        if (manifest is null || string.IsNullOrEmpty(manifest.Sha256)
                              || string.IsNullOrEmpty(manifest.Signature)
                              || string.IsNullOrEmpty(manifest.SignerCertificate))
        {
            _logger.LogWarning("Plugin manifest {Path} is missing required fields", manifestPath);
            return (PluginTrustStatus.ManifestInvalid, null);
        }

        // 1. Verify DLL hash
        byte[] dllBytes;
        byte[] hashBytes;
        try
        {
            dllBytes = File.ReadAllBytes(dllPath);
            hashBytes = SHA256.HashData(dllBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot read plugin DLL {Path}: {Error}", dllPath, ex.Message);
            return (PluginTrustStatus.HashMismatch, null);
        }

        var computedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!computedHex.Equals(manifest.Sha256.ToLowerInvariant(), StringComparison.Ordinal))
        {
            _logger.LogWarning("Plugin {Path} SHA-256 mismatch (manifest: {Expected}, actual: {Actual})",
                dllPath, manifest.Sha256, computedHex);
            return (PluginTrustStatus.HashMismatch, null);
        }

        // 2. Load signer certificate
        X509Certificate2 cert;
        try
        {
            cert = new X509Certificate2(Convert.FromBase64String(manifest.SignerCertificate));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Plugin manifest {Path} contains invalid signerCertificate: {Error}", manifestPath, ex.Message);
            return (PluginTrustStatus.SignatureInvalid, null);
        }

        using (cert)
        {
            var subject = cert.Subject;

            // 3. Verify signature (RSA-SHA256 PKCS#1 or ECDSA-SHA256)
            bool signatureValid;
            try
            {
                var sigBytes = Convert.FromBase64String(manifest.Signature);
                using var rsa = cert.GetRSAPublicKey();
                if (rsa is not null)
                {
                    signatureValid = rsa.VerifyHash(hashBytes, sigBytes,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                else
                {
                    using var ecdsa = cert.GetECDsaPublicKey();
                    signatureValid = ecdsa?.VerifyHash(hashBytes, sigBytes) ?? false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Signature verification failed for plugin {Path}: {Error}", dllPath, ex.Message);
                return (PluginTrustStatus.SignatureInvalid, subject);
            }

            if (!signatureValid)
            {
                _logger.LogWarning("Plugin {Path} has invalid signature (signer: {Subject})", dllPath, subject);
                return (PluginTrustStatus.SignatureInvalid, subject);
            }

            // 4. Check signer thumbprint against allowlist
            if (_trustedThumbprints.Count > 0)
            {
                var thumbprint = cert.GetCertHashString(HashAlgorithmName.SHA256).ToUpperInvariant();
                if (!_trustedThumbprints.Contains(thumbprint))
                {
                    _logger.LogWarning(
                        "Plugin {Path} signed by untrusted cert {Subject} (thumbprint: {Thumb}); add to Plugins:TrustedSignerThumbprints to trust",
                        dllPath, subject, thumbprint);
                    return (PluginTrustStatus.Untrusted, subject);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Plugin {Path}: Plugins:TrustedSignerThumbprints is empty; signer {Subject} not cross-checked",
                    dllPath, subject);
            }

            return (PluginTrustStatus.Trusted, subject);
        }
    }
}
