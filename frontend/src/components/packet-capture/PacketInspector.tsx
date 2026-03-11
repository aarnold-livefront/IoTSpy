import { useState, useEffect } from 'react'
import type { CapturedPacket, FreezeFrameDto } from '../../types/api'
import { createFreezeFrame } from '../../api/packetCapture'

interface Props {
  packet: CapturedPacket
  onClose: () => void
}

export default function PacketInspector({ packet, onClose }: Props) {
  const [view, setView] = useState<'details' | 'hex' | 'layers'>('details')
  const [freezeData, setFreezeData] = useState<FreezeFrameDto | null>(null)
  const [loadingFreeze, setLoadingFreeze] = useState(false)

  // Load freeze frame data when switching to hex/layers view
  useEffect(() => {
    if ((view === 'hex' || view === 'layers') && !freezeData) {
      setLoadingFreeze(true)
      createFreezeFrame(packet.id)
        .then(setFreezeData)
        .catch(() => {/* ignore — hex dump unavailable */})
        .finally(() => setLoadingFreeze(false))
    }
  }, [view, freezeData, packet.id])

  // Reset freeze data when packet changes
  useEffect(() => {
    setFreezeData(null)
  }, [packet.id])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Header */}
      <div style={{ padding: '12px', background: 'var(--color-surface)', borderBottom: '1px solid var(--color-border)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, fontSize: 'var(--font-size-md)' }}>Packet Inspector</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '20px' }}>x</button>
        </div>

        {/* View tabs */}
        <div style={{ display: 'flex', gap: '4px', marginTop: '8px' }}>
          {(['details', 'hex', 'layers'] as const).map(tab => (
            <button
              key={tab}
              onClick={() => setView(tab)}
              style={{
                padding: '4px 12px',
                background: view === tab ? 'var(--color-primary)' : 'transparent',
                color: view === tab ? '#fff' : 'inherit',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                cursor: 'pointer',
                textTransform: 'capitalize',
              }}
            >{tab === 'hex' ? 'Hex Dump' : tab === 'layers' ? 'Layers' : 'Details'}</button>
          ))}
        </div>
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflowY: 'auto' }}>
        {view === 'details' && <DetailsView packet={packet} />}
        {view === 'hex' && <HexDumpView freezeData={freezeData} loading={loadingFreeze} />}
        {view === 'layers' && <LayerView freezeData={freezeData} loading={loadingFreeze} />}
      </div>
    </div>
  )
}

function DetailsView({ packet }: { packet: CapturedPacket }) {
  return (
    <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
      <div style={{ marginBottom: '8px' }}>
        <strong>ID:</strong> <code>{packet.id}</code>
      </div>
      <div style={{ marginBottom: '8px' }}>
        <strong>Timestamp:</strong> {new Date(packet.timestamp).toLocaleString()}
      </div>

      <div style={{ marginBottom: '12px', padding: '8px', background: 'var(--color-surface)', borderRadius: '4px' }}>
        <h4 style={{ margin: '0 0 8px 0' }}>Network</h4>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr auto 1fr', gap: '8px' }}>
          <div>
            <strong>Source:</strong><br />
            {packet.sourceIp}:{packet.sourcePort}
          </div>
          <span style={{ textAlign: 'center', opacity: 0.5, alignSelf: 'center' }}>&rarr;</span>
          <div>
            <strong>Destination:</strong><br />
            {packet.destinationIp}:{packet.destinationPort}
          </div>
        </div>
        <div style={{ marginTop: '8px' }}>
          <strong>Protocol:</strong> {packet.protocol}
        </div>
        <div>
          <strong>Length:</strong> {packet.length} bytes
        </div>
        {packet.tcpFlags && (
          <div>
            <strong>TCP Flags:</strong> {packet.tcpFlags}
          </div>
        )}
        {packet.isError && (
          <div style={{ color: '#d32f2f', marginTop: '4px' }}>
            <strong>Error packet</strong>
          </div>
        )}
        {packet.isRetransmission && (
          <div style={{ color: '#f57c00', marginTop: '4px' }}>
            <strong>Retransmission</strong>
          </div>
        )}
      </div>

      {packet.payloadPreview ? (
        <div style={{ marginBottom: '12px', padding: '8px', background: '#e3f2fd', borderRadius: '4px' }}>
          <h4 style={{ margin: '0 0 8px 0' }}>Payload Preview</h4>
          <pre style={{
            margin: 0,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            fontFamily: 'var(--font-mono)',
            fontSize: 'var(--font-size-xs)',
          }}>
            {packet.payloadPreview}
          </pre>
        </div>
      ) : (
        <div style={{ padding: '8px', background: '#fff3e0', borderRadius: '4px' }}>
          <strong>Payload:</strong> No preview available. Switch to Hex Dump for raw data.
        </div>
      )}
    </div>
  )
}

function HexDumpView({ freezeData, loading }: { freezeData: FreezeFrameDto | null; loading: boolean }) {
  if (loading) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>Loading hex dump...</div>
  }

  if (!freezeData || !freezeData.hexDump) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>No raw data available for this packet.</div>
  }

  return (
    <pre style={{
      padding: '12px',
      margin: 0,
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--font-size-xs)',
      whiteSpace: 'pre',
      overflowX: 'auto',
    }}>
      {freezeData.hexDump}
    </pre>
  )
}

function LayerView({ freezeData, loading }: { freezeData: FreezeFrameDto | null; loading: boolean }) {
  if (loading) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>Loading layer data...</div>
  }

  if (!freezeData) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>No layer data available.</div>
  }

  const layers = [
    { label: 'Layer 2 (Data Link)', content: freezeData.layer2Info },
    { label: 'Layer 3 (Network)', content: freezeData.layer3Info },
    { label: 'Layer 4 (Transport)', content: freezeData.layer4Info },
    { label: 'Protocol Details', content: freezeData.protocolDetails },
  ]

  return (
    <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
      {layers.map(layer => (
        layer.content ? (
          <div key={layer.label} style={{ marginBottom: '12px', padding: '8px', background: 'var(--color-surface)', borderRadius: '4px' }}>
            <h4 style={{ margin: '0 0 8px 0' }}>{layer.label}</h4>
            <pre style={{
              margin: 0,
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              fontFamily: 'var(--font-mono)',
              fontSize: 'var(--font-size-xs)',
            }}>
              {layer.content}
            </pre>
          </div>
        ) : null
      ))}
      {layers.every(l => !l.content) && (
        <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>
          No layer information available for this packet.
        </div>
      )}
    </div>
  )
}
