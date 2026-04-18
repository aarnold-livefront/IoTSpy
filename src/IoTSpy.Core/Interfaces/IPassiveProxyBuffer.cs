using IoTSpy.Core.Models;

namespace IoTSpy.Core.Interfaces;

public interface IPassiveProxyBuffer
{
    void Add(CapturedRequest capture);
    IReadOnlyList<CapturedRequest> Snapshot();
    void Clear();
    int Count { get; }

    /// <summary>
    /// When non-empty, only captures from these client IPs are recorded.
    /// An empty set means capture all devices.
    /// </summary>
    IReadOnlySet<string> DeviceFilter { get; }
    void SetDeviceFilter(IEnumerable<string> ips);
    void ClearDeviceFilter();

    /// <summary>Builds an API discovery summary from the current buffer.</summary>
    PassiveCaptureSummary GetSummary();
}

public record PassiveCaptureSummary(
    int TotalRequests,
    IReadOnlyList<EndpointFrequency> TopEndpoints,
    IReadOnlyList<StatusCodeBucket> StatusCodes,
    IReadOnlyList<string> TopHosts,
    IReadOnlySet<string> ActiveDeviceFilter);

public record EndpointFrequency(string Method, string Host, string Path, int Count);
public record StatusCodeBucket(int StatusCode, int Count);
