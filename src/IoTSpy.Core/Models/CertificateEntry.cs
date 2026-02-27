namespace IoTSpy.Core.Models;

public class CertificateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommonName { get; set; } = string.Empty;        // hostname
    public string SubjectAltNames { get; set; } = string.Empty;   // comma-separated
    public string CertificatePem { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public bool IsRootCa { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
