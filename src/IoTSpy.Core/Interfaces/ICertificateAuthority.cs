using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface ICertificateAuthority
{
    /// <summary>Generates or loads the root CA certificate.</summary>
    Task<CertificateEntry> GetOrCreateRootCaAsync(CancellationToken ct = default);

    /// <summary>Generates a leaf certificate signed by the root CA for a given hostname.</summary>
    Task<CertificateEntry> GetOrCreateHostCertificateAsync(string hostname, CancellationToken ct = default);

    /// <summary>Returns the root CA certificate in DER format for download and installation.</summary>
    Task<byte[]> ExportRootCaDerAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes the existing root CA and all leaf certificates, then generates a new root CA
    /// using the current ProxySettings CA fields. Call after changing CA customization settings.
    /// </summary>
    Task<CertificateEntry> RegenerateRootCaAsync(CancellationToken ct = default);
}
