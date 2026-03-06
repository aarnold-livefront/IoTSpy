import type { PiiStrippingLog, PiiAuditStats } from '../../types/api'

interface Props {
  logs: PiiStrippingLog[]
  total: number
  stats: PiiAuditStats | null
  loading: boolean
  error: string | null
  onRefresh: () => void
}

export default function PiiAuditLog({ logs, total, stats, loading, error, onRefresh }: Props) {
  return (
    <div className="pii-audit-log">
      <div className="openrtb-header">
        <h3>PII Stripping Audit Log ({total})</h3>
        <button onClick={onRefresh} disabled={loading}>
          {loading ? 'Loading...' : 'Refresh'}
        </button>
      </div>

      {error && <div className="openrtb-error">{error}</div>}

      {stats && stats.totalStripped > 0 && (
        <div className="audit-stats">
          <div className="stat-card">
            <div className="stat-value">{stats.totalStripped}</div>
            <div className="stat-label">Total Fields Stripped</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Top Fields</div>
            <div className="stat-list">
              {Object.entries(stats.byFieldPath)
                .sort(([, a], [, b]) => b - a)
                .slice(0, 5)
                .map(([field, count]) => (
                  <div key={field}><code>{field}</code>: {count}</div>
                ))}
            </div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Top Hosts</div>
            <div className="stat-list">
              {Object.entries(stats.byHost)
                .sort(([, a], [, b]) => b - a)
                .slice(0, 5)
                .map(([host, count]) => (
                  <div key={host}>{host}: {count}</div>
                ))}
            </div>
          </div>
        </div>
      )}

      <table className="openrtb-table">
        <thead>
          <tr>
            <th>Time</th>
            <th>Host</th>
            <th>Field</th>
            <th>Strategy</th>
            <th>Preview</th>
          </tr>
        </thead>
        <tbody>
          {logs.map((log) => (
            <tr key={log.id}>
              <td>{new Date(log.strippedAt).toLocaleTimeString()}</td>
              <td title={log.host}>{log.host}</td>
              <td><code>{log.fieldPath}</code></td>
              <td><span className="badge">{log.strategy}</span></td>
              <td className="redacted-preview">{log.redactedPreview}</td>
            </tr>
          ))}
          {logs.length === 0 && !loading && (
            <tr><td colSpan={5} style={{ textAlign: 'center', color: 'var(--color-text-muted)' }}>
              No PII stripping events recorded yet
            </td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
