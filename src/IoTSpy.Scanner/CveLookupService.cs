using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Scanner;

public class CveLookupService(HttpClient httpClient, ILogger<CveLookupService> logger)
{
    // OSV.dev API (free, no key required)
    private const string OsvApiUrl = "https://api.osv.dev/v1/query";

    public async Task<List<ScanFinding>> LookupAsync(
        List<ScanFinding> fingerprints,
        CancellationToken ct = default)
    {
        var findings = new List<ScanFinding>();

        foreach (var fp in fingerprints)
        {
            ct.ThrowIfCancellationRequested();
            if (fp.Cpe is not { Length: > 0 } cpe) continue;

            try
            {
                var cveFindings = await QueryOsvAsync(cpe, fp, ct);
                findings.AddRange(cveFindings);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "CVE lookup failed for CPE {Cpe}", cpe);
            }
        }

        logger.LogInformation("CVE lookup complete: {Count} vulnerabilities found", findings.Count);
        return findings;
    }

    private async Task<List<ScanFinding>> QueryOsvAsync(
        string cpe, ScanFinding source, CancellationToken ct)
    {
        var findings = new List<ScanFinding>();

        // Parse CPE to extract package/version for OSV query
        var cpeParts = cpe.Split(':');
        if (cpeParts.Length < 6) return findings;

        var product = cpeParts[4];
        var version = cpeParts[5];
        if (version == "*") return findings;

        var request = new OsvQuery
        {
            Package = new OsvPackage { Name = product, Ecosystem = "OSS-Fuzz" },
            Version = version
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync(OsvApiUrl, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("OSV API returned {Status} for {Product}:{Version}",
                    response.StatusCode, product, version);
                return findings;
            }

            var result = await response.Content.ReadFromJsonAsync<OsvResponse>(ct);
            if (result?.Vulns is null) return findings;

            foreach (var vuln in result.Vulns.Take(20)) // Limit to 20 CVEs per service
            {
                var cveId = vuln.Aliases?.FirstOrDefault(a => a.StartsWith("CVE-", StringComparison.Ordinal))
                    ?? vuln.Id;

                var severity = vuln.DatabaseSpecific?.Severity?.ToUpperInvariant() switch
                {
                    "CRITICAL" => ScanFindingSeverity.Critical,
                    "HIGH" => ScanFindingSeverity.High,
                    "MEDIUM" => ScanFindingSeverity.Medium,
                    "LOW" => ScanFindingSeverity.Low,
                    _ => ScanFindingSeverity.Medium
                };

                var cvssScore = vuln.DatabaseSpecific?.CvssScore;

                findings.Add(new ScanFinding
                {
                    Type = ScanFindingType.Cve,
                    Severity = severity,
                    Title = $"{cveId}: {Truncate(vuln.Summary ?? "Vulnerability found", 120)}",
                    Description = Truncate(vuln.Details ?? vuln.Summary ?? "No description available", 2000),
                    Port = source.Port,
                    Protocol = source.Protocol,
                    ServiceName = source.ServiceName,
                    Cpe = cpe,
                    CveId = cveId,
                    CvssScore = cvssScore,
                    CveDescription = Truncate(vuln.Summary ?? "", 2000),
                    Reference = vuln.References?.FirstOrDefault()?.Url
                });
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "OSV API request failed for {Product}:{Version}", product, version);
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Failed to parse OSV response for {Product}:{Version}", product, version);
        }

        return findings;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    // OSV API request/response models
    private sealed class OsvQuery
    {
        [JsonPropertyName("package")]
        public OsvPackage? Package { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    private sealed class OsvPackage
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("ecosystem")]
        public string? Ecosystem { get; set; }
    }

    private sealed class OsvResponse
    {
        [JsonPropertyName("vulns")]
        public List<OsvVuln>? Vulns { get; set; }
    }

    private sealed class OsvVuln
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
        [JsonPropertyName("details")]
        public string? Details { get; set; }
        [JsonPropertyName("aliases")]
        public List<string>? Aliases { get; set; }
        [JsonPropertyName("references")]
        public List<OsvReference>? References { get; set; }
        [JsonPropertyName("database_specific")]
        public OsvDatabaseSpecific? DatabaseSpecific { get; set; }
    }

    private sealed class OsvReference
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    private sealed class OsvDatabaseSpecific
    {
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
        [JsonPropertyName("cvss_score")]
        public double? CvssScore { get; set; }
    }
}
