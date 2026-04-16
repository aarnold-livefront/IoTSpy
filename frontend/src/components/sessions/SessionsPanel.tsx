import { useState, useCallback } from 'react'
import { useSessions, useSessionDetail } from '../../hooks/useSessions'
import PresenceIndicator from './PresenceIndicator'
import AnnotationPanel from './AnnotationPanel'
import {
  createSession,
  deleteSession,
  generateShareToken,
  revokeShareToken,
  exportSession,
} from '../../api/sessions'
import type { CaptureAnnotation } from '../../types/sessions'

type SessionTab = 'captures' | 'annotations' | 'activity'

export default function SessionsPanel() {
  const { sessions, loading, error, reload } = useSessions()
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<SessionTab>('captures')
  const [showCreate, setShowCreate] = useState(false)
  const [newName, setNewName] = useState('')
  const [newDesc, setNewDesc] = useState('')
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  const {
    session,
    captures,
    annotations,
    activity,
    presence,
    setAnnotations,
  } = useSessionDetail(activeSessionId)

  const showToast = useCallback((msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 4000)
  }, [])

  const handleCreate = async () => {
    if (!newName.trim()) return
    setBusy(true)
    try {
      await createSession(newName.trim(), newDesc.trim() || undefined)
      setNewName('')
      setNewDesc('')
      setShowCreate(false)
      void reload()
    } catch {
      showToast('Failed to create session')
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Close this session?')) return
    try {
      await deleteSession(id)
      if (activeSessionId === id) setActiveSessionId(null)
      void reload()
    } catch {
      showToast('Failed to close session')
    }
  }

  const handleShare = async () => {
    if (!activeSessionId) return
    try {
      const { url } = await generateShareToken(activeSessionId)
      await navigator.clipboard.writeText(url)
      showToast('Share URL copied to clipboard')
      void reload()
    } catch {
      showToast('Failed to generate share link')
    }
  }

  const handleRevokeShare = async () => {
    if (!activeSessionId || !confirm('Revoke the share link?')) return
    try {
      await revokeShareToken(activeSessionId)
      showToast('Share link revoked')
      void reload()
    } catch {
      showToast('Failed to revoke share link')
    }
  }

  const handleExport = async () => {
    if (!activeSessionId || !session) return
    try {
      await exportSession(activeSessionId, session.name)
    } catch {
      showToast('Export failed')
    }
  }

  const handleAnnotationAdded = useCallback((ann: CaptureAnnotation) => {
    setAnnotations(prev => [ann, ...prev])
  }, [setAnnotations])

  const handleAnnotationDeleted = useCallback((id: string) => {
    setAnnotations(prev => prev.filter(a => a.id !== id))
  }, [setAnnotations])

  return (
    <div className="sessions-panel">
      {toast && <div className="toast toast--info">{toast}</div>}

      {/* ── Sessions sidebar ─────────────────────────────────────────────────── */}
      <div className="sessions-panel__sidebar">
        <div className="sessions-panel__sidebar-header">
          <h3 className="sessions-panel__title">Sessions</h3>
          <button className="btn btn--primary btn--sm" onClick={() => setShowCreate(true)}>
            + New
          </button>
        </div>

        {showCreate && (
          <div className="sessions-panel__create-form">
            <input
              className="sessions-panel__input"
              placeholder="Session name"
              value={newName}
              onChange={e => setNewName(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleCreate()}
              autoFocus
            />
            <textarea
              className="sessions-panel__input"
              placeholder="Description (optional)"
              value={newDesc}
              onChange={e => setNewDesc(e.target.value)}
              rows={2}
            />
            <div className="sessions-panel__create-actions">
              <button className="btn btn--primary btn--sm" onClick={handleCreate} disabled={busy || !newName.trim()}>
                {busy ? 'Creating…' : 'Create'}
              </button>
              <button className="btn btn--sm" onClick={() => setShowCreate(false)}>Cancel</button>
            </div>
          </div>
        )}

        {loading && <p className="sessions-panel__loading">Loading…</p>}
        {error && <p className="sessions-panel__error">{error}</p>}

        <ul className="sessions-panel__list">
          {sessions.map(s => (
            <li
              key={s.id}
              className={`sessions-panel__item${activeSessionId === s.id ? ' sessions-panel__item--active' : ''}`}
              onClick={() => setActiveSessionId(s.id)}
            >
              <div className="sessions-panel__item-name">{s.name}</div>
              <div className="sessions-panel__item-meta">
                by {s.createdByUsername} · {new Date(s.createdAt).toLocaleDateString()}
              </div>
              {s.hasShareToken && (
                <span className="badge badge--teal" title="Has share link">shared</span>
              )}
              <button
                className="sessions-panel__item-close"
                onClick={e => { e.stopPropagation(); handleDelete(s.id) }}
                title="Close session"
              >
                ×
              </button>
            </li>
          ))}
          {!loading && sessions.length === 0 && (
            <li className="sessions-panel__empty">No active sessions</li>
          )}
        </ul>
      </div>

      {/* ── Session detail ────────────────────────────────────────────────────── */}
      <div className="sessions-panel__detail">
        {!activeSessionId && (
          <div className="sessions-panel__placeholder">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
              <path d="M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
            <p>Select a session to view details</p>
          </div>
        )}

        {activeSessionId && session && (
          <>
            <div className="sessions-panel__detail-header">
              <div>
                <h3 className="sessions-panel__detail-title">{session.name}</h3>
                {session.description && (
                  <p className="sessions-panel__detail-desc">{session.description}</p>
                )}
              </div>
              <PresenceIndicator presence={presence} />
              <div className="sessions-panel__detail-actions">
                <button className="btn btn--sm" onClick={handleExport} title="Export session as ZIP">
                  Export
                </button>
                {session.hasShareToken ? (
                  <button className="btn btn--sm btn--danger" onClick={handleRevokeShare}>
                    Revoke Link
                  </button>
                ) : (
                  <button className="btn btn--sm btn--secondary" onClick={handleShare}>
                    Share
                  </button>
                )}
              </div>
            </div>

            {/* Tabs */}
            <div className="sessions-panel__tabs">
              {(['captures', 'annotations', 'activity'] as const).map(tab => (
                <button
                  key={tab}
                  className={`sessions-panel__tab${activeTab === tab ? ' sessions-panel__tab--active' : ''}`}
                  onClick={() => setActiveTab(tab)}
                >
                  {tab === 'captures' ? `Captures (${captures.length})` :
                   tab === 'annotations' ? `Annotations (${annotations.length})` :
                   'Activity'}
                </button>
              ))}
            </div>

            {/* Captures tab */}
            {activeTab === 'captures' && (
              <div className="sessions-panel__captures">
                {captures.length === 0 && (
                  <p className="sessions-panel__empty">
                    No captures in this session. Add captures from the capture list.
                  </p>
                )}
                {captures.map(sc => (
                  <div key={sc.id} className="sessions-panel__capture-row">
                    {sc.capture ? (
                      <>
                        <span className={`badge badge--method-${sc.capture.method.toLowerCase()}`}>
                          {sc.capture.method}
                        </span>
                        <span className="sessions-panel__capture-host">{sc.capture.host}</span>
                        <span className="sessions-panel__capture-path">{sc.capture.path}</span>
                        <span className={`badge badge--status-${Math.floor(sc.capture.statusCode / 100)}xx`}>
                          {sc.capture.statusCode}
                        </span>
                        <span className="sessions-panel__capture-time">
                          {new Date(sc.capture.timestamp).toLocaleTimeString()}
                        </span>
                      </>
                    ) : (
                      <span className="sessions-panel__capture-id">{sc.captureId}</span>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* Annotations tab */}
            {activeTab === 'annotations' && (
              <AnnotationPanel
                sessionId={activeSessionId}
                captureId={captures[0]?.captureId ?? ''}
                annotations={annotations}
                onAdded={handleAnnotationAdded}
                onDeleted={handleAnnotationDeleted}
              />
            )}

            {/* Activity feed tab */}
            {activeTab === 'activity' && (
              <div className="sessions-panel__activity">
                {activity.length === 0 && (
                  <p className="sessions-panel__empty">No activity yet.</p>
                )}
                {activity.map(a => (
                  <div key={a.id} className="sessions-panel__activity-row">
                    <span className="sessions-panel__activity-user">{a.username}</span>
                    <span className="sessions-panel__activity-action">{a.action}</span>
                    {a.details && (
                      <span className="sessions-panel__activity-details">{a.details}</span>
                    )}
                    <span className="sessions-panel__activity-time">
                      {new Date(a.timestamp).toLocaleTimeString()}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
