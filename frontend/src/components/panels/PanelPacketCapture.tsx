import { useState } from 'react'
import { usePacketCapture } from '../../hooks/usePacketCapture'
import PacketListFilterable from '../../components/packet-capture/PacketListFilterable'
import PacketInspector from '../../components/packet-capture/PacketInspector'

export default function PanelPacketCapture() {
  const { devices, packets, isCapturing, startCapture, stopCapture, clearPackets, error } = usePacketCapture()
  const [selectedDevice, setSelectedDevice] = useState<string | null>(null)
  const [selectedPacket, setSelectedPacket] = useState<any>(null)

  return (
    <div style={{ padding: '16px', height: '100%', display: 'flex', gap: '16px' }}>
      {/* Left panel - Device selection and capture controls */}
      <div style={{ width: '320px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
        <h3 style={{ margin: 0, fontSize: 'var(--font-size-md)' }}>📡 Network Device Selection</h3>

        {error && (
          <div style={{ padding: '8px', background: '#fee', borderRadius: '4px', color: '#c00' }}>
            {error}
          </div>
        )}

        <div>
          <label style={{ display: 'block', marginBottom: '4px', fontSize: 'var(--font-size-sm)' }}>
            Network Interface:
          </label>
          <select
            value={selectedDevice || ''}
            onChange={(e) => setSelectedDevice(e.target.value)}
            disabled={isCapturing}
            style={{ width: '100%', padding: '8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          >
            <option value="">Select a device...</option>
            {devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.displayName} ({device.name})
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'flex', gap: '8px' }}>
          {!isCapturing && selectedDevice && (
            <button
              onClick={() => startCapture(selectedDevice)}
              disabled={!selectedDevice}
              style={{
                flex: 1,
                padding: '10px',
                background: 'var(--color-primary)',
                color: '#fff',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                cursor: selectedDevice ? 'pointer' : 'not-allowed',
              }}
            >
              Start Capture
            </button>
          )}

          {isCapturing && (
            <button
              onClick={stopCapture}
              style={{
                flex: 1,
                padding: '10px',
                background: '#d32f2f',
                color: '#fff',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                cursor: 'pointer',
              }}
            >
              Stop Capture
            </button>
          )}
        </div>

        <div style={{ marginTop: 'auto' }}>
          <button
            onClick={clearPackets}
            disabled={!packets.length || isCapturing}
            style={{
              width: '100%',
              padding: '8px',
              background: isCapturing ? '#ccc' : 'var(--color-surface-2)',
              color: 'var(--color-text)',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              cursor: packets.length && !isCapturing ? 'pointer' : 'not-allowed',
            }}
          >
            Clear Packets ({packets.length})
          </button>
        </div>
      </div>

      {/* Middle panel - Packet list with filtering */}
      <PacketListFilterable 
        packets={packets}
        isCapturing={isCapturing}
        selectedPacket={selectedPacket}
        onSelect={setSelectedPacket}
        freezeFrame={selectedPacket !== null}
      />

      {/* Right panel - Packet inspector (frozen frame) */}
      {selectedPacket && (
        <div style={{ width: '400px' }}>
          <PacketInspector packet={selectedPacket} onClose={() => setSelectedPacket(null)} />
        </div>
      )}
    </div>
  )
}
