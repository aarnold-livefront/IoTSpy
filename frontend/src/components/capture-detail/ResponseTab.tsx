import HeadersViewer from '../common/HeadersViewer'
import type { CapturedRequest } from '../../types/api'
import '../../styles/capture-detail.css'

interface Props {
  capture: CapturedRequest
}

function tryPrettyJson(raw: string): string {
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}

export default function ResponseTab({ capture }: Props) {
  const { statusCode, statusMessage, responseHeaders, responseBody, responseBodySize } = capture

  return (
    <div className="detail-tab-content">
      <div className="req-res-section">
        <div className="req-res-section__title">Status</div>
        <div className="body-viewer">
          <pre>{statusCode} {statusMessage}</pre>
        </div>
      </div>

      <div className="req-res-section">
        <div className="req-res-section__title">Response Headers</div>
        <HeadersViewer headersJson={responseHeaders} />
      </div>

      {responseBodySize > 0 && (
        <div className="req-res-section">
          <div className="req-res-section__title">Response Body</div>
          {responseBody ? (
            <div className="body-viewer">
              <pre>{tryPrettyJson(responseBody)}</pre>
            </div>
          ) : (
            <p className="body-size-note">{responseBodySize.toLocaleString()} bytes (body not captured)</p>
          )}
        </div>
      )}
    </div>
  )
}
