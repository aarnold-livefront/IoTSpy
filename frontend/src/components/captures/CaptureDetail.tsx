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
        {(['request', 'response', 'tls'] as Tab[]).map((tab) => (
          <button
            key={tab}
            className={`detail-tab${activeTab === tab ? ' detail-tab--active' : ''}`}
            onClick={() => setActiveTab(tab)}
          >
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {activeTab === 'request' && <RequestTab capture={capture} />}
      {activeTab === 'response' && <ResponseTab capture={capture} />}
      {activeTab === 'tls' && <TlsTab capture={capture} />}
    </div>
  )
}
