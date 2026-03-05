import { useEffect, useState } from 'react'
import { getEvent } from '../../api/openrtb'
import type { OpenRtbEvent } from '../../types/api'

interface Props {
  eventId: string | null
}

export default function OpenRtbInspector({ eventId }: Props) {
  const [event, setEvent] = useState<OpenRtbEvent | null>(null)
  const [loading, setLoading] = useState(false)
  const [parsedJson, setParsedJson] = useState<object | null>(null)

  useEffect(() => {
    if (!eventId) {
      setEvent(null)
      setParsedJson(null)
      return
    }
    setLoading(true)
    getEvent(eventId)
      .then((evt) => {
        setEvent(evt)
        try {
          setParsedJson(JSON.parse(evt.rawJson))
        } catch {
          setParsedJson(null)
        }
      })
      .finally(() => setLoading(false))
  }, [eventId])

  if (!eventId) {
    return (
      <div className="openrtb-inspector-empty">
        Select an OpenRTB event to inspect
      </div>
    )
  }

  if (loading) return <div className="openrtb-inspector-loading">Loading...</div>
  if (!event) return <div className="openrtb-inspector-empty">Event not found</div>

  return (
    <div className="openrtb-inspector">
      <h3>
        {event.messageType === 'BidRequest' ? 'Bid Request' : 'Bid Response'}
        {event.version && <span className="version-badge"> v{event.version}</span>}
      </h3>

      <div className="inspector-section">
        <h4>Summary</h4>
        <div className="inspector-grid">
          <div><strong>Exchange:</strong> {event.exchange}</div>
          <div><strong>Time:</strong> {new Date(event.detectedAt).toLocaleString()}</div>
          {event.messageType === 'BidRequest' && (
            <div><strong>Impressions:</strong> {event.impressionCount}</div>
          )}
          {event.messageType === 'BidResponse' && (
            <div><strong>Bids:</strong> {event.bidCount}</div>
          )}
        </div>
      </div>

      <div className="inspector-section">
        <h4>PII Detection</h4>
        <div className="pii-indicators">
          <span className={`pii-indicator ${event.hasDeviceInfo ? 'pii-present' : ''}`}>
            Device Info {event.hasDeviceInfo ? '(detected)' : '(none)'}
          </span>
          <span className={`pii-indicator ${event.hasUserData ? 'pii-present' : ''}`}>
            User Data {event.hasUserData ? '(detected)' : '(none)'}
          </span>
          <span className={`pii-indicator ${event.hasGeoData ? 'pii-present' : ''}`}>
            Geo Data {event.hasGeoData ? '(detected)' : '(none)'}
          </span>
        </div>
      </div>

      <div className="inspector-section">
        <h4>Raw Payload</h4>
        <pre className="json-viewer">
          {parsedJson ? JSON.stringify(parsedJson, null, 2) : event.rawJson}
        </pre>
      </div>
    </div>
  )
}
