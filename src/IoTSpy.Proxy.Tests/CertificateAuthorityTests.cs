using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy.Tls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace IoTSpy.Proxy.Tests;

/// <summary>
/// Verifies that the generated root CA and leaf certificates include the X.509 extensions
/// required for full trust on iOS devices (HT210176 / formerly HT5012).
///
/// iOS trust requirements checklist:
///   Root CA  — BasicConstraints(CA=true), KeyUsage(keyCertSign|cRLSign),
///              SubjectKeyIdentifier, AuthorityKeyIdentifier (self-referential)
///   Leaf     — BasicConstraints(CA=false), KeyUsage(digitalSignature|keyEncipherment),
///              ExtendedKeyUsage(serverAuth), SubjectAltName, SubjectKeyIdentifier,
///              AuthorityKeyIdentifier (links to root SKI), validity ≤ 398 days
/// </summary>
public class CertificateAuthorityTests
{
    // OIDs used to locate extensions in System.Security.Cryptography.X509Certificates
    private const string OidBasicConstraints     = "2.5.29.19";
    private const string OidKeyUsage             = "2.5.29.15";
    private const string OidSubjectKeyIdentifier = "2.5.29.14";
    private const string OidAuthorityKeyId       = "2.5.29.35";
    private const string OidExtendedKeyUsage     = "2.5.29.37";
    private const string OidSubjectAltName       = "2.5.29.17";

    // ── Root CA extension tests ──────────────────────────────────────────────

    [Fact]
    public async Task RootCa_HasAuthorityKeyIdentifier()
    {
        var ca = BuildCa(out _);
        var cert = await LoadRootCaX509(ca);

        Assert.NotNull(cert.Extensions[OidAuthorityKeyId]);
    }

    [Fact]
    public async Task RootCa_HasSubjectKeyIdentifier()
    {
        var ca = BuildCa(out _);
        var cert = await LoadRootCaX509(ca);

        Assert.NotNull(cert.Extensions[OidSubjectKeyIdentifier]);
    }

    [Fact]
    public async Task RootCa_BasicConstraints_IsCa()
    {
        var ca = BuildCa(out _);
        var cert = await LoadRootCaX509(ca);

        var bc = cert.Extensions[OidBasicConstraints] as X509BasicConstraintsExtension;
        Assert.NotNull(bc);
        Assert.True(bc.CertificateAuthority);
    }

    [Fact]
    public async Task RootCa_HasKeyUsage_KeyCertSign_And_CrlSign()
    {
        var ca = BuildCa(out _);
        var cert = await LoadRootCaX509(ca);

        var ku = cert.Extensions[OidKeyUsage] as X509KeyUsageExtension;
        Assert.NotNull(ku);
        Assert.True((ku.KeyUsages & X509KeyUsageFlags.KeyCertSign) != 0);
        Assert.True((ku.KeyUsages & X509KeyUsageFlags.CrlSign) != 0);
    }

    // ── Leaf certificate extension tests ────────────────────────────────────

    [Fact]
    public async Task LeafCert_HasAuthorityKeyIdentifier()
    {
        var ca = BuildCa(out var repo);
        CertificateEntry? savedRoot = null;
        repo.SaveAsync(Arg.Any<CertificateEntry>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var e = c.Arg<CertificateEntry>();
                if (e.IsRootCa) savedRoot = e;
                return e;
            });
        repo.GetRootCaAsync(Arg.Any<CancellationToken>())
            .Returns(_ => savedRoot);

        var leafEntry = await ca.GetOrCreateHostCertificateAsync("example.com");
        var cert = PemToX509(leafEntry.CertificatePem);

        Assert.NotNull(cert.Extensions[OidAuthorityKeyId]);
    }

    [Fact]
    public async Task LeafCert_HasSubjectAltName()
    {
        var ca = BuildCa(out var repo);
        CertificateEntry? savedRoot = null;
        repo.SaveAsync(Arg.Any<CertificateEntry>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var e = c.Arg<CertificateEntry>();
                if (e.IsRootCa) savedRoot = e;
                return e;
            });
        repo.GetRootCaAsync(Arg.Any<CancellationToken>())
            .Returns(_ => savedRoot);

        var leafEntry = await ca.GetOrCreateHostCertificateAsync("example.com");
        var cert = PemToX509(leafEntry.CertificatePem);

        Assert.NotNull(cert.Extensions[OidSubjectAltName]);
    }

    [Fact]
    public async Task LeafCert_HasExtendedKeyUsage_ServerAuth()
    {
        var ca = BuildCa(out var repo);
        CertificateEntry? savedRoot = null;
        repo.SaveAsync(Arg.Any<CertificateEntry>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var e = c.Arg<CertificateEntry>();
                if (e.IsRootCa) savedRoot = e;
                return e;
            });
        repo.GetRootCaAsync(Arg.Any<CancellationToken>())
            .Returns(_ => savedRoot);

        var leafEntry = await ca.GetOrCreateHostCertificateAsync("example.com");
        var cert = PemToX509(leafEntry.CertificatePem);

        var eku = cert.Extensions[OidExtendedKeyUsage] as X509EnhancedKeyUsageExtension;
        Assert.NotNull(eku);
        // OidCollection is non-generic; cast to use LINQ with a lambda predicate.
        Assert.Contains(eku.EnhancedKeyUsages.Cast<Oid>(), o => o.Value == "1.3.6.1.5.5.7.3.1"); // id-kp-serverAuth
    }

    [Fact]
    public async Task LeafCert_ValidityWithin398Days()
    {
        var ca = BuildCa(out var repo);
        CertificateEntry? savedRoot = null;
        repo.SaveAsync(Arg.Any<CertificateEntry>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var e = c.Arg<CertificateEntry>();
                if (e.IsRootCa) savedRoot = e;
                return e;
            });
        repo.GetRootCaAsync(Arg.Any<CancellationToken>())
            .Returns(_ => savedRoot);

        var leafEntry = await ca.GetOrCreateHostCertificateAsync("example.com");
        var days = (leafEntry.NotAfter - leafEntry.NotBefore).TotalDays;

        Assert.True(days <= 398, $"Leaf cert validity {days:F1} days exceeds 398-day iOS limit");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="CertificateAuthority"/> backed by a NSubstitute repository mock.
    /// The mock returns null from GetRootCaAsync (simulating first run) and echoes every
    /// SaveAsync call back to the caller, capturing generated entries via <paramref name="repo"/>.
    /// </summary>
    private static CertificateAuthority BuildCa(out ICertificateRepository repo)
    {
        var mockRepo = Substitute.For<ICertificateRepository>();
        mockRepo.GetRootCaAsync(Arg.Any<CancellationToken>())
            .Returns((CertificateEntry?)null);
        mockRepo.GetByHostnameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CertificateEntry?)null);
        mockRepo.SaveAsync(Arg.Any<CertificateEntry>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<CertificateEntry>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ICertificateRepository)).Returns(mockRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        repo = mockRepo;
        return new CertificateAuthority(scopeFactory, NullLogger<CertificateAuthority>.Instance);
    }

    private static async Task<X509Certificate2> LoadRootCaX509(CertificateAuthority ca)
    {
        var entry = await ca.GetOrCreateRootCaAsync();
        return PemToX509(entry.CertificatePem);
    }

    private static X509Certificate2 PemToX509(string pem)
    {
        var b64 = pem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\n", "").Replace("\r", "").Trim();
        return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(b64));
    }
}
