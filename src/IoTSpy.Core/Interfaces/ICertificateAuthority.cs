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
}
