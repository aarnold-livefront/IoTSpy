import { useEffect, useRef, useState } from 'react'
import { getCapture } from '../../api/captures'
import { listSessions, addCaptureToSession } from '../../api/sessions'
import { ApiError } from '../../api/client'
import RequestTab from '../capture-detail/RequestTab'
import ResponseTab from '../capture-detail/ResponseTab'
import TlsTab from '../capture-detail/TlsTab'
import LoadingSpinner from '../common/LoadingSpinner'
import ErrorBanner from '../common/ErrorBanner'
import type { CapturedRequest } from '../../types/api'
import type { InvestigationSession } from '../../types/sessions'
import '../../styles/capture-detail.css'

type Tab = 'request' | 'response' | 'tls'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes}b`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}k`
  return `${(bytes / (1024 * 1024)).toFixed(1)}M`
}

interface Props {
  captureId: string | null
  onBack?: () => void
}

export default function CaptureDetail({ captureId, onBack }: Props) {
  const [capture, setCapture] = useState<CapturedRequest | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<Tab>('request')

  // Add-to-session picker
  const [showSessionPicker, setShowSessionPicker] = useState(false)
  const [sessions, setSessions] = useState<InvestigationSession[]>([])
  const [sessionsLoading, setSessionsLoading] = useState(false)
  const [addedMsg, setAddedMsg] = useState<string | null>(null)
  const pickerRef = useRef<HTMLDivElement>(null)

  // Close picker when clicking outside
  useEffect(() => {
    if (!showSessionPicker) return
    const handler = (e: MouseEvent) => {
      if (pickerRef.current && !pickerRef.current.contains(e.target as Node)) {
        setShowSessionPicker(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [showSessionPicker])

  const handleOpenSessionPicker = async () => {
    setShowSessionPicker(v => !v)
    if (sessions.length === 0) {
      setSessionsLoading(true)
      try {
        setSessions(await listSessions())
      } catch {
        // silently fail — picker will show empty
      } finally {
        setSessionsLoading(false)
      }
    }
  }

  const handleAddToSession = async (sessionId: string, sessionName: string) => {
    if (!captureId) return
    try {
      await addCaptureToSession(sessionId, captureId)
      setShowSessionPicker(false)
      setAddedMsg(`Added to "${sessionName}"`)
      setTimeout(() => setAddedMsg(null), 3000)
    } catch {
      setAddedMsg('Failed to add to session')
      setTimeout(() => setAddedMsg(null), 3000)
    }
  }

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
        <div className="capture-detail-placeholder">
          <svg width="48" height="48" viewBox="0 0 48 48" fill="none" aria-hidden="true" className="capture-detail-placeholder__icon">
            <rect x="7" y="11" width="34" height="26" rx="3" stroke="currentColor" strokeWidth="2"/>
            <line x1="7" y1="19" x2="41" y2="19" stroke="currentColor" strokeWidth="1.5"/>
            <line x1="14" y1="27" x2="30" y2="27" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.6"/>
            <line x1="14" y1="32" x2="23" y2="32" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.4"/>
          </svg>
          <span>Select a capture to inspect</span>
        </div>
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
        {onBack && (
          <button className="capture-detail__back" onClick={onBack} aria-label="Back to list">
            <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
              <path d="M2 8l4-4 4 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            Back
          </button>
        )}
        <span className="capture-detail__url" title={url}>{url}</span>
        <div className="capture-detail__meta">
          <span>{timestamp}</span>
          <span>{capture.clientIp}</span>
          {capture.durationMs > 0 && <span>{capture.durationMs}ms</span>}
        </div>
        <div className="capture-detail__session-action" ref={pickerRef}>
          <button
            className="capture-detail__add-to-session"
            onClick={handleOpenSessionPicker}
            title="Add this capture to an investigation session"
          >
            + Session
          </button>
          {showSessionPicker && (
            <div className="capture-detail__session-picker">
              {sessionsLoading && <div className="capture-detail__session-picker-item capture-detail__session-picker-item--muted">Loading…</div>}
              {!sessionsLoading && sessions.length === 0 && (
                <div className="capture-detail__session-picker-item capture-detail__session-picker-item--muted">No active sessions</div>
              )}
              {sessions.map(s => (
                <div
                  key={s.id}
                  className="capture-detail__session-picker-item"
                  onClick={() => void handleAddToSession(s.id, s.name)}
                >
                  {s.name}
                </div>
              ))}
            </div>
          )}
        </div>
        {addedMsg && <span className="capture-detail__added-msg">{addedMsg}</span>}
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
