import { useEffect, useRef, useState } from 'react'
import type { NetworkDevice, CapturedPacket } from '../../types/api'

export default function PacketCapturePanel() {
  const [devices, setDevices] = useState<NetworkDevice[]>([])
  const [selectedDevice, setSelectedDevice] = useState<string | null>(null)
  const [isCapturing, setIsCapturing] = useState(false)
  const [packets, setPackets] = useState<CapturedPacket[]>([])
  const [filterText, setFilterText] = useState('')
  const wsRef = useRef<WebSocket | null>(null)

  useEffect(() => {
    loadDevices()
    connectWs()

    return () => {
      wsRef.current?.close()
    }
  }, [])

  const loadDevices = async () => {
    try {
      const res = await fetch('/api/packet-capture/devices')
      if (res.ok) {
        const data: { items: NetworkDevice[] } = await res.json()
        setDevices(data.items || [])
      }
    } catch {
      // Ignore errors
    }
  }

  const connectWs = () => {
    wsRef.current?.close()
    const ws = new WebSocket(`ws://localhost:5000/packet-capture`)
    ws.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data) as { type: string; packet?: CapturedPacket; isCapturing?: boolean }
if (message.type === 'PACKET' && message.packet) {
              setPackets((prev) => [message.packet!, ...prev].slice(0, 500))
            } else if (message.type === 'CAPTURE_STATUS') {
              setIsCapturing(message.isCapturing ?? false)
            }
      } catch {
        // Ignore invalid JSON
      }
    }
    ws.onerror = () => {
      console.error('WebSocket error in packet capture')
    }
  }

  const handleStartCapture = async () => {
    if (!selectedDevice) return
    try {
      await fetch('/api/packet-capture/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ deviceNameOrIp: selectedDevice }),
      })
      setIsCapturing(true)
      setPackets([])
    } catch {
      // Ignore errors
    }
  }

  const handleStopCapture = async () => {
    try {
      await fetch('/api/packet-capture/stop', { method: 'POST' })
      setIsCapturing(false)
    } catch {
      // Ignore errors
    }
  }

  const filteredPackets = packets.filter((p) => {
    if (!filterText) return true
    const text = filterText.toLowerCase()
    return (
      p.sourceIp.includes(text) ||
      p.destinationIp.includes(text) ||
      p.protocol.toLowerCase().includes(text) ||
      p.id.includes(text)
    )
  })

  return (
    <div className="panel-packet-capture">
      <h2>📡 Packet Capture</h2>

      <div style={{ marginBottom: '1rem' }}>
        <label style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 600 }}>
          Select Network Interface:
        </label>
        <select
          value={selectedDevice || ''}
          onChange={(e) => setSelectedDevice(e.target.value)}
          disabled={isCapturing}
          style={{ width: '100%', padding: '0.5rem', borderRadius: 4, border: '1px solid #ccc' }}
        >
          <option value="">-- Select Interface --</option>
          {devices.map((d) => (
            <option key={d.name} value={d.name}>
              {d.displayName || d.name} ({d.ipAddresses[0]})
            </option>
          ))}
        </select>
      </div>

      <div style={{ marginBottom: '1rem' }}>
        <button
          onClick={handleStartCapture}
          disabled={!selectedDevice || isCapturing}
          className="btn--start-capture"
        >
          Start Capture
        </button>
        <button onClick={handleStopCapture} disabled={!isCapturing} className="btn--stop-capture">
          Stop Capture
        </button>
      </div>

      {isCapturing && (
        <p style={{ color: '#4caf50', fontWeight: 600 }}>● Capturing packets on "{selectedDevice}"...</p>
      )}

      <div style={{ marginBottom: '1rem' }}>
        <input
          type="text"
          placeholder="Filter by IP, protocol, or ID..."
          value={filterText}
          onChange={(e) => setFilterText(e.target.value)}
          style={{ width: '100%', padding: '0.5rem', borderRadius: 4, border: '1px solid #ccc' }}
        />
      </div>

      <table className="packet-list-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Time</th>
            <th>Source</th>
            <th>Destination</th>
            <th>Protocol</th>
            <th>Size</th>
            <th>Action</th>
          </tr>
        </thead>
        <tbody>
          {filteredPackets.map((packet) => (
            <tr key={packet.id} className="packet-row">
              <td>{packets.indexOf(packet)}</td>
              <td>{new Date(packet.timestamp).toLocaleTimeString()}</td>
              <td>{packet.sourceIp}:{packet.sourcePort}</td>
              <td>{packet.destinationIp}:{packet.destinationPort}</td>
              <td>{packet.protocol}</td>
              <td>{packet.size} bytes</td>
              <td>
                <button
                  onClick={() => inspectPacket(packet)}
                  style={{ background: 'none', border: 'none', color: '#4caf50', cursor: 'pointer' }}
                >
                  Inspect
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {filteredPackets.length === 0 && (
        <p style={{ color: '#aaa', textAlign: 'center', marginTop: '2rem' }}>
          No packets captured yet. Select an interface and start capturing.
        </p>
      )}
    </div>
  )

  function inspectPacket(packet: CapturedPacket) {
    console.log('Inspecting packet:', packet)
  }
}
