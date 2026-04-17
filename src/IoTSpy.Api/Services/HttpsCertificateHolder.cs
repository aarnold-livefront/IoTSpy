using System.Security.Cryptography.X509Certificates;

namespace IoTSpy.Api.Services;

/// <summary>
/// Singleton that holds the current HTTPS certificate for dynamic Kestrel configuration.
/// Populated either from a cert file at startup or by CertesLetsEncryptService after ACME issuance.
/// </summary>
public sealed class HttpsCertificateHolder
{
    private volatile X509Certificate2? _certificate;

    public X509Certificate2? Certificate
    {
        get => _certificate;
        set => _certificate = value;
    }
}
