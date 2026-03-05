import type { OpenRtbEvent } from '../../types/api'

interface Props {
  events: OpenRtbEvent[]
  total: number
  loading: boolean
  error: string | null
  selectedId: string | null
  onSelect: (id: string) => void
  onRefresh: () => void
}

export default function OpenRtbTrafficList({
  events, total, loading, error, selectedId, onSelect, onRefresh,
}: Props) {
  return (
    <div className="openrtb-traffic-list">
      <div className="openrtb-header">
        <h3>OpenRTB Traffic ({total})</h3>
        <button onClick={onRefresh} disabled={loading}>
          {loading ? 'Loading...' : 'Refresh'}
        </button>
      </div>

      {error && <div className="openrtb-error">{error}</div>}

      <table className="openrtb-table">
        <thead>
          <tr>
            <th>Time</th>
            <th>Exchange</th>
            <th>Type</th>
            <th>Imps</th>
            <th>PII</th>
          </tr>
        </thead>
        <tbody>
          {events.map((evt) => (
            <tr
              key={evt.id}
              className={selectedId === evt.id ? 'selected' : ''}
              onClick={() => onSelect(evt.id)}
            >
              <td>{new Date(evt.detectedAt).toLocaleTimeString()}</td>
              <td title={evt.exchange}>{evt.exchange}</td>
              <td>
                <span className={`badge badge--${evt.messageType === 'BidRequest' ? 'info' : 'success'}`}>
                  {evt.messageType === 'BidRequest' ? 'Req' : 'Resp'}
                </span>
              </td>
              <td>{evt.messageType === 'BidRequest' ? evt.impressionCount : evt.bidCount}</td>
              <td>
                {(evt.hasDeviceInfo || evt.hasUserData || evt.hasGeoData) && (
                  <span className="badge badge--warning" title="PII fields detected">
                    PII
                  </span>
                )}
              </td>
            </tr>
          ))}
          {events.length === 0 && !loading && (
            <tr><td colSpan={5} style={{ textAlign: 'center', color: 'var(--color-text-muted)' }}>
              No OpenRTB traffic captured yet
            </td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
