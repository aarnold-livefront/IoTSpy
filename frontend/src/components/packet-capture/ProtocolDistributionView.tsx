import type { ProtocolDistributionDto, ProtocolStatsDto } from '../../types/api'

interface Props {
  distribution: ProtocolDistributionDto | null
  loading: boolean
}

const BAR_COLORS = ['#1976d2', '#388e3c', '#f57c00', '#d32f2f', '#7b1fa2', '#0097a7', '#c2185b', '#455a64']

export default function ProtocolDistributionView({ distribution, loading }: Props) {
  if (loading) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>Loading protocol analysis...</div>
  }

  if (!distribution || distribution.totalPackets === 0) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>No packets captured yet.</div>
  }

  return (
    <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
      <div style={{ marginBottom: '4px', fontWeight: 'bold' }}>
        Total Packets: {distribution.totalPackets.toLocaleString()}
      </div>

      <StatsSection title="Application Protocols" stats={distribution.byProtocol} />
      <StatsSection title="Layer 3 (Network)" stats={distribution.byLayer3} />
      <StatsSection title="Layer 4 (Transport)" stats={distribution.byLayer4} />
    </div>
  )
}

function StatsSection({ title, stats }: { title: string; stats: ProtocolStatsDto[] }) {
  if (!stats.length) return null

  return (
    <div style={{ marginTop: '12px' }}>
      <h4 style={{ margin: '0 0 8px 0' }}>{title}</h4>
      {stats.map((s, i) => (
        <div key={s.name} style={{ marginBottom: '6px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '2px' }}>
            <span>{s.name}</span>
            <span>{s.count.toLocaleString()} ({s.percentage.toFixed(1)}%)</span>
          </div>
          <div style={{ height: '8px', background: 'var(--color-surface-2)', borderRadius: '4px', overflow: 'hidden' }}>
            <div style={{
              height: '100%',
              width: `${Math.max(s.percentage, 1)}%`,
              background: BAR_COLORS[i % BAR_COLORS.length],
              borderRadius: '4px',
              transition: 'width 0.3s ease',
            }} />
          </div>
        </div>
      ))}
    </div>
  )
}
