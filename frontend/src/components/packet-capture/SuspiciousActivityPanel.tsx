import type { SuspiciousActivityDto } from '../../types/api'

interface Props {
  activities: SuspiciousActivityDto[]
  loading: boolean
}

const SEVERITY_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  Low: { bg: '#e8f5e9', text: '#2e7d32', border: '#a5d6a7' },
  Medium: { bg: '#fff3e0', text: '#e65100', border: '#ffcc80' },
  High: { bg: '#ffebee', text: '#c62828', border: '#ef9a9a' },
  Critical: { bg: '#f3e5f5', text: '#6a1b9a', border: '#ce93d8' },
}

export default function SuspiciousActivityPanel({ activities, loading }: Props) {
  if (loading) {
    return <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>Analyzing traffic...</div>
  }

  if (!activities.length) {
    return (
      <div style={{ padding: '24px', textAlign: 'center', color: 'var(--color-text-muted)' }}>
        No suspicious activity detected.
      </div>
    )
  }

  // Sort by severity: Critical > High > Medium > Low
  const severityOrder: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 }
  const sorted = [...activities].sort((a, b) =>
    (severityOrder[a.severity] ?? 4) - (severityOrder[b.severity] ?? 4)
  )

  return (
    <div style={{ padding: '12px', fontSize: 'var(--font-size-sm)' }}>
      <div style={{ marginBottom: '12px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        {(['Critical', 'High', 'Medium', 'Low'] as const).map(sev => {
          const count = activities.filter(a => a.severity === sev).length
          if (!count) return null
          const colors = SEVERITY_COLORS[sev]
          return (
            <span key={sev} style={{
              padding: '2px 8px',
              background: colors.bg,
              color: colors.text,
              border: `1px solid ${colors.border}`,
              borderRadius: '12px',
              fontSize: 'var(--font-size-xs)',
            }}>
              {sev}: {count}
            </span>
          )
        })}
      </div>

      {sorted.map(activity => {
        const colors = SEVERITY_COLORS[activity.severity] ?? SEVERITY_COLORS.Medium
        return (
          <div
            key={activity.id}
            style={{
              marginBottom: '10px',
              padding: '10px',
              border: `1px solid ${colors.border}`,
              borderLeft: `4px solid ${colors.text}`,
              borderRadius: '4px',
              background: colors.bg,
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <strong style={{ color: colors.text }}>[{activity.severity}] {activity.category}</strong>
                <div style={{ marginTop: '4px' }}>{activity.description}</div>
              </div>
              <span style={{
                padding: '2px 6px',
                background: colors.text,
                color: '#fff',
                borderRadius: '4px',
                fontSize: 'var(--font-size-xs)',
                whiteSpace: 'nowrap',
              }}>
                {activity.packetCount} pkts
              </span>
            </div>

            <div style={{ marginTop: '6px', fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
              <span>Source: <code>{activity.sourceIp}</code></span>
              {activity.destinationIp && <span> &rarr; Dest: <code>{activity.destinationIp}</code></span>}
              <span style={{ marginLeft: '12px' }}>
                Detected: {new Date(activity.firstDetected).toLocaleString()}
              </span>
            </div>

            {activity.evidence.length > 0 && (
              <div style={{ marginTop: '6px' }}>
                <strong style={{ fontSize: 'var(--font-size-xs)' }}>Evidence:</strong>
                <ul style={{ margin: '4px 0 0 16px', padding: 0, fontSize: 'var(--font-size-xs)' }}>
                  {activity.evidence.map((e, i) => (
                    <li key={i}>{e}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
