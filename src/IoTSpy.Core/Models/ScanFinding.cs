using IoTSpy.Core.Enums;

namespace IoTSpy.Core.Models;

public class ScanFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }
    public ScanFindingType Type { get; set; }
    public ScanFindingSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Port scan / service info
    public int? Port { get; set; }
    public string? Protocol { get; set; } // "tcp" / "udp"
    public string? ServiceName { get; set; } // e.g. "ssh", "http", "mqtt"
    public string? Banner { get; set; } // raw banner text
    public string? Cpe { get; set; } // CPE 2.3 string, e.g. "cpe:2.3:a:openbsd:openssh:8.9"

    // Credential test
    public string? Username { get; set; }
    public string? Password { get; set; }

    // CVE info
    public string? CveId { get; set; } // e.g. "CVE-2023-12345"
    public double? CvssScore { get; set; }
    public string? CveDescription { get; set; }
    public string? Reference { get; set; } // URL to advisory

    public DateTimeOffset FoundAt { get; set; } = DateTimeOffset.UtcNow;
}
