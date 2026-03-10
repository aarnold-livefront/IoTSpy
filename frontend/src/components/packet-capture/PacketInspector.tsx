import { useState } from 'react'
import type { CapturedPacket } from '../../types/api'

interface Props {
  packet: CapturedPacket
  onClose: () => void
}

export default function PacketInspector({ packet, onClose }: Props) {
  const [view, setView] = useState<'details' | 'hex'>('details')

  // Decode payload if available (basic ASCII attempt)
  let decodedText = ''
  try {
    if (packet.payload) {
      const bytes = new Uint8Array(packet.payload)
      decodedText = String.fromCharCode(...bytes.slice(0, 512))
      
      // Check if mostly printable
      let printableCount = 0
      for (let i = 0; i < bytes.length && i < 500; i++) {
        if (bytes[i] >= 32 && bytes[i] <= 126) printableCount++
      }
      if (printableCount / Math.min(bytes.length, 500) < 0.7) {
        decodedText = '' // Not readable text
      }
    }
  } catch {
    // Ignore payload decoding errors
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Header */}
      <div style={{ padding: '12px', background: 'var(--color-surface)', borderBottom: '1px solid var(--color-border)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, fontSize: 'var(--font-size-md)' }}>📦 Packet Inspector</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '20px' }}>✕</button>
        </div>

        {/* View tabs */}
        <div style={{ display: 'flex', gap: '4px', marginTop: '8px' }}>
          <button
            onClick={() => setView('details')}
            style={{
              padding: '4px 12px',
              background: view === 'details' ? 'var(--color-primary)' : 'transparent',
              color: view === 'details' ? '#fff' : 'inherit',
              border: 'none',
              borderRadius: 'var(--radius-sm)',
              cursor: 'pointer',
            }}
          >Details</button>
          <button
            onClick={() => setView('hex')}
            style={{
              padding: '4px 12px',
              background: view === 'hex' ? 'var(--color-primary)' : 'transparent',
              color: view === 'hex' ? '#fff' : 'inherit',
              border: 'none',
              borderRadius: 'var(--radius-sm)',
              cursor: 'pointer',
            }}
          >Hex Dump</button>
        </div>
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflowY: 'auto' }}>
        {view === 'details' ? (
          <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
            <div style={{ marginBottom: '8px' }}>
              <strong>ID:</strong> <code>{packet.id}</code>
            </div>

            <div style={{ marginBottom: '8px' }}>
              <strong>Timestamp:</strong> {new Date(packet.timestamp).toLocaleString()}
            </div>

            <div style={{ marginBottom: '12px', padding: '8px', background: 'var(--color-surface)', borderRadius: '4px' }}>
              <h4 style={{ margin: '0 0 8px 0' }}>Network Layer</h4>
              
              <div style={{ display: 'grid', gridTemplateColumns: '1fr auto 1fr', gap: '8px' }}>
                <div>
                  <strong>Source:</strong><br />
                  {packet.sourceIp}:{packet.sourcePort}
                </div>
                <span style={{ textAlign: 'center', opacity: 0.5 }}>→</span>
                <div>
                  <strong>Destination:</strong><br />
                  {packet.destinationIp}:{packet.destinationPort}
                </div>
              </div>

              <div style={{ marginTop: '8px' }}>
                <strong>Protocol:</strong> {packet.protocol}
              </div>
              <div>
                <strong>Size:</strong> {packet.size} bytes
              </div>
            </div>

            {decodedText && (
              <div style={{ marginBottom: '12px', padding: '8px', background: '#e3f2fd', borderRadius: '4px' }}>
                <h4 style={{ margin: '0 0 8px 0' }}>Payload Preview</h4>
                <pre style={{ 
                  margin: 0, 
                  whiteSpace: 'pre-wrap', 
                  wordBreak: 'break-word',
                  fontFamily: 'var(--font-mono)',
                  fontSize: 'var(--font-size-xs)'
                }}>
                  {decodedText}
                </pre>
              </div>
            )}

            {!decodedText && (
              <div style={{ marginBottom: '12px', padding: '8px', background: '#fff3e0', borderRadius: '4px' }}>
                <strong>Payload:</strong> Binary data - switch to Hex Dump view for detailed analysis.
              </div>
            )}

            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
              Packet captured in freeze frame mode. Click another packet or disable freeze to resume live updates.
            </div>
          </div>
        ) : (
          <HexDumpView packet={packet} />
        )}
      </div>
    </div>
  )
}

function HexDumpView({ packet }: { packet: CapturedPacket }) {
  // Generate hex dump from payload
  let hexLines = ''
  
  if (packet.payload && packet.payload.length > 0) {
    const bytes = new Uint8Array(packet.payload)
    const lines: string[] = []
    
    for (let i = 0; i < bytes.length; i += 16) {
      const chunk = bytes.slice(i, i + 16)
      
      // Hex part
      const hexStr = Array.from(chunk)
        .map(b => b.toString(16).padStart(2, '0'))
        .join(' ')
      
      // ASCII part
      const asciiStr = Array.from(chunk)
        .map(b => b >= 32 && b <= 126 ? String.fromCharCode(b) : '.')
        .join('')
      
      lines.push(`0x${i.toString(16).padStart(8, '0')}  ${hexStr.padEnd(47)} |${asciiStr}|`)
    }
    
    hexLines = lines.join('\n')
  } else {
    hexLines = '(No payload data available)'
  }

  return (
    <pre style={{ 
      padding: '12px', 
      margin: 0,
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--font-size-xs)',
      whiteSpace: 'pre-wrap',
      overflowX: 'auto'
    }}>
      {hexLines}
    </pre>
  )
}
