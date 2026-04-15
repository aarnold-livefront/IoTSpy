using IoTSpy.Core.Models;
using System.Text;

namespace IoTSpy.Scanner;

/// <summary>
/// Reassembles TCP streams from captured packets and extracts application-layer
/// metadata (HTTP method/URI/status code) to enrich imported packet records.
/// </summary>
public static class TcpSessionReconstructor
{
    /// <summary>
    /// Processes a list of imported packets and enriches HTTP packets with
    /// method, URI and response code extracted from reassembled TCP payloads.
    /// Returns the number of distinct TCP sessions where HTTP was detected.
    /// </summary>
    public static int ReconstructSessions(IReadOnlyList<CapturedPacket> packets)
    {
        // Group TCP packets that have raw payload data into flows
        var flows = new Dictionary<string, List<CapturedPacket>>();

        foreach (var pkt in packets)
        {
            if (pkt.Layer4Protocol != "TCP" || pkt.RawData == null || pkt.RawData.Length == 0)
                continue;

            // Canonical flow key: lower endpoint first to merge both directions
            var key = FlowKey(pkt.SourceIp, pkt.SourcePort, pkt.DestinationIp, pkt.DestinationPort);
            if (!flows.TryGetValue(key, out var list))
            {
                list = new List<CapturedPacket>();
                flows[key] = list;
            }
            list.Add(pkt);
        }

        int httpSessions = 0;

        foreach (var (_, flowPackets) in flows)
        {
            // Only attempt HTTP reconstruction on standard HTTP ports or when
            // payload looks like HTTP
            var clientPort = flowPackets[0].SourcePort;
            var serverPort = flowPackets[0].DestinationPort;
            bool likelyHttp = serverPort is 80 or 8080 or 8000 or 3000
                           || clientPort is 80 or 8080 or 8000 or 3000;

            if (!likelyHttp)
            {
                // Peek at raw payload to detect HTTP even on non-standard ports
                var firstWithPayload = flowPackets.FirstOrDefault(p => p.RawData is { Length: > 14 });
                if (firstWithPayload != null)
                    likelyHttp = LooksLikeHttp(firstWithPayload.RawData!);
            }

            if (!likelyHttp) continue;

            bool enriched = TryEnrichHttpPackets(flowPackets);
            if (enriched) httpSessions++;
        }

        return httpSessions;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryEnrichHttpPackets(List<CapturedPacket> flowPackets)
    {
        bool any = false;

        foreach (var pkt in flowPackets)
        {
            if (pkt.RawData == null || pkt.RawData.Length < 20) continue;

            // Extract TCP payload by skipping Ethernet (14) + IP (variable) + TCP (variable) headers
            var payload = ExtractTcpPayload(pkt.RawData);
            if (payload.Length == 0) continue;

            var text = Encoding.ASCII.GetString(payload, 0, Math.Min(payload.Length, 512));

            // Request line: METHOD /path HTTP/1.x
            if (TryParseHttpRequest(text, out var method, out var uri))
            {
                pkt.Protocol = "HTTP";
                pkt.HttpMethodName = method;
                pkt.HttpRequestUri = uri;
                any = true;
            }
            // Status line: HTTP/1.x 200 OK
            else if (TryParseHttpResponse(text, out var status))
            {
                pkt.Protocol = "HTTP";
                pkt.HttpResponseCode = status;
                any = true;
            }
        }

        return any;
    }

    private static bool TryParseHttpRequest(string text, out string method, out string uri)
    {
        method = string.Empty;
        uri = string.Empty;

        var nl = text.IndexOf('\n');
        var line = nl > 0 ? text[..nl].TrimEnd('\r') : text;
        var parts = line.Split(' ', 3);
        if (parts.Length < 3) return false;

        var m = parts[0];
        if (m is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS" or "CONNECT"))
            return false;

        method = m;
        uri = parts[1];
        return true;
    }

    private static bool TryParseHttpResponse(string text, out int status)
    {
        status = 0;
        // "HTTP/1.1 200 OK\r\n..." or "HTTP/2 200\r\n..."
        if (!text.StartsWith("HTTP/", StringComparison.Ordinal)) return false;

        var nl = text.IndexOf('\n');
        var line = nl > 0 ? text[..nl].TrimEnd('\r') : text;
        var parts = line.Split(' ', 3);
        if (parts.Length < 2) return false;

        return int.TryParse(parts[1], out status) && status is >= 100 and <= 599;
    }

    private static byte[] ExtractTcpPayload(byte[] frame)
    {
        try
        {
            if (frame.Length < 14) return Array.Empty<byte>();

            // Skip Ethernet header (14 bytes)
            int offset = 14;
            if (frame.Length <= offset) return Array.Empty<byte>();

            // IP header: version/IHL in first nibble
            int ipVersion = (frame[offset] >> 4) & 0xF;
            int ipHeaderLen;

            if (ipVersion == 4)
            {
                ipHeaderLen = (frame[offset] & 0xF) * 4;
            }
            else if (ipVersion == 6)
            {
                ipHeaderLen = 40; // fixed
            }
            else
            {
                return Array.Empty<byte>();
            }

            offset += ipHeaderLen;
            if (frame.Length <= offset + 12) return Array.Empty<byte>();

            // TCP header: data offset in high nibble of byte 12
            int tcpHeaderLen = ((frame[offset + 12] >> 4) & 0xF) * 4;
            offset += tcpHeaderLen;

            if (offset >= frame.Length) return Array.Empty<byte>();
            return frame[offset..];
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static bool LooksLikeHttp(byte[] rawData)
    {
        var payload = ExtractTcpPayload(rawData);
        if (payload.Length < 4) return false;
        var start = Encoding.ASCII.GetString(payload, 0, Math.Min(payload.Length, 16));
        return start.StartsWith("GET ") || start.StartsWith("POST ")
            || start.StartsWith("PUT ") || start.StartsWith("DELETE ")
            || start.StartsWith("HTTP/");
    }

    private static string FlowKey(string srcIp, int? srcPort, string dstIp, int? dstPort)
    {
        var a = $"{srcIp}:{srcPort}";
        var b = $"{dstIp}:{dstPort}";
        // Canonical: lexicographically smaller endpoint first
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}↔{b}"
            : $"{b}↔{a}";
    }
}
