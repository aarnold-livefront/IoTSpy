import { useState } from 'react'
import HeadersViewer from '../common/HeadersViewer'
import BodyViewer from '../common/BodyViewer'
import { exportAsAsset, downloadBodyUrl } from '../../api/captures'
import type { CapturedRequest } from '../../types/api'
import '../../styles/capture-detail.css'

interface Props {
  capture: CapturedRequest
}

function parseContentType(headersJson: string): string | null {
  try {
    const headers: Record<string, string> = JSON.parse(headersJson)
    const key = Object.keys(headers).find(k => k.toLowerCase() === 'content-type')
    return key ? headers[key].split(';')[0].trim().toLowerCase() : null
  } catch {
    return null
  }
}

function isStreamingType(ct: string | null): boolean {
  return ct === 'text/event-stream' ||
    ct === 'application/x-ndjson' ||
    ct === 'application/json-stream' ||
    ct === 'application/jsonlines'
}

export default function ResponseTab({ capture }: Props) {
  const { statusCode, statusMessage, responseHeaders, responseBody, responseBodySize } = capture
  const [saveState, setSaveState] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [savedFileName, setSavedFileName] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const ct = parseContentType(responseHeaders)
  const showStreamingActions = isStreamingType(ct)

  async function handleSaveAsAsset() {
    setSaveState('saving')
    try {
      const result = await exportAsAsset(capture.id)
      setSavedFileName(result.fileName)
      setSaveState('saved')
    } catch {
      setSaveState('error')
    }
  }

  function handleCopyPath() {
    if (!savedFileName) return
    navigator.clipboard.writeText(savedFileName).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    })
  }

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

      {showStreamingActions && (
        <div className="req-res-section">
          <div className="req-res-section__title">Streaming Asset</div>
          <div className="streaming-asset-actions">
            <button
              className="btn btn-sm btn-secondary"
              onClick={handleSaveAsAsset}
              disabled={saveState === 'saving' || saveState === 'saved'}
            >
              {saveState === 'saving' ? 'Saving…' : saveState === 'saved' ? 'Saved' : 'Save as Asset'}
            </button>
            <a
              className="btn btn-sm btn-secondary"
              href={downloadBodyUrl(capture.id)}
              download
            >
              Download
            </a>
          </div>
          {saveState === 'saved' && savedFileName && (
            <div className="streaming-asset-toast">
              Saved as <code>{savedFileName}</code>
              <button className="btn btn-xs btn-ghost" onClick={handleCopyPath}>
                {copied ? 'Copied!' : 'Copy path'}
              </button>
            </div>
          )}
          {saveState === 'error' && (
            <div className="streaming-asset-toast streaming-asset-toast--error">
              Failed to save asset.
            </div>
          )}
        </div>
      )}

      {responseBodySize > 0 && (
        <div className="req-res-section">
          <div className="req-res-section__title">Response Body</div>
          {responseBody ? (
            <BodyViewer
              body={responseBody}
              headersJson={responseHeaders}
              bodySize={responseBodySize}
            />
          ) : (
            <p className="body-size-note">{responseBodySize.toLocaleString()} bytes (body not captured)</p>
          )}
        </div>
      )}
    </div>
  )
}
