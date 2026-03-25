using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Proxy.Tls;

public class CertificateAuthority(
    IServiceScopeFactory scopeFactory,
    ILogger<CertificateAuthority> logger) : ICertificateAuthority
{
    private static readonly SecureRandom SecureRandom = new();
    private CertificateEntry? _cachedRootCa;
    private AsymmetricCipherKeyPair? _cachedRootKeyPair;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CertificateEntry> _hostCertCache = new();

    public async Task<CertificateEntry> GetOrCreateRootCaAsync(CancellationToken ct = default)
    {
        if (_cachedRootCa is not null) return _cachedRootCa;

        await _lock.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICertificateRepository>();

            var existing = await repository.GetRootCaAsync(ct);
            if (existing is not null)
            {
                _cachedRootCa = existing;
                _cachedRootKeyPair = LoadKeyPair(existing.PrivateKeyPem);
                return existing;
            }

            logger.LogInformation("Generating new IoTSpy root CA certificate...");
            var (entry, keyPair) = GenerateRootCa();
            _cachedRootCa = await repository.SaveAsync(entry, ct);
            _cachedRootKeyPair = keyPair;
            logger.LogInformation("Root CA generated: Serial={Serial}", entry.SerialNumber);
            return _cachedRootCa;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Apple requires TLS leaf certificates to have a validity period ≤ 398 days
    // (policy effective September 1 2020, applies to all certs regardless of CA trust type).
    // Use 397 to stay safely under the limit and leave a one-day buffer.
    private const int MaxLeafValidityDays = 397;

    public async Task<CertificateEntry> GetOrCreateHostCertificateAsync(string hostname, CancellationToken ct = default)
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(1);
        if (_hostCertCache.TryGetValue(hostname, out var cached)
            && cached.NotAfter > expiry
            && (cached.NotAfter - cached.NotBefore).TotalDays <= MaxLeafValidityDays)
            return cached;

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICertificateRepository>();

        var existing = await repository.GetByHostnameAsync(hostname, ct);
        // Reject cached certs that exceed Apple's 398-day limit — they were generated
        // before the validity was corrected and will be silently rejected by iOS/macOS.
        if (existing is not null && existing.NotAfter > expiry
            && (existing.NotAfter - existing.NotBefore).TotalDays <= MaxLeafValidityDays)
        {
            _hostCertCache[hostname] = existing;
            return existing;
        }

        var rootCa = await GetOrCreateRootCaAsync(ct);
        var entry = GenerateHostCertificate(hostname, rootCa);
        var saved = await repository.SaveAsync(entry, ct);
        _hostCertCache[hostname] = saved;
        return saved;
    }

    public async Task<byte[]> ExportRootCaDerAsync(CancellationToken ct = default)
    {
        var rootCa = await GetOrCreateRootCaAsync(ct);
        var cert = X509CertificateLoader.LoadCertificate(PemToBytes(rootCa.CertificatePem));
        return cert.Export(X509ContentType.Cert);
    }

    private (CertificateEntry entry, AsymmetricCipherKeyPair keyPair) GenerateRootCa()
    {
        var keyPair = GenerateKeyPair(4096);
        var serial = BigInteger.ProbablePrime(128, SecureRandom);
        var notBefore = DateTime.UtcNow.AddDays(-1);
        var notAfter = DateTime.UtcNow.AddYears(10);

        var gen = new X509V3CertificateGenerator();
        var subject = new X509Name("CN=IoTSpy CA, O=IoTSpy, C=US");
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(subject);
        gen.SetSubjectDN(subject);
        gen.SetNotBefore(notBefore);
        gen.SetNotAfter(notAfter);
        gen.SetPublicKey(keyPair.Public);
        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        // SubjectKeyIdentifier — required to link AKI on leaf certs back to this root.
        var ski = X509ExtensionUtilities.CreateSubjectKeyIdentifier(
            SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public));
        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, ski);

        // AuthorityKeyIdentifier (self-referential) — iOS requires AKI on every cert in the
        // chain, including the root CA itself, to build the trust path to the trust anchor.
        // Proxyman and mitmproxy both include this; omitting it causes silent chain-build
        // failures in iOS even after the user enables full trust in Certificate Trust Settings.
        gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            new AuthorityKeyIdentifierStructure(keyPair.Public));

        var signer = new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private, SecureRandom);
        var cert = gen.Generate(signer);

        var entry = new CertificateEntry
        {
            CommonName = "IoTSpy CA",
            CertificatePem = CertToPem(cert),
            PrivateKeyPem = KeyToPem(keyPair.Private),
            SerialNumber = serial.ToString(16),
            NotBefore = new DateTimeOffset(notBefore, TimeSpan.Zero),
            NotAfter = new DateTimeOffset(notAfter, TimeSpan.Zero),
            IsRootCa = true
        };
        return (entry, keyPair);
    }

    private CertificateEntry GenerateHostCertificate(string hostname, CertificateEntry rootCaEntry)
    {
        var caKeyPair = _cachedRootKeyPair ?? LoadKeyPair(rootCaEntry.PrivateKeyPem);
        var caCert = LoadCert(rootCaEntry.CertificatePem);

        var keyPair = GenerateKeyPair(2048);
        var serial = BigInteger.ProbablePrime(128, SecureRandom);
        var notBefore = DateTime.UtcNow.AddDays(-1);
        var notAfter = DateTime.UtcNow.AddDays(MaxLeafValidityDays);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(caCert.SubjectDN);
        gen.SetSubjectDN(new X509Name($"CN={hostname}"));
        gen.SetNotBefore(notBefore);
        gen.SetNotAfter(notAfter);
        gen.SetPublicKey(keyPair.Public);
        gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        gen.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
        gen.AddExtension(X509Extensions.ExtendedKeyUsage, false,
            new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));

        // Subject Alternative Names — iOS/macOS require the SAN to use IPAddress for
        // IP literals; using DnsName for an IP causes the cert to be rejected outright.
        GeneralName san;
        if (System.Net.IPAddress.TryParse(hostname, out var ip))
        {
            san = new GeneralName(GeneralName.IPAddress,
                new DerOctetString(ip.GetAddressBytes()));
        }
        else
        {
            san = new GeneralName(GeneralName.DnsName, hostname);
        }
        gen.AddExtension(X509Extensions.SubjectAlternativeName, false, new GeneralNames(san));

        // Authority Key Identifier — required by iOS to build the trust chain to the root CA
        gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
            X509ExtensionUtilities.CreateAuthorityKeyIdentifier(caCert));

        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            X509ExtensionUtilities.CreateSubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public)));

        var signer = new Asn1SignatureFactory("SHA256WithRSA", caKeyPair.Private, SecureRandom);
        var cert = gen.Generate(signer);

        return new CertificateEntry
        {
            CommonName = hostname,
            SubjectAltNames = hostname,
            CertificatePem = CertToPem(cert),
            PrivateKeyPem = KeyToPem(keyPair.Private),
            SerialNumber = serial.ToString(16),
            NotBefore = new DateTimeOffset(notBefore, TimeSpan.Zero),
            NotAfter = new DateTimeOffset(notAfter, TimeSpan.Zero),
            IsRootCa = false
        };
    }

    private static AsymmetricCipherKeyPair GenerateKeyPair(int bits)
    {
        var gen = new RsaKeyPairGenerator();
        gen.Init(new Org.BouncyCastle.Crypto.KeyGenerationParameters(SecureRandom, bits));
        return gen.GenerateKeyPair();
    }

    private static Org.BouncyCastle.X509.X509Certificate LoadCert(string pem)
    {
        var parser = new X509CertificateParser();
        return parser.ReadCertificate(System.Text.Encoding.ASCII.GetBytes(pem));
    }

    private static AsymmetricCipherKeyPair LoadKeyPair(string pem)
    {
        using var reader = new System.IO.StringReader(pem);
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(reader);
        return (AsymmetricCipherKeyPair)pemReader.ReadObject();
    }

    private static string CertToPem(Org.BouncyCastle.X509.X509Certificate cert)
    {
        using var sw = new System.IO.StringWriter();
        var writer = new Org.BouncyCastle.OpenSsl.PemWriter(sw);
        writer.WriteObject(cert);
        return sw.ToString();
    }

    private static string KeyToPem(AsymmetricKeyParameter key)
    {
        using var sw = new System.IO.StringWriter();
        var writer = new Org.BouncyCastle.OpenSsl.PemWriter(sw);
        writer.WriteObject(key);
        return sw.ToString();
    }

    private static byte[] PemToBytes(string pem)
    {
        var b64 = pem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\n", "").Replace("\r", "").Trim();
        return Convert.FromBase64String(b64);
    }
}
