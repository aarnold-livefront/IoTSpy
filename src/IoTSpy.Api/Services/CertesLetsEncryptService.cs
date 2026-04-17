using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;

namespace IoTSpy.Api.Services;

/// <summary>
/// Hosted service that obtains and renews a TLS certificate from Let's Encrypt using the
/// Certes ACME client library. Activated when Kestrel:LetsEncrypt:Enabled is true.
///
/// HTTP-01 challenge tokens are served by AcmeChallengeMiddleware registered in Program.cs.
/// The issued certificate is stored in HttpsCertificateHolder for dynamic Kestrel selection.
/// </summary>
public sealed class CertesLetsEncryptService : IHostedService, IDisposable
{
    private readonly LetsEncryptOptions _options;
    private readonly HttpsCertificateHolder _certHolder;
    private readonly ILogger<CertesLetsEncryptService> _logger;

    private readonly Dictionary<string, string> _challenges = new();
    private Timer? _renewalTimer;
    private AcmeContext? _acme;

    public CertesLetsEncryptService(
        IConfiguration configuration,
        HttpsCertificateHolder certHolder,
        ILogger<CertesLetsEncryptService> logger)
    {
        _options = configuration.GetSection(LetsEncryptOptions.SectionName).Get<LetsEncryptOptions>()
            ?? throw new InvalidOperationException("Kestrel:LetsEncrypt config section missing");
        _certHolder = certHolder;
        _logger = logger;
    }

    public bool TryGetChallenge(string token, out string keyAuthorization)
        => _challenges.TryGetValue(token, out keyAuthorization!);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;

        // Load existing cert if not yet expired with comfortable margin
        if (TryLoadExistingCertificate()) return;

        await AcquireCertificateAsync(cancellationToken);

        // Renew 30 days before expiry; check daily
        _renewalTimer = new Timer(
            _ => _ = RenewIfNeededAsync(CancellationToken.None),
            null,
            TimeSpan.FromHours(12),
            TimeSpan.FromHours(24));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _renewalTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private bool TryLoadExistingCertificate()
    {
        if (!File.Exists(_options.CertificatePath)) return false;

        try
        {
            var pfxBytes = File.ReadAllBytes(_options.CertificatePath);
            var cert = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                _options.CertificatePassword,
                X509KeyStorageFlags.Exportable);

            if (cert.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30))
            {
                _certHolder.Certificate = cert;
                _logger.LogInformation("Loaded existing Let's Encrypt certificate for {Domain}, expires {Expiry}",
                    _options.Domain, cert.NotAfter);
                return true;
            }
            _logger.LogInformation("Existing Let's Encrypt certificate for {Domain} expires {Expiry} — renewing",
                _options.Domain, cert.NotAfter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load existing certificate from {Path}", _options.CertificatePath);
        }

        return false;
    }

    private async Task RenewIfNeededAsync(CancellationToken ct)
    {
        var cert = _certHolder.Certificate;
        if (cert is null || cert.NotAfter.ToUniversalTime() < DateTime.UtcNow.AddDays(30))
        {
            _logger.LogInformation("Renewing Let's Encrypt certificate for {Domain}", _options.Domain);
            await AcquireCertificateAsync(ct);
        }
    }

    private async Task AcquireCertificateAsync(CancellationToken ct)
    {
        try
        {
            var server = _options.UseStagingServer
                ? WellKnownServers.LetsEncryptStagingV2
                : WellKnownServers.LetsEncryptV2;

            _acme = new AcmeContext(server);

            // Create or recover account
            await _acme.NewAccount(_options.Email, termsOfServiceAgreed: true);

            var order = await _acme.NewOrder([_options.Domain]);
            var auth = (await order.Authorizations()).First();
            var httpChallenge = await auth.Http();

            // Store token so AcmeChallengeMiddleware can serve it
            _challenges[httpChallenge.Token] = httpChallenge.KeyAuthz;
            _logger.LogDebug("Serving ACME challenge token {Token}", httpChallenge.Token);

            // Allow ACME server to fetch the challenge
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            await httpChallenge.Validate();

            // Poll for authorization completion (up to 60 s)
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                var authResource = await auth.Resource();
                if (authResource.Status == AuthorizationStatus.Valid) break;
                if (authResource.Status == AuthorizationStatus.Invalid)
                    throw new InvalidOperationException($"ACME HTTP-01 challenge failed for {_options.Domain}");
            }

            _challenges.Remove(httpChallenge.Token);

            // Generate key + CSR and finalize the order
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);
            var cert = await order.Generate(new CsrInfo { CommonName = _options.Domain }, privateKey);

            // Export as PFX
            var pfxPassword = _options.CertificatePassword ?? "";
            var pfxBuilder = cert.ToPfx(privateKey);
            var pfxBytes = pfxBuilder.Build(_options.Domain, pfxPassword);

            var certDir = Path.GetDirectoryName(_options.CertificatePath);
            if (!string.IsNullOrEmpty(certDir))
                System.IO.Directory.CreateDirectory(certDir);

            await File.WriteAllBytesAsync(_options.CertificatePath, pfxBytes, ct);

            var x509 = X509CertificateLoader.LoadPkcs12(pfxBytes, pfxPassword, X509KeyStorageFlags.Exportable);
            _certHolder.Certificate = x509;

            _logger.LogInformation("Let's Encrypt certificate issued for {Domain}, expires {Expiry}",
                _options.Domain, x509.NotAfter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Let's Encrypt certificate for {Domain}", _options.Domain);
        }
    }

    public void Dispose()
    {
        _renewalTimer?.Dispose();
        _acme = null;
    }
}

public sealed class LetsEncryptOptions
{
    public const string SectionName = "Kestrel:LetsEncrypt";

    public bool Enabled { get; set; } = false;
    public string Email { get; set; } = "";
    public string Domain { get; set; } = "";
    public bool UseStagingServer { get; set; } = false;
    public string CertificatePath { get; set; } = "/data/certs/iotspy-le.pfx";
    public string? CertificatePassword { get; set; }
}
