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

export default function RequestTab({ capture }: Props) {
  const { method, scheme, host, port, path, query, requestHeaders, requestBody, requestBodySize } = capture
  const url = `${scheme}://${host}${port ? `:${port}` : ''}${path}${query ? `?${query}` : ''}`

  return (
    <div className="detail-tab-content">
      <div className="req-res-section">
        <div className="req-res-section__title">URL</div>
        <div className="body-viewer">
          <pre>{method} {url}</pre>
        </div>
      </div>

      <div className="req-res-section">
        <div className="req-res-section__title">Request Headers</div>
        <HeadersViewer headersJson={requestHeaders} />
      </div>

      {requestBodySize > 0 && (
        <div className="req-res-section">
          <div className="req-res-section__title">Request Body</div>
          {requestBody ? (
            <div className="body-viewer">
              <pre>{tryPrettyJson(requestBody)}</pre>
            </div>
          ) : (
            <p className="body-size-note">{requestBodySize.toLocaleString()} bytes (body not captured)</p>
          )}
        </div>
      )}
    </div>
  )
}
