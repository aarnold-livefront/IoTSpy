import { useState, useMemo } from 'react'
import type { CapturedPacket } from '../../types/api'

interface Props {
  packets: CapturedPacket[]
  isCapturing: boolean
  selectedPacket: any
  onSelect: (packet: any) => void
  freezeFrame: boolean
}

interface Filter {
  protocol: string
  sourceIp: string
  destIp: string
  errorsOnly: boolean
  retransmissionsOnly: boolean
}

export default function PacketListFilterable({ packets, isCapturing, selectedPacket, onSelect, freezeFrame }: Props) {
  const [filter, setFilter] = useState<Filter>({
    protocol: 'all',
    sourceIp: '',
    destIp: '',
    errorsOnly: false,
    retransmissionsOnly: false,
  })

  const filteredPackets = useMemo(() => {
    return packets.filter(packet => {
      if (filter.protocol !== 'all' && packet.protocol.toLowerCase() !== filter.protocol.toLowerCase()) return false
      if (filter.sourceIp && !packet.sourceIp.includes(filter.sourceIp)) return false
      if (filter.destIp && !packet.destinationIp.includes(filter.destIp)) return false
      if (filter.errorsOnly && !packet.isError) return false
      if (filter.retransmissionsOnly && !packet.isRetransmission) return false
      return true
    })
  }, [packets, filter])

  const protocols = useMemo(() => ['all', ...new Set(packets.map(p => p.protocol))], [packets])

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
      {/* Filter controls */}
      <div style={{ padding: '8px 12px', background: 'var(--color-surface)', borderBottom: '1px solid var(--color-border)' }}>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'center' }}>
          <select
            value={filter.protocol}
            onChange={(e) => setFilter(f => ({ ...f, protocol: e.target.value }))}
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
            value={filter.sourceIp}
            onChange={(e) => setFilter(f => ({ ...f, sourceIp: e.target.value }))}
            disabled={freezeFrame}
            style={{ padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          />

          <input
            type="text"
            placeholder="Dest IP..."
            value={filter.destIp}
            onChange={(e) => setFilter(f => ({ ...f, destIp: e.target.value }))}
            disabled={freezeFrame}
            style={{ padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          />

          <label style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: 'var(--font-size-sm)', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={filter.errorsOnly}
              onChange={(e) => setFilter(f => ({ ...f, errorsOnly: e.target.checked }))}
              disabled={freezeFrame}
            />
            Errors only
          </label>

          <label style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: 'var(--font-size-sm)', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={filter.retransmissionsOnly}
              onChange={(e) => setFilter(f => ({ ...f, retransmissionsOnly: e.target.checked }))}
              disabled={freezeFrame}
            />
            Retransmissions only
          </label>

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
                {packet.length} bytes
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
