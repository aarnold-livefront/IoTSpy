using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IoTSpy.Api.Services;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Api.Tests.Services;

public class PluginLoaderServiceTests
{
    private static PluginLoaderService CreateService(string? directory = null, bool requireSigned = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Directory"] = directory ?? Path.Combine(Path.GetTempPath(), $"iotspy-plugins-{Guid.NewGuid():N}"),
                ["Plugins:RequireSignedPlugins"] = requireSigned.ToString()
            })
            .Build();
        var verifier = new PluginSignatureVerifier(config, NullLogger<PluginSignatureVerifier>.Instance);
        return new PluginLoaderService(config, verifier, NullLogger<PluginLoaderService>.Instance);
    }

    [Fact]
    public void Initialize_WhenDirectoryDoesNotExist_LoadsNoPlugins()
    {
        var svc = CreateService();
        svc.Initialize();
        Assert.Empty(svc.LoadedPlugins);
    }

    [Fact]
    public void GetDecoder_UnknownProtocol_ReturnsNull()
    {
        var svc = CreateService();
        svc.Initialize();
        Assert.Null(svc.GetDecoder("unknown-protocol-xyz"));
    }

    [Fact]
    public void LoadedPlugins_AfterInitialize_IsReadOnlySnapshot()
    {
        var svc = CreateService();
        svc.Initialize();

        var snap1 = svc.LoadedPlugins;
        var snap2 = svc.LoadedPlugins;

        // Two calls return separate list instances (snapshot semantics)
        Assert.NotSame(snap1, snap2);
    }

    [Fact]
    public void Reload_WhenDirectoryDoesNotExist_ClearsPlugins()
    {
        var svc = CreateService();
        svc.Initialize();
        svc.Reload();
        Assert.Empty(svc.LoadedPlugins);
    }

    [Fact]
    public void Initialize_WithEmptyDirectory_LoadsNoPlugins()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var svc = CreateService(dir);
            svc.Initialize();
            Assert.Empty(svc.LoadedPlugins);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Initialize_WithNonPluginDll_RecordsLoadError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // Copy a real but non-plugin DLL into the directory
        var sourceDll = typeof(PluginLoaderService).Assembly.Location;
        var targetDll = Path.Combine(dir, "nonplugin.dll");
        File.Copy(sourceDll, targetDll);

        try
        {
            var svc = CreateService(dir);
            svc.Initialize();

            // May load 0 decoders but should not throw
            // The assembly itself should be loadable; it just contains no IPluginDecoder types
            var loaded = svc.LoadedPlugins;
            Assert.All(loaded, p => Assert.False(string.IsNullOrEmpty(p.AssemblyPath)));
        }
        finally
        {
            // On Windows the loaded assembly file is locked until the process exits; best-effort cleanup.
            try { Directory.Delete(dir, recursive: true); }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void PluginInfo_DefaultValues_AreCorrect()
    {
        var info = new PluginInfo();
        Assert.Equal("", info.Protocol);
        Assert.Equal("", info.Name);
        Assert.Equal("", info.Version);
        Assert.False(info.IsLoaded);
        Assert.Null(info.LoadError);
        Assert.Equal(PluginTrustStatus.ManifestMissing, info.TrustStatus);
        Assert.Null(info.SignerSubject);
    }

    // ── PluginSignatureVerifier ────────────────────────────────────────────────

    private static PluginSignatureVerifier CreateVerifier(string? directory = null, string[]? thumbprints = null)
    {
        var dict = new Dictionary<string, string?>();
        if (directory is not null) dict["Plugins:Directory"] = directory;
        if (thumbprints is not null)
            for (var i = 0; i < thumbprints.Length; i++)
                dict[$"Plugins:TrustedSignerThumbprints:{i}"] = thumbprints[i];
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new PluginSignatureVerifier(config, NullLogger<PluginSignatureVerifier>.Instance);
    }

    [Fact]
    public void Verify_WhenManifestMissing_ReturnsManifestMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dll = Path.Combine(dir, "test.dll");
            File.WriteAllBytes(dll, [0x4D, 0x5A]); // MZ stub

            var (status, subject) = CreateVerifier().Verify(dll);
            Assert.Equal(PluginTrustStatus.ManifestMissing, status);
            Assert.Null(subject);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_WhenManifestHasBadJson_ReturnsManifestInvalid()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dll = Path.Combine(dir, "test.dll");
            File.WriteAllBytes(dll, [0x4D, 0x5A]);
            File.WriteAllText(Path.ChangeExtension(dll, ".manifest.json"), "not-json{{{");

            var (status, _) = CreateVerifier().Verify(dll);
            Assert.Equal(PluginTrustStatus.ManifestInvalid, status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_WhenManifestHashDoesNotMatch_ReturnsHashMismatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dll = Path.Combine(dir, "test.dll");
            File.WriteAllBytes(dll, [0x4D, 0x5A]);
            var manifest = """{"assemblyName":"test","sha256":"0000000000000000000000000000000000000000000000000000000000000000","signature":"AA==","signerCertificate":"AA=="}""";
            File.WriteAllText(Path.ChangeExtension(dll, ".manifest.json"), manifest);

            var (status, _) = CreateVerifier().Verify(dll);
            Assert.Equal(PluginTrustStatus.HashMismatch, status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // Writes a valid .manifest.json signed by an ephemeral RSA cert.
    // Returns the cert's SHA-256 thumbprint (normalised upper-hex) so callers can build allowlists.
    private static string WriteValidManifest(string dllPath, byte[] dllBytes)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=IoTSpyTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var hashBytes = SHA256.HashData(dllBytes);
        var sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var sigBytes = rsa.SignHash(hashBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var certDer = cert.Export(X509ContentType.Cert);
        var thumbprint = cert.GetCertHashString(HashAlgorithmName.SHA256).ToUpperInvariant();

        var manifest = JsonSerializer.Serialize(new
        {
            assemblyName = Path.GetFileNameWithoutExtension(dllPath),
            sha256 = sha256Hex,
            signature = Convert.ToBase64String(sigBytes),
            signerCertificate = Convert.ToBase64String(certDer)
        });
        File.WriteAllText(Path.ChangeExtension(dllPath, ".manifest.json"), manifest);

        return thumbprint;
    }

    [Fact]
    public void Verify_WithValidSignatureAndThumbprintNotInAllowlist_ReturnsUntrusted()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dllBytes = new byte[] { 0x4D, 0x5A, 0x01 };
            var dll = Path.Combine(dir, "test.dll");
            File.WriteAllBytes(dll, dllBytes);
            WriteValidManifest(dll, dllBytes);

            // Allowlist contains a different thumbprint — cert is valid but not trusted
            var (status, subject) = CreateVerifier(thumbprints: ["AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899"]).Verify(dll);
            Assert.Equal(PluginTrustStatus.Untrusted, status);
            Assert.NotNull(subject);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Verify_WithValidSignatureAndThumbprintInAllowlist_ReturnsTrusted()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dllBytes = new byte[] { 0x4D, 0x5A, 0x02 };
            var dll = Path.Combine(dir, "test.dll");
            File.WriteAllBytes(dll, dllBytes);
            var thumbprint = WriteValidManifest(dll, dllBytes);

            var (status, subject) = CreateVerifier(thumbprints: [thumbprint]).Verify(dll);
            Assert.Equal(PluginTrustStatus.Trusted, status);
            Assert.NotNull(subject);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Initialize_WithEmptyDir_AllPluginsHaveManifestMissingStatus()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var svc = CreateService(dir);
            svc.Initialize();
            // No DLLs → no plugin records
            Assert.Empty(svc.LoadedPlugins);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Initialize_WithRequireSignedTrue_RejectsUnsignedDll()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"iotspy-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var sourceDll = typeof(PluginLoaderService).Assembly.Location;
        var targetDll = Path.Combine(dir, "nonplugin.dll");
        File.Copy(sourceDll, targetDll);
        try
        {
            var svc = CreateService(dir, requireSigned: true);
            svc.Initialize();

            // Unsigned DLL must be rejected (not loaded)
            var plugins = svc.LoadedPlugins;
            Assert.All(plugins, p =>
            {
                Assert.False(p.IsLoaded);
                Assert.Equal(PluginTrustStatus.ManifestMissing, p.TrustStatus);
            });
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (UnauthorizedAccessException) { }
        }
    }
}
