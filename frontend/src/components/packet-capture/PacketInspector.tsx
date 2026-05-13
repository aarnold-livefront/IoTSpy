import { useState, useEffect } from 'react'
import type { CapturedPacket, FreezeFrameDto } from '../../types/api'
import { createFreezeFrame } from '../../api/packetCapture'
import '../../styles/packet-inspector.css'

interface Props {
  packet: CapturedPacket
  onClose: () => void
}

const TAB_LABELS: Record<'details' | 'hex' | 'layers', string> = {
  details: 'Details',
  hex: 'Hex Dump',
  layers: 'Layers',
}

export default function PacketInspector({ packet, onClose }: Props) {
  const [view, setView] = useState<'details' | 'hex' | 'layers'>('details')
  const [freezeData, setFreezeData] = useState<FreezeFrameDto | null>(null)
  const [loadingFreeze, setLoadingFreeze] = useState(false)

  useEffect(() => {
    if ((view === 'hex' || view === 'layers') && !freezeData) {
      setLoadingFreeze(true)
      createFreezeFrame(packet.id)
        .then(setFreezeData)
        .catch(() => {/* hex dump unavailable */})
        .finally(() => setLoadingFreeze(false))
    }
  }, [view, freezeData, packet.id])

  useEffect(() => { setFreezeData(null) }, [packet.id])

  return (
    <div className="pi-root">
      <div className="pi-header">
        <div className="pi-header__top">
          <h3 className="pi-header__title">Packet Inspector</h3>
          <button className="pi-header__close" onClick={onClose} aria-label="Close packet inspector">×</button>
        </div>

        <div className="pi-tabs" role="tablist" aria-label="Packet inspector views">
          {(['details', 'hex', 'layers'] as const).map((tab) => (
            <button
              key={tab}
              role="tab"
              aria-selected={view === tab}
              aria-controls={`pi-tabpanel-${tab}`}
              id={`pi-tab-${tab}`}
              onClick={() => setView(tab)}
              className={`pi-tab${view === tab ? ' pi-tab--active' : ''}`}
            >
              {TAB_LABELS[tab]}
            </button>
          ))}
        </div>
      </div>

      <div
        className="pi-body"
        role="tabpanel"
        id={`pi-tabpanel-${view}`}
        aria-labelledby={`pi-tab-${view}`}
      >
        {view === 'details' && <DetailsView packet={packet} />}
        {view === 'hex' && <HexDumpView freezeData={freezeData} loading={loadingFreeze} />}
        {view === 'layers' && <LayerView freezeData={freezeData} loading={loadingFreeze} />}
      </div>
    </div>
  )
}

function DetailsView({ packet }: { packet: CapturedPacket }) {
  return (
    <div className="pi-details">
      <div className="pi-details__row">
        <strong>ID:</strong> <code>{packet.id}</code>
      </div>
      <div className="pi-details__row">
        <strong>Timestamp:</strong> {new Date(packet.timestamp).toLocaleString()}
      </div>

      <div className="pi-section">
        <h4>Network</h4>
        <div className="pi-network-grid">
          <div>
            <strong>Source:</strong><br />
            {packet.sourceIp}:{packet.sourcePort}
          </div>
          <span className="pi-network-grid__arrow">&rarr;</span>
          <div>
            <strong>Destination:</strong><br />
            {packet.destinationIp}:{packet.destinationPort}
          </div>
        </div>
        <div className="pi-details__row" style={{ marginTop: 'var(--space-2)' }}>
          <strong>Protocol:</strong> {packet.protocol}
        </div>
        <div className="pi-details__row">
          <strong>Length:</strong> {packet.length} bytes
        </div>
        {packet.tcpFlags && (
          <div className="pi-details__row">
            <strong>TCP Flags:</strong> {packet.tcpFlags}
          </div>
        )}
        {packet.isError && (
          <div className="pi-flag--error"><strong>Error packet</strong></div>
        )}
        {packet.isRetransmission && (
          <div className="pi-flag--retransmission"><strong>Retransmission</strong></div>
        )}
      </div>

      {packet.payloadPreview ? (
        <div className="pi-payload">
          <h4>Payload Preview</h4>
          <pre className="pi-payload__pre">{packet.payloadPreview}</pre>
        </div>
      ) : (
        <div className="pi-no-payload">
          <strong>Payload:</strong> No preview available. Switch to Hex Dump for raw data.
        </div>
      )}
    </div>
  )
}

function HexDumpView({ freezeData, loading }: { freezeData: FreezeFrameDto | null; loading: boolean }) {
  if (loading) return <div className="pi-loading">Loading hex dump...</div>
  if (!freezeData?.hexDump) return <div className="pi-empty">No raw data available for this packet.</div>
  return <pre className="pi-hex-pre">{freezeData.hexDump}</pre>
}

function LayerView({ freezeData, loading }: { freezeData: FreezeFrameDto | null; loading: boolean }) {
  if (loading) return <div className="pi-loading">Loading layer data...</div>
  if (!freezeData) return <div className="pi-empty">No layer data available.</div>

  const layers = [
    { label: 'Layer 2 (Data Link)', content: freezeData.layer2Info },
    { label: 'Layer 3 (Network)', content: freezeData.layer3Info },
    { label: 'Layer 4 (Transport)', content: freezeData.layer4Info },
    { label: 'Protocol Details', content: freezeData.protocolDetails },
  ]

  if (layers.every((l) => !l.content)) {
    return <div className="pi-empty">No layer information available for this packet.</div>
  }

  return (
    <div className="pi-layers">
      {layers.map((layer) =>
        layer.content ? (
          <div key={layer.label} className="pi-layer">
            <h4>{layer.label}</h4>
            <pre className="pi-layer__pre">{layer.content}</pre>
          </div>
        ) : null
      )}
    </div>
  )
}
