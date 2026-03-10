import { useState, useMemo } from 'react'
import type { CapturedPacket } from '../../types/api'

interface Props {
  packets: CapturedPacket[]
  isCapturing: boolean
  selectedPacket: any
  onSelect: (packet: any) => void
  freezeFrame: boolean
}

export default function PacketListFilterable({ packets, isCapturing, selectedPacket, onSelect, freezeFrame }: Props) {
  const [filterProtocol, setFilterProtocol] = useState<string>('all')
  const [filterSourceIp, setFilterSourceIp] = useState<string>('')
  const [filterDestIp, setFilterDestIp] = useState<string>('')

  const filteredPackets = useMemo(() => {
    return packets.filter(packet => {
      if (filterProtocol !== 'all' && packet.protocol.toLowerCase() !== filterProtocol.toLowerCase()) {
        return false
      }
      if (filterSourceIp && !packet.sourceIp.includes(filterSourceIp)) {
        return false
      }
      if (filterDestIp && !packet.destinationIp.includes(filterDestIp)) {
        return false
      }
      return true
    })
  }, [packets, filterProtocol, filterSourceIp, filterDestIp])

  const protocols = useMemo(() => ['all', ...new Set(packets.map(p => p.protocol))], [packets])

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
      {/* Filter controls */}
      <div style={{ padding: '8px 12px', background: 'var(--color-surface)', borderBottom: '1px solid var(--color-border)' }}>
        <div style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
          <select
            value={filterProtocol}
            onChange={(e) => setFilterProtocol(e.target.value)}
            disabled={freezeFrame}
            style={{ padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          >
            {protocols.map(p => (
              <option key={p} value={p}>{p.toUpperCase()}</option>
            ))}
          </select>

          <input
            type="text"
            placeholder="Source IP..."
            value={filterSourceIp}
            onChange={(e) => setFilterSourceIp(e.target.value)}
            disabled={freezeFrame}
            style={{ padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          />

          <input
            type="text"
            placeholder="Dest IP..."
            value={filterDestIp}
            onChange={(e) => setFilterDestIp(e.target.value)}
            disabled={freezeFrame}
            style={{ padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          />

          {freezeFrame && (
            <span title="Freeze frame active" style={{ display: 'flex', alignItems: 'center', color: '#f57c00' }}>
              📷 Frozen
            </span>
          )}
        </div>
      </div>

      {/* Packet list */}
      <div style={{ flex: 1, overflowY: 'auto' }}>
        {isCapturing && (
          <div style={{ padding: '4px 8px', background: '#fff3e0', color: '#e65100', fontSize: 'var(--font-size-sm)' }}>
            📡 Capturing packets...
          </div>
        )}

        {filteredPackets.length === 0 ? (
          <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>
            No packets captured yet. Select a network device and start capture.
          </div>
        ) : (
          filteredPackets.map(packet => (
            <div
              key={packet.id}
              onClick={() => onSelect(packet)}
              style={{
                padding: '8px 12px',
                borderBottom: '1px solid var(--color-border)',
                background: selectedPacket?.id === packet.id ? 'var(--color-primary)' : 'transparent',
                color: selectedPacket?.id === packet.id ? '#fff' : 'inherit',
                cursor: freezeFrame ? 'default' : 'pointer',
                opacity: freezeFrame && selectedPacket?.id !== packet.id ? 0.3 : 1,
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontWeight: 'bold' }}>{packet.protocol}</span>
                <span style={{ fontSize: 'var(--font-size-sm)', opacity: 0.8 }}>
                  {new Date(packet.timestamp).toLocaleTimeString()}
                </span>
              </div>
              <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
                <span>{packet.sourceIp}:{packet.sourcePort}</span>
                <span>→</span>
                <span>{packet.destinationIp}:{packet.destinationPort}</span>
              </div>
              <div style={{ fontSize: 'var(--font-size-sm)', opacity: 0.7, marginTop: '2px' }}>
                {packet.size} bytes
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
