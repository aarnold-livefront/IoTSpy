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

    public async Task<CertificateEntry> GetOrCreateHostCertificateAsync(string hostname, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICertificateRepository>();

        var existing = await repository.GetByHostnameAsync(hostname, ct);
        if (existing is not null && existing.NotAfter > DateTimeOffset.UtcNow.AddDays(1))
            return existing;

        var rootCa = await GetOrCreateRootCaAsync(ct);
        var entry = GenerateHostCertificate(hostname, rootCa);
        return await repository.SaveAsync(entry, ct);
    }

    public async Task<byte[]> ExportRootCaDerAsync(CancellationToken ct = default)
    {
        var rootCa = await GetOrCreateRootCaAsync(ct);
        var cert = new X509Certificate2(PemToBytes(rootCa.CertificatePem));
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
        gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
            new SubjectKeyIdentifierStructure(keyPair.Public));

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
        var notAfter = DateTime.UtcNow.AddDays(825);

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
            new ExtendedKeyUsage(KeyPurposeID.IdKPServerAuth));

        // Subject Alternative Names
        var sanList = new GeneralNames(new GeneralName(GeneralName.DnsName, hostname));
        gen.AddExtension(X509Extensions.SubjectAlternativeName, false, sanList);

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
