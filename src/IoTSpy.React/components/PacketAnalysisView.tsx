import React, { useState, useEffect, useMemo } from 'react';
import './PacketAnalysisView.css';

interface PacketStats {
  totalPackets: number;
  totalBytes: number;
  uniqueSources: number;
  uniqueDestinations: number;
  topProtocols: Array<{ name: string; count: number; percentage: number }>;
  byLayer3: Array<{ name: string; count: number; percentage: number }>;
  byLayer4: Array<{ name: string; count: number; percentage: number }>;
}

interface SuspiciousActivity {
  category: string;
  severity: 'Low' | 'Medium' | 'High';
  description: string;
  sourceIp?: string;
  packetCount: number;
  firstDetected: string;
  evidence: string[];
}

export function PacketAnalysisView({ 
  packets, 
  filter, 
  onFilterChange,
  isFrozen,
  setFreezeEnabled
}: { 
  packets: any[], 
  filter: PacketFilter,
  onFilterChange: (filter: PacketFilter) => void,
  isFrozen: boolean,
  setFreezeEnabled: (enabled: boolean) => void
}) {
  const [selectedPackets, setSelectedPackets] = useState<Set<number>>(new Set());
  const [showDetails, setShowDetails] = useState(false);
  const [activeTab, setActiveTab] = useState<'packets' | 'stats' | 'suspicious'>('packets');

  const filteredPackets = useMemo(() => {
    return packets.filter(packet => {
      if (filter.protocol && packet.Protocol !== filter.protocol) return false;
      if (filter.sourceIp && !packet.SourceIp?.includes(filter.sourceIp!)) return false;
      if (filter.destinationIp && !packet.DestinationIp?.includes(filter.destinationIp!)) return false;
      if (filter.showOnlyErrors && !packet.IsError) return false;
      if (filter.showOnlyRetransmissions && !packet.IsRetransmission) return false;
      if (filter.fromTime && new Date(packet.Timestamp) < filter.fromTime!) return false;
      if (filter.toTime && new Date(packet.Timestamp) > filter.toTime!) return false;
      return true;
    });
  }, [packets, filter]);

  const stats: PacketStats = useMemo(() => {
    const protocolCounts: Record<string, number> = {};
    const layer3Counts: Record<string, number> = {};
    const layer4Counts: Record<string, number> = {};
    const uniqueSources = new Set<string>();
    const uniqueDestinations = new Set<string>();

    let totalBytes = 0;

    filteredPackets.forEach(packet => {
      if (packet.Protocol) protocolCounts[packet.Protocol] = (protocolCounts[packet.Protocol] || 0) + 1;
      if (packet.Layer3Protocol) layer3Counts[packet.Layer3Protocol] = (layer3Counts[packet.Layer3Protocol] || 0) + 1;
      if (packet.Layer4Protocol) layer4Counts[packet.Layer4Protocol] = (layer4Counts[packet.Layer4Protocol] || 0) + 1;
      
      if (packet.SourceIp) uniqueSources.add(packet.SourceIp);
      if (packet.DestinationIp) uniqueDestinations.add(packet.DestinationIp);
      totalBytes += packet.Length ?? 0;
    });

    const total = filteredPackets.length || 1;

    return {
      totalPackets: filteredPackets.length,
      totalBytes,
      uniqueSources: uniqueSources.size,
      uniqueDestinations: uniqueDestinations.size,
      topProtocols: Object.entries(protocolCounts)
        .map(([name, count]) => ({ name, count, percentage: (count / total) * 100 }))
        .sort((a, b) => b.count - a.count),
      byLayer3: Object.entries(layer3Counts)
        .map(([name, count]) => ({ name, count, percentage: (count / total) * 100 }))
        .sort((a, b) => b.count - a.count),
      byLayer4: Object.entries(layer4Counts)
        .map(([name, count]) => ({ name, count, percentage: (count / total) * 100 }))
        .sort((a, b) => b.count - a.count)
    };
  }, [filteredPackets]);

  const togglePacketSelection = (frameNumber: number) => {
    setSelectedPackets(prev => {
      const next = new Set(prev);
      if (next.has(frameNumber)) next.delete(frameNumber);
      else next.add(frameNumber);
      return next;
    });
  };

  const clearSelection = () => setSelectedPackets(new Set());

  return (
    <div className="packet-analysis-view">
      <div className="analysis-header">
        <h3>Packet Analysis</h3>
        
        <div className="freeze-frame-control">
          <button 
            onClick={() => setFreezeEnabled(!isFrozen)}
            className={isFrozen ? 'freeze active' : 'freeze'}
            title={isFrozen ? 'Unfreeze (live mode)' : 'Freeze current view for inspection'}
          >
            {isFrozen ? '❄️ Unfreeze' : '⏸️ Freeze Frame'}
          </button>
        </div>

        <div className="filter-controls">
          <input
            type="text"
            placeholder="Protocol (TCP, UDP, etc.)"
            value={filter.protocol || ''}
            onChange={(e) => onFilterChange({ ...filter, protocol: e.target.value })}
            className="filter-input"
          />
          <input
            type="text"
            placeholder="Source IP"
            value={filter.sourceIp || ''}
            onChange={(e) => onFilterChange({ ...filter, sourceIp: e.target.value })}
            className="filter-input"
          />
          <input
            type="text"
            placeholder="Destination IP"
            value={filter.destinationIp || ''}
            onChange={(e) => onFilterChange({ ...filter, destinationIp: e.target.value })}
            className="filter-input"
          />
          <label>
            <input
              type="checkbox"
              checked={filter.showOnlyErrors}
              onChange={(e) => onFilterChange({ ...filter, showOnlyErrors: e.target.checked })}
            />
            Errors only
          </label>
          <label>
            <input
              type="checkbox"
              checked={filter.showOnlyRetransmissions}
              onChange={(e) => onFilterChange({ ...filter, showOnlyRetransmissions: e.target.checked })}
            />
            Retransmissions only
          </label>
        </div>

        <div className="stats-summary">
          <span>{stats.totalPackets.toLocaleString()} packets</span>
          <span>{(stats.totalBytes / 1024 / 1024).toFixed(2)} MB</span>
          <span>{stats.uniqueSources} sources</span>
          <span>{stats.uniqueDestinations} destinations</span>
        </div>

        <button 
          onClick={() => setActiveTab('packets')}
          className={activeTab === 'packets' ? 'tab active' : 'tab'}
        >
          Packets
        </button>
        <button 
          onClick={() => setActiveTab('stats')}
          className={activeTab === 'stats' ? 'tab active' : 'tab'}
        >
          Statistics
        </button>
        <button 
          onClick={() => setActiveTab('suspicious')}
          className={activeTab === 'suspicious' ? 'tab active' : 'tab'}
        >
          Suspicious Activity
        </button>
      </div>

      {activeTab === 'packets' && (
        <PacketTable 
          packets={filteredPackets} 
          selectedFrames={selectedPackets}
          onToggleSelection={togglePacketSelection}
          onSelectAll={() => setSelectedPackets(new Set(filteredPackets.map(p => p.FrameNumber)))}
          clearSelection={clearSelection}
          setShowDetails={setShowDetails}
        />
      )}

      {activeTab === 'stats' && (
        <StatisticsView stats={stats} />
      )}

      {activeTab === 'suspicious' && (
        <SuspiciousActivityView packets={filteredPackets} />
      )}

      {showDetails && selectedPackets.size > 0 && (
        <PacketDetailPanel 
          packets={packets.filter(p => selectedPackets.has(p.FrameNumber))}
          onClose={() => setShowDetails(false)}
        />
      )}
    </div>
  );
}

interface PacketTableProps {
  packets: any[];
  selectedFrames: Set<number>;
  onToggleSelection: (frameNumber: number) => void;
  onSelectAll: () => void;
  clearSelection: () => void;
  setShowDetails: (show: boolean) => void;
}

function PacketTable({ packets, selectedFrames, onToggleSelection, onSelectAll, clearSelection, setShowDetails }: PacketTableProps) {
  if (packets.length === 0) return <p className="no-packets">No packets match the current filter</p>;

  const packetIds = Array.from(packets); // Keep references for detail view

  return (
    <div className="packet-table-container">
      <div className="table-controls">
        <button onClick={onSelectAll}>Select All ({packets.length})</button>
        <button onClick={clearSelection} disabled={selectedFrames.size === 0}>Clear Selection</button>
        <button onClick={() => setShowDetails(true)} disabled={selectedFrames.size === 0}>
          Inspect Selected ({selectedFrames.size})
        </button>
      </div>

      <table className="packet-table">
        <thead>
          <tr>
            <th><input type="checkbox" onChange={(e) => e.target.checked ? onSelectAll() : clearSelection()} /></th>
            <th>#</th>
            <th>Time</th>
            <th>Protocol</th>
            <th>Source</th>
            <th>Destination</th>
            <th>Length</th>
          </tr>
        </thead>
        <tbody>
          {packets.map((packet, idx) => (
            <tr 
              key={idx} 
              className={selectedFrames.has(packet.FrameNumber) ? 'selected' : ''}
              onClick={() => onToggleSelection(packet.FrameNumber)}
            >
              <td>
                <input 
                  type="checkbox" 
                  checked={selectedFrames.has(packet.FrameNumber)}
                  onChange={() => {}} // Handled by parent click
                />
              </td>
              <td>{packet.FrameNumber}</td>
              <td className="timestamp">{new Date(packet.Timestamp).toLocaleTimeString()}</td>
              <td><span className={`protocol-badge ${packet.Protocol?.toLowerCase()}`}>{packet.Protocol || 'Unknown'}</span></td>
              <td className="ip-cell">{packet.SourceIp || '-'}</td>
              <td className="ip-cell">{packet.DestinationIp || '-'}</td>
              <td>{packet.Length ?? 0}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface StatisticsViewProps {
  stats: PacketStats;
}

function StatisticsView({ stats }: StatisticsViewProps) {
  return (
    <div className="statistics-view">
      <h4>Protocol Distribution</h4>
      <ul>
        {stats.topProtocols.map(p => (
          <li key={p.name}>
            {p.name}: {p.count} ({p.percentage.toFixed(1)}%)
            <div className="progress-bar">
              <div style={{ width: `${p.percentage}%` }}></div>
            </div>
          </li>
        ))}
      </ul>

      <h4>Layer 3 (Network) Protocols</h4>
      <ul>
        {stats.byLayer3.map(p => (
          <li key={p.name}>
            {p.name}: {p.count} ({p.percentage.toFixed(1)}%)
          </li>
        ))}
      </ul>

      <h4>Layer 4 (Transport) Protocols</h4>
      <ul>
        {stats.byLayer4.map(p => (
          <li key={p.name}>
            {p.name}: {p.count} ({p.percentage.toFixed(1)}%)
          </li>
        ))}
      </ul>
    </div>
  );
}

interface SuspiciousActivityViewProps {
  packets: any[];
}

function SuspiciousActivityView({ packets }: SuspiciousActivityViewProps) {
  const [activities, setActivities] = useState<SuspiciousActivity[]>([]);

  useEffect(() => {
    // Analyze for suspicious activity
    const detected: SuspiciousActivity[] = [];
    
    // Port scanning detection
    const portAccessMap = new Map<string, Set<number>>();
    packets.forEach(packet => {
      if (packet.SourceIp && packet.DestinationPort) {
        if (!portAccessMap.has(packet.SourceIp)) portAccessMap.set(packet.SourceIp, new Set());
        portAccessMap.get(packet.SourceIp)!.add(packet.DestinationPort);
      }
    });

    portAccessMap.forEach((ports, ip) => {
      if (ports.size > 50) {
        detected.push({
          category: 'PortScan',
          severity: 'Medium',
          description: `Potential port scanning from ${ip}: accessed ${ports.size} unique ports`,
          sourceIp: ip,
          packetCount: packets.length,
          firstDetected: new Date().toISOString(),
          evidence: [`Unique ports: ${ports.size}`]
        });
      }
    });

    // High retransmission rate detection
    const flowRetransmits = new Map<string, number>();
    const flowTotal = new Map<string, number>();
    
    packets.forEach(packet => {
      if (packet.SourceIp) {
        const key = `${packet.SourceIp}:${packet.SourcePort}-${packet.DestinationIp}:${packet.DestinationPort}`;
        flowTotal.set(key, (flowTotal.get(key) || 0) + 1);
        if (packet.IsRetransmission) {
          flowRetransmits.set(key, (flowRetransmits.get(key) || 0) + 1);
        }
      }
    });

    flowRetransmits.forEach((count, key) => {
      const total = flowTotal.get(key) || 1;
      if ((count / total) > 0.1 && count > 5) {
        detected.push({
          category: 'HighRetransmission',
          severity: 'Low',
          description: `Flow ${key} has high TCP retransmission rate`,
          packetCount: count,
          firstDetected: new Date().toISOString(),
          evidence: [`${count} retransmissions (${((count/total)*100).toFixed(1)}%)`]
        });
      }
    });

    setActivities(detected);
  }, [packets]);

  if (activities.length === 0) return <p className="no-suspicious">No suspicious activity detected</p>;

  return (
    <div className="suspicious-activity-view">
      {activities.map((activity, idx) => (
        <div key={idx} className={`alert alert-${activity.severity.toLowerCase()}`}>
          <strong>{activity.category}</strong> ({activity.severity})
          <p>{activity.description}</p>
          {activity.sourceIp && <p><strong>Source IP:</strong> {activity.sourceIp}</p>}
          <ul className="evidence-list">
            {activity.evidence.map((ev, i) => (
              <li key={i}>{ev}</li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

interface PacketDetailPanelProps {
  packets: any[];
  onClose: () => void;
}

function PacketDetailPanel({ packets, onClose }: PacketDetailPanelProps) {
  const [selectedPacket, setSelectedPacket] = useState<any>(packets[0]);

  return (
    <div className="detail-panel-overlay">
      <div className="detail-panel">
        <div className="panel-header">
          <h3>Packet Detail Inspection</h3>
          <button onClick={onClose}>&times;</button>
        </div>

        <select 
          value={selectedPacket?.FrameNumber || ''}
          onChange={(e) => setSelectedPacket(packets.find(p => p.FrameNumber === parseInt(e.target.value)))}
          className="packet-selector"
        >
          {packets.map(p => (
            <option key={p.FrameNumber} value={p.FrameNumber}>
              Frame #{p.FrameNumber} - {p.Protocol} - {p.SourceIp} → {p.DestinationIp}
            </option>
          ))}
        </select>

        {selectedPacket && (
          <div className="packet-details">
            <h4>Frame #{selectedPacket.FrameNumber}</h4>
            
            <dl>
              <dt>Timestamp</dt>
              <dd>{new Date(selectedPacket.Timestamp).toLocaleString()}</dd>

              <dt>Protocol</dt>
              <dd>{selectedPacket.Protocol || 'Unknown'}</dd>

              <dt>Layer 2 Protocol</dt>
              <dd>{selectedPacket.Layer2Protocol || 'Ethernet'}</dd>

              <dt>Layer 3 Protocol</dt>
              <dd>{selectedPacket.Layer3Protocol || 'IPv4'}</dd>

              <dt>Layer 4 Protocol</dt>
              <dd>{selectedPacket.Layer4Protocol || 'TCP/UDP'}</dd>

              {selectedPacket.SourcePort && (
                <>
                  <dt>Source Port</dt>
                  <dd>{selectedPacket.SourcePort}</dd>
                </>
              )}

              {selectedPacket.DestinationPort && (
                <>
                  <dt>Destination Port</dt>
                  <dd>{selectedPacket.DestinationPort}</dd>
                </>
              )}

              <dt>Source MAC</dt>
              <dd>{selectedPacket.SourceMac || 'N/A'}</dd>

              <dt>Destination MAC</dt>
              <dd>{selectedPacket.DestinationMac || 'N/A'}</dd>

              <dt>Length</dt>
              <dd>{selectedPacket.Length} bytes</dd>

              {selectedPacket.TcpFlags && (
                <>
                  <dt>TCP Flags</dt>
                  <dd>{selectedPacket.TcpFlags}</dd>
                </>
              )}

              {selectedPacket.DnsQueryName && (
                <>
                  <dt>DNS Query</dt>
                  <dd>{selectedPacket.DnsQueryName}</dd>
                </>
              )}

              <dt>Error</dt>
              <dd className={selectedPacket.IsError ? 'error' : ''}>{selectedPacket.IsError ? 'Yes' : 'No'}</dd>

              <dt>Retransmission</dt>
              <dd>{selectedPacket.IsRetransmission ? 'Yes' : 'No'}</dd>

              <dt>Fragmented</dt>
              <dd>{selectedPacket.IsFragment ? 'Yes' : 'No'}</dd>
            </dl>
          </div>
        )}
      </div>
    </div>
  );
}

export interface PacketFilter {
  protocol?: string;
  sourceIp?: string;
  destinationIp?: string;
  showOnlyErrors: boolean;
  showOnlyRetransmissions: boolean;
  fromTime?: Date;
  toTime?: Date;
}
