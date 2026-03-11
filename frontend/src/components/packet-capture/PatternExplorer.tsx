import type { CommunicationPatternDto } from '../../types/api'

interface Props {
  patterns: CommunicationPatternDto[]
  loading: boolean
}

export default function PatternExplorer({ patterns, loading }: Props) {
  if (loading) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>Loading patterns...</div>
  }

  if (!patterns.length) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>No communication patterns detected yet.</div>
  }

  const maxPackets = Math.max(...patterns.map(p => p.packetCount))

  return (
    <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
      <h4 style={{ margin: '0 0 12px 0' }}>Top Communication Pairs</h4>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ borderBottom: '2px solid var(--color-border)', textAlign: 'left' }}>
            <th style={{ padding: '6px 8px' }}>Source</th>
            <th style={{ padding: '6px 8px' }}>Destination</th>
            <th style={{ padding: '6px 8px' }}>Packets</th>
            <th style={{ padding: '6px 8px' }}>Bytes</th>
            <th style={{ padding: '6px 8px' }}>Protocols</th>
            <th style={{ padding: '6px 8px' }}>Time Range</th>
          </tr>
        </thead>
        <tbody>
          {patterns.map((p, i) => (
            <tr key={i} style={{ borderBottom: '1px solid var(--color-border)' }}>
              <td style={{ padding: '6px 8px', fontFamily: 'var(--font-mono)', fontSize: 'var(--font-size-xs)' }}>
                {p.sourceIp}
              </td>
              <td style={{ padding: '6px 8px', fontFamily: 'var(--font-mono)', fontSize: 'var(--font-size-xs)' }}>
                {p.destinationIp}
              </td>
              <td style={{ padding: '6px 8px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <div style={{
                    height: '6px',
                    width: `${(p.packetCount / maxPackets) * 80}px`,
                    background: '#1976d2',
                    borderRadius: '3px',
                    minWidth: '4px',
                  }} />
                  <span>{p.packetCount.toLocaleString()}</span>
                </div>
              </td>
              <td style={{ padding: '6px 8px' }}>
                {formatBytes(p.totalBytes)}
              </td>
              <td style={{ padding: '6px 8px' }}>
                {p.protocolsUsed.join(', ')}
              </td>
              <td style={{ padding: '6px 8px', fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
                {p.firstSeen && p.lastSeen
                  ? `${new Date(p.firstSeen).toLocaleTimeString()} - ${new Date(p.lastSeen).toLocaleTimeString()}`
                  : '-'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
