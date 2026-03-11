using System.Text.RegularExpressions;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Manipulation.Analysis;

/// <summary>
/// Analyzer for captured packets with filtering, freeze frame support, and protocol analysis.
/// </summary>
public class PacketCaptureAnalyzer : IPacketCaptureAnalyzer
{
    private readonly List<CapturedPacket> _allPackets = new();
    private readonly List<CapturedPacket> _filteredPackets = new();
    private readonly object _lockObj = new();
    private bool _isFrozen;
    private PacketFilter _currentFilter = new();

    public bool IsFrozen => _isFrozen;
    public int FilteredPacketCount => _filteredPackets.Count;

    public void ApplyFilters(PacketFilter filter)
    {
        lock (_lockObj)
        {
            _currentFilter = filter ?? new PacketFilter();
            RecalculateFilteredSet();
        }
    }

    public void FreezeFrame()
    {
        lock (_lockObj)
        {
            if (!_isFrozen)
            {
                _isFrozen = true;
                // Snapshot current filtered set
                _filteredPackets.Clear();
                _filteredPackets.AddRange(CopyFilteredPackets());
            }
        }
    }

    public void UnfreezeFrame()
    {
        lock (_lockObj)
        {
            if (_isFrozen)
            {
                _isFrozen = false;
                RecalculateFilteredSet();
            }
        }
    }

    public IEnumerable<CapturedPacket> GetFilteredPackets()
    {
        lock (_lockObj)
        {
            return _isFrozen 
                ? _filteredPackets.AsEnumerable() 
                : CalculateFilteredPackets().AsEnumerable();
        }
    }

    private void RecalculateFilteredSet()
    {
        if (!_isFrozen)
        {
            lock (_lockObj)
            {
                _filteredPackets.Clear();
                foreach (var packet in CalculateFilteredPackets())
                {
                    _filteredPackets.Add(packet);
                }
            }
        }
    }

    private List<CapturedPacket> CalculateFilteredPackets()
    {
        var result = new List<CapturedPacket>();

        foreach (var packet in _allPackets)
        {
            if (MatchesFilter(packet, _currentFilter))
            {
                result.Add(packet);
            }
        }

        return result;
    }

    private bool MatchesFilter(CapturedPacket packet, PacketFilter filter)
    {
        // Protocol filter
        if (!string.IsNullOrEmpty(filter.Protocol))
        {
            if (packet.Protocol.IndexOf(filter.Protocol!, StringComparison.OrdinalIgnoreCase) == -1 &&
                packet.Layer4Protocol.IndexOf(filter.Protocol!, StringComparison.OrdinalIgnoreCase) == -1)
            {
                return false;
            }
        }

        // Layer 3 protocol filter
        if (!string.IsNullOrEmpty(filter.Protocol))
        {
            var lowerFilter = filter.Protocol!.ToLower();
            bool matchesLayer3Or4 = packet.Layer3Protocol.ToLower().Contains(lowerFilter) ||
                                   packet.Layer4Protocol.ToLower().Contains(lowerFilter);
            
            if (!matchesLayer3Or4 && 
                (packet.TcpFlags != null || packet.DnsQueryName != null || packet.HttpMethodName != null))
            {
                // Check protocol-specific fields
                if (lowerFilter == "tcp" && !string.IsNullOrEmpty(packet.TcpFlags)) matchesLayer3Or4 = true;
                else if (lowerFilter == "udp") matchesLayer3Or4 = true;
                else if (lowerFilter == "arp" && packet.ArpOperation != null) matchesLayer3Or4 = true;
                else if (lowerFilter == "dns" && !string.IsNullOrEmpty(packet.DnsQueryName)) matchesLayer3Or4 = true;
                else if ((lowerFilter == "http" || lowerFilter == "https") && 
                        (!string.IsNullOrEmpty(packet.HttpMethodName) || packet.HttpResponseCode.HasValue))
                    matchesLayer3Or4 = true;

                if (!matchesLayer3Or4) return false;
            }
        }

        // Source IP filter (supports CIDR notation)
        if (!string.IsNullOrEmpty(filter.SourceIp))
        {
            if (!MatchesIpAddress(packet.SourceIp, filter.SourceIp!))
            {
                return false;
            }
        }

        // Destination IP filter
        if (!string.IsNullOrEmpty(filter.DestinationIp))
        {
            if (!MatchesIpAddress(packet.DestinationIp, filter.DestinationIp!))
            {
                return false;
            }
        }

        // Port filters
        if (filter.SourcePort.HasValue && packet.SourcePort != filter.SourcePort)
        {
            return false;
        }

        if (filter.DestinationPort.HasValue && packet.DestinationPort != filter.DestinationPort)
        {
            return false;
        }

        // MAC address filter
        if (!string.IsNullOrEmpty(filter.MacAddress))
        {
            var normalizedFilter = NormalizeMacAddress(filter.MacAddress!);
            if (packet.SourceMac != normalizedFilter && packet.DestinationMac != normalizedFilter)
            {
                return false;
            }
        }

        // Error packets only filter
        if (filter.ShowOnlyErrors && !packet.IsError)
        {
            return false;
        }

        // Retransmissions only filter
        if (filter.ShowOnlyRetransmissions && !packet.IsRetransmission)
        {
            return false;
        }

        // Time range filter
        if (filter.FromTime.HasValue && packet.Timestamp < filter.FromTime.Value)
        {
            return false;
        }

        if (filter.ToTime.HasValue && packet.Timestamp > filter.ToTime.Value)
        {
            return false;
        }

        // Payload search
        if (!string.IsNullOrEmpty(filter.PayloadSearch))
        {
            var searchLower = filter.PayloadSearch!.ToLower();
            if (packet.PayloadPreview.ToLower().IndexOf(searchLower) == -1 &&
                packet.Protocol.ToLower().IndexOf(searchLower) == -1)
            {
                return false;
            }
        }

        return true;
    }

    private bool MatchesIpAddress(string packetIp, string filterIp)
    {
        // Check for CIDR notation
        if (filterIp.Contains('/'))
        {
            var parts = filterIp.Split('/');
            if (parts.Length != 2) return false;

            if (!System.Net.IPAddress.TryParse(parts[0], out var networkAddr)) return false;
            if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32) return false;

            return IsInSubnet(packetIp, networkAddr, prefixLength);
        }

        // Exact match or wildcard
        if (filterIp.EndsWith(".*"))
        {
            var prefix = filterIp.Substring(0, filterIp.Length - 2);
            return packetIp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(packetIp, filterIp, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInSubnet(string ipAddr, System.Net.IPAddress network, int prefixLength)
    {
        if (!System.Net.IPAddress.TryParse(ipAddr, out var ip)) return false;

        var ipBytes = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();

        for (int i = 0; i < 4; i++)
        {
            int bitsInOctet = Math.Min(8, prefixLength);
            int mask = 0xFF << (8 - bitsInOctet) & 0xFF;

            if ((ipBytes[i] & mask) != (netBytes[i] & mask))
            {
                return false;
            }

            prefixLength -= bitsInOctet;
            if (prefixLength <= 0) break;
        }

        return true;
    }

    private string NormalizeMacAddress(string macAddr)
    {
        var clean = Regex.Replace(macAddr, @"[^0-9A-Fa-f]", "");
        return clean.ToUpperInvariant();
    }

    private List<CapturedPacket> CopyFilteredPackets()
    {
        var snapshot = new List<CapturedPacket>();
        foreach (var packet in _filteredPackets)
        {
            snapshot.Add(new CapturedPacket
            {
                Id = packet.Id,
                CaptureIndex = packet.CaptureIndex,
                Timestamp = packet.Timestamp,
                TimeDeltaFromPrevious = packet.TimeDeltaFromPrevious,
                DeviceId = packet.DeviceId,
                Protocol = packet.Protocol,
                Layer2Protocol = packet.Layer2Protocol,
                Layer3Protocol = packet.Layer3Protocol,
                Layer4Protocol = packet.Layer4Protocol,
                SourceIp = packet.SourceIp,
                DestinationIp = packet.DestinationIp,
                SourcePort = packet.SourcePort,
                DestinationPort = packet.DestinationPort,
                SourceMac = packet.SourceMac,
                DestinationMac = packet.DestinationMac,
                Length = packet.Length,
                FrameNumber = packet.FrameNumber,
                IsError = packet.IsError,
                IsRetransmission = packet.IsRetransmission,
                IsFragment = packet.IsFragment,
                TcpFlags = packet.TcpFlags,
                UdpLength = packet.UdpLength,
                ArpOperation = packet.ArpOperation,
                ArpSenderMac = packet.ArpSenderMac,
                ArpTargetIp = packet.ArpTargetIp,
                DnsQueryName = packet.DnsQueryName,
                DnsResponseType = packet.DnsResponseType,
                HttpMethodName = packet.HttpMethodName,
                HttpRequestUri = packet.HttpRequestUri,
                HttpResponseCode = packet.HttpResponseCode,
                PayloadPreview = packet.PayloadPreview,
                IsSuspicious = packet.IsSuspicious,
                SuspicionReason = packet.SuspicionReason
            });
        }

        return snapshot;
    }

    public void AddPacket(CapturedPacket packet)
    {
        lock (_lockObj)
        {
            _allPackets.Add(packet);
            
            if (!_isFrozen && MatchesFilter(packet, _currentFilter))
            {
                _filteredPackets.Add(packet);
            }
        }
    }

    public void ClearAll()
    {
        lock (_lockObj)
        {
            _allPackets.Clear();
            _filteredPackets.Clear();
            _isFrozen = false;
        }
    }

    public async Task<ProtocolDistribution?> AnalyzeProtocolsAsync(CancellationToken ct = default)
    {
        await Task.Yield();

        lock (_lockObj)
        {
            var packets = IsFrozen ? _filteredPackets : _allPackets;
            
            var distribution = new ProtocolDistribution
            {
                TotalPackets = packets.Count
            };

            var protocolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var layer3Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var layer4Counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var packet in packets)
            {
                // Count main protocol
                if (!protocolCounts.ContainsKey(packet.Protocol))
                    protocolCounts[packet.Protocol] = 0;
                protocolCounts[packet.Protocol]++;

                // Count Layer 3 protocols
                if (!string.IsNullOrEmpty(packet.Layer3Protocol) && 
                    !layer3Counts.ContainsKey(packet.Layer3Protocol))
                    layer3Counts[packet.Layer3Protocol] = 0;
                if (!string.IsNullOrEmpty(packet.Layer3Protocol))
                    layer3Counts[packet.Layer3Protocol]++;

                // Count Layer 4 protocols
                if (!string.IsNullOrEmpty(packet.Layer4Protocol) && 
                    !layer4Counts.ContainsKey(packet.Layer4Protocol))
                    layer4Counts[packet.Layer4Protocol] = 0;
                layer4Counts[packet.Layer4Protocol]++;
            }

            distribution.ByProtocol = protocolCounts.Select(kvp => new ProtocolStats
            {
                Name = kvp.Key,
                Count = kvp.Value,
                Percentage = distribution.TotalPackets > 0 
                    ? (kvp.Value * 100.0 / distribution.TotalPackets) 
                    : 0
            }).OrderByDescending(x => x.Count).ToList();

            distribution.ByLayer3 = layer3Counts.Select(kvp => new ProtocolStats
            {
                Name = kvp.Key,
                Count = kvp.Value,
                Percentage = distribution.TotalPackets > 0 
                    ? (kvp.Value * 100.0 / distribution.TotalPackets) 
                    : 0
            }).OrderByDescending(x => x.Count).ToList();

            distribution.ByLayer4 = layer4Counts.Select(kvp => new ProtocolStats
            {
                Name = kvp.Key,
                Count = kvp.Value,
                Percentage = distribution.TotalPackets > 0 
                    ? (kvp.Value * 100.0 / distribution.TotalPackets) 
                    : 0
            }).OrderByDescending(x => x.Count).ToList();

            return distribution;
        }
    }

    public async Task<List<CommunicationPattern>> FindCommunicationPatternsAsync(int topN = 10, CancellationToken ct = default)
    {
        await Task.Yield();

        lock (_lockObj)
        {
            var packets = IsFrozen ? _filteredPackets : _allPackets;
            
            var patternMap = new Dictionary<(string src, string dst), CommunicationPattern>();

            foreach (var packet in packets)
            {
                if (string.IsNullOrEmpty(packet.SourceIp) || string.IsNullOrEmpty(packet.DestinationIp))
                    continue;

                var key = (packet.SourceIp, packet.DestinationIp);

                if (!patternMap.ContainsKey(key))
                {
                    patternMap[key] = new CommunicationPattern
                    {
                        SourceIp = packet.SourceIp,
                        DestinationIp = packet.DestinationIp
                    };
                }

                var pattern = patternMap[key];
                pattern.PacketCount++;
                pattern.TotalBytes += packet.Length;

                if (!pattern.ProtocolsUsed.Contains(packet.Protocol))
                    pattern.ProtocolsUsed.Add(packet.Protocol);

                if (pattern.FirstSeen == null || packet.Timestamp < pattern.FirstSeen)
                    pattern.FirstSeen = packet.Timestamp;

                if (pattern.LastSeen == null || packet.Timestamp > pattern.LastSeen)
                    pattern.LastSeen = packet.Timestamp;
            }

            return patternMap.Values
                .OrderByDescending(x => x.PacketCount)
                .Take(topN)
                .ToList();
        }
    }

    public async Task<List<SuspiciousActivity>> DetectSuspiciousActivityAsync(CancellationToken ct = default)
    {
        await Task.Yield();

        var suspiciousActivities = new List<SuspiciousActivity>();
        
        lock (_lockObj)
        {
            var packets = IsFrozen ? _filteredPackets : _allPackets;
            
            // Detect port scanning (many ports from same source in short time)
            var portScanDetection = DetectPortScanning(packets);
            suspiciousActivities.AddRange(portScanDetection);

            // Detect ARP spoofing attempts
            var arpSpoofingDetection = DetectArpSpoofing(packets);
            suspiciousActivities.AddRange(arpSpoofingDetection);

            // Detect DNS anomalies (unusual query patterns)
            var dnsAnomalies = DetectDnsAnomalies(packets);
            suspiciousActivities.AddRange(dnsAnomalies);

            // Detect unusual retransmission rates
            var retransmissionAnalysis = DetectHighRetransmissions(packets);
            suspiciousActivities.AddRange(retransmissionAnalysis);
        }

        return suspiciousActivities;
    }

    private List<SuspiciousActivity> DetectPortScanning(List<CapturedPacket> packets)
    {
        var activities = new List<SuspiciousActivity>();
        
        // Group by source IP and count unique destination ports
        var portAccessMap = new Dictionary<string, HashSet<int>>();
        var packetCounts = new Dictionary<string, int>();

        foreach (var packet in packets)
        {
            if (string.IsNullOrEmpty(packet.SourceIp)) continue;
            
            if (!portAccessMap.ContainsKey(packet.SourceIp!))
                portAccessMap[packet.SourceIp!] = new HashSet<int>();
            
            if (packet.DestinationPort.HasValue)
                portAccessMap[packet.SourceIp!].Add(packet.DestinationPort.Value);

            if (!packetCounts.ContainsKey(packet.SourceIp!))
                packetCounts[packet.SourceIp!] = 0;
            packetCounts[packet.SourceIp!]++;
        }

        // Flag sources that accessed many unique ports (potential scan)
        foreach (var kvp in portAccessMap)
        {
            if (kvp.Value.Count > 50 && packetCounts[kvp.Key] > 100)
            {
                activities.Add(new SuspiciousActivity
                {
                    Category = "PortScan",
                    Severity = "Medium",
                    Description = $"Potential port scanning detected: {kvp.Value.Count} unique ports accessed from {kvp.Key}",
                    SourceIp = kvp.Key,
                    PacketCount = packetCounts[kvp.Key],
                    FirstDetected = DateTimeOffset.UtcNow,
                    Evidence = new List<string> 
                    { 
                        $"{kvp.Value.Count} unique destination ports",
                        $"{packetCounts[kvp.Key]} total packets"
                    }
                });
            }
        }

        return activities;
    }

    private List<SuspiciousActivity> DetectArpSpoofing(List<CapturedPacket> packets)
    {
        var activities = new List<SuspiciousActivity>();
        
        // Track IP-to-MAC mappings
        var ipToMacMap = new Dictionary<string, HashSet<string>>();
        var macChanges = new Dictionary<string, int>();

        foreach (var packet in packets)
        {
            if (!string.IsNullOrEmpty(packet.ArpOperation) && !string.IsNullOrEmpty(packet.ArpTargetIp))
            {
                var senderMac = packet.SourceMac.ToUpperInvariant();

                if (ipToMacMap.ContainsKey(packet.ArpTargetIp!))
                    ipToMacMap[packet.ArpTargetIp].Add(senderMac);
                else
                {
                    ipToMacMap[packet.ArpTargetIp!] = new HashSet<string> { senderMac };
                    
                    if (!macChanges.ContainsKey(packet.ArpTargetIp!))
                        macChanges[packet.ArpTargetIp!] = 0;
                    macChanges[packet.ArpTargetIp!]++;
                }
            }
        }

        // Flag IPs with multiple MAC addresses (potential ARP spoofing)
        foreach (var kvp in ipToMacMap)
        {
            if (kvp.Value.Count > 1)
            {
                activities.Add(new SuspiciousActivity
                {
                    Category = "ArpSpoofing",
                    Severity = "High",
                    Description = $"Multiple MAC addresses detected for IP {kvp.Key} - possible ARP spoofing",
                    SourceIp = kvp.Key,
                    PacketCount = kvp.Value.Count,
                    FirstDetected = DateTimeOffset.UtcNow,
                    Evidence = kvp.Value.Select(mac => $"MAC: {mac}").ToList()
                });
            }
        }

        return activities;
    }

    private List<SuspiciousActivity> DetectDnsAnomalies(List<CapturedPacket> packets)
    {
        var activities = new List<SuspiciousActivity>();
        
        // Track DNS query patterns
        var domainCounts = new Dictionary<string, int>();
        var sourceQueries = new Dictionary<string, HashSet<string>>();

        foreach (var packet in packets)
        {
            if (!string.IsNullOrEmpty(packet.DnsQueryName))
            {
                var domain = packet.DnsQueryName.ToLower();
                
                if (!domainCounts.ContainsKey(domain))
                    domainCounts[domain] = 0;
                domainCounts[domain]++;

                if (packet.SourceIp != null)
                {
                    if (!sourceQueries.ContainsKey(packet.SourceIp!))
                        sourceQueries[packet.SourceIp!] = new HashSet<string>();
                    sourceQueries[packet.SourceIp!].Add(domain);
                }
            }
        }

// Flag excessive queries to same domain (potential DGA or exfiltration)
        foreach (var kvp in domainCounts)
        {
            if (kvp.Value > 100 && kvp.Key.Length > 30)
            {
                activities.Add(new SuspiciousActivity
                {
                    Category = "DnsAnomaly",
                    Severity = "Medium",
                    Description = $"Unusual DNS query pattern: long domain queried {kvp.Value} times",
                    SourceIp = string.Empty,
                    PacketCount = kvp.Value,
                    FirstDetected = DateTimeOffset.UtcNow,
                    Evidence = new List<string> 
                    { 
                        $"Domain: {kvp.Key}",
                        $"Query count: {kvp.Value}"
                    }
                });
            }
        }

        // Flag hosts with excessive unique DNS queries (potential DGA)
        foreach (var kvp in sourceQueries)
        {
            if (kvp.Value.Count > 50)
            {
                activities.Add(new SuspiciousActivity
                {
                    Category = "DnsAnomaly",
                    Severity = "Low",
                    Description = $"Host making many unique DNS queries: possible DGA activity",
                    SourceIp = kvp.Key,
                    PacketCount = kvp.Value.Count,
                    FirstDetected = DateTimeOffset.UtcNow,
                    Evidence = new List<string> 
                    { 
                        $"{kvp.Value.Count} unique domains queried"
                    }
                });
            }
        }

        return activities;
    }

    private List<SuspiciousActivity> DetectHighRetransmissions(List<CapturedPacket> packets)
    {
        var activities = new List<SuspiciousActivity>();
        
        // Group by TCP flow (srcIp:port -> dstIp:port)
        var flowRetransmits = new Dictionary<string, int>();

        foreach (var packet in packets)
        {
            if (!packet.IsRetransmission || string.IsNullOrEmpty(packet.SourceIp)) continue;
            
            var key = $"{packet.SourceIp}:{packet.SourcePort}->{packet.DestinationIp}:{packet.DestinationPort}";
            
            if (!flowRetransmits.ContainsKey(key))
                flowRetransmits[key] = 0;
            flowRetransmits[key]++;
        }

        // Flag flows with excessive retransmissions (>10%)
        var flowTotal = new Dictionary<string, int>();
        foreach (var packet in packets)
        {
            if (string.IsNullOrEmpty(packet.SourceIp)) continue;
            
            var key = $"{packet.SourceIp}:{packet.SourcePort}->{packet.DestinationIp}:{packet.DestinationPort}";
            
            if (!flowTotal.ContainsKey(key))
                flowTotal[key] = 0;
            flowTotal[key]++;
        }

        foreach (var kvp in flowRetransmits)
        {
            var total = flowTotal.GetValueOrDefault(kvp.Key, 1);
            var rate = (double)kvp.Value / total;

            if (rate > 0.1 && kvp.Value > 5)
            {
                activities.Add(new SuspiciousActivity
                {
                    Category = "HighRetransmission",
                    Severity = "Low",
                    Description = $"Flow with high TCP retransmission rate: {kvp.Key}",
                    SourceIp = kvp.Key.Split(':')[0],
                    PacketCount = kvp.Value,
                    FirstDetected = DateTimeOffset.UtcNow,
                    Evidence = new List<string> 
                    { 
                        $"{kvp.Value} retransmissions",
                        $"Rate: {(rate * 100):F1}%"
                    }
                });
            }
        }

        return activities;
    }
}
