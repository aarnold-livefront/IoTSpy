import { useEffect, useState } from 'react'
import { getCapture } from '../../api/captures'
import { ApiError } from '../../api/client'
import RequestTab from '../capture-detail/RequestTab'
import ResponseTab from '../capture-detail/ResponseTab'
import TlsTab from '../capture-detail/TlsTab'
import LoadingSpinner from '../common/LoadingSpinner'
import ErrorBanner from '../common/ErrorBanner'
import type { CapturedRequest } from '../../types/api'
import '../../styles/capture-detail.css'

type Tab = 'request' | 'response' | 'tls'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes}b`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}k`
  return `${(bytes / (1024 * 1024)).toFixed(1)}M`
}

interface Props {
  captureId: string | null
}

export default function CaptureDetail({ captureId }: Props) {
  const [capture, setCapture] = useState<CapturedRequest | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<Tab>('request')

  useEffect(() => {
    if (!captureId) {
      setCapture(null)
      return
    }
    let cancelled = false
    setLoading(true)
    setError(null)
    getCapture(captureId)
      .then((data) => {
        if (!cancelled) {
          setCapture(data)
          setActiveTab('request')
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Failed to load capture')
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [captureId])

  if (!captureId) {
    return (
      <div className="capture-detail-pane">
        <div className="capture-detail-placeholder">Select a capture to inspect</div>
      </div>
    )
  }

  if (loading) {
    return (
      <div className="capture-detail-pane">
        <LoadingSpinner />
      </div>
    )
  }

  if (error) {
    return (
      <div className="capture-detail-pane">
        <ErrorBanner message={error} />
      </div>
    )
  }

  if (!capture) return null

  const url = `${capture.scheme}://${capture.host}${capture.port ? `:${capture.port}` : ''}${capture.path}`
  const timestamp = new Date(capture.timestamp).toLocaleString()

  return (
    <div className="capture-detail-pane">
      <div className="capture-detail__summary">
        <span className="capture-detail__url" title={url}>{url}</span>
        <div className="capture-detail__meta">
          <span>{timestamp}</span>
          <span>{capture.clientIp}</span>
          {capture.durationMs > 0 && <span>{capture.durationMs}ms</span>}
        </div>
      </div>

      <div className="detail-tabs">
        {(['request', 'response', 'tls'] as Tab[]).map((tab) => {
          let label = tab.charAt(0).toUpperCase() + tab.slice(1)
          if (tab === 'request' && capture.requestBodySize > 0) {
            label += ` · ${formatBytes(capture.requestBodySize)}`
          } else if (tab === 'response' && capture.responseBodySize > 0) {
            label += ` · ${formatBytes(capture.responseBodySize)}`
          }
          return (
            <button
              key={tab}
              className={`detail-tab${activeTab === tab ? ' detail-tab--active' : ''}`}
              onClick={() => setActiveTab(tab)}
            >
              {label}
            </button>
          )
        })}
      </div>

      {activeTab === 'request' && <RequestTab capture={capture} />}
      {activeTab === 'response' && <ResponseTab capture={capture} />}
      {activeTab === 'tls' && <TlsTab capture={capture} />}
    </div>
  )
}
