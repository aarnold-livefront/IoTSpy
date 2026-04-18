import { useCallback, useEffect, useState } from 'react'
import '../../styles/passive.css'
import type { PassiveCaptureSummary as Summary, PassiveCaptureSession } from '../../types/api'
import {
  clearBuffer,
  clearDeviceFilter,
  deletePassiveSession,
  getPassiveSummary,
  listPassiveSessions,
  savePassiveSession,
  setDeviceFilter,
} from '../../api/passiveCapture'

type Tab = 'summary' | 'sessions'

export default function PassiveCaptureSummary() {
  const [tab, setTab] = useState<Tab>('summary')
  const [summary, setSummary] = useState<Summary | null>(null)
  const [sessions, setSessions] = useState<PassiveCaptureSession[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [filterInput, setFilterInput] = useState('')
  const [saveDialogOpen, setSaveDialogOpen] = useState(false)
  const [saveName, setSaveName] = useState('')
  const [saveDesc, setSaveDesc] = useState('')
  const [saveClear, setSaveClear] = useState(false)

  const loadSummary = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const s = await getPassiveSummary()
      setSummary(s)
      setFilterInput(s.activeDeviceFilter.join(', '))
    } catch {
      setError('Failed to load passive capture summary.')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadSessions = useCallback(async () => {
    try {
      setSessions(await listPassiveSessions())
    } catch {
      setError('Failed to load sessions.')
    }
  }, [])

  useEffect(() => {
    loadSummary()
    loadSessions()
    const interval = setInterval(loadSummary, 5000)
    return () => clearInterval(interval)
  }, [loadSummary, loadSessions])

  async function handleApplyFilter() {
    const ips = filterInput
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean)
    try {
      if (ips.length === 0) await clearDeviceFilter()
      else await setDeviceFilter(ips)
      await loadSummary()
    } catch {
      setError('Failed to update device filter.')
    }
  }

  async function handleClearBuffer() {
    await clearBuffer()
    await loadSummary()
  }

  async function handleSaveSession() {
    if (!saveName.trim()) return
    const ips = filterInput
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean)
    try {
      await savePassiveSession(saveName, saveDesc || undefined, ips.length > 0 ? ips : undefined, saveClear)
      setSaveDialogOpen(false)
      setSaveName('')
      setSaveDesc('')
      await loadSessions()
      await loadSummary()
    } catch {
      setError('Failed to save session.')
    }
  }

  async function handleDeleteSession(id: string) {
    await deletePassiveSession(id)
    await loadSessions()
  }

  const maxCount = summary?.topEndpoints[0]?.count ?? 1

  return (
    <div className="passive-summary">
      <div className="passive-summary__header">
        <h2 className="passive-summary__title">Passive Capture</h2>
        <div className="passive-summary__tabs">
          {(['summary', 'sessions'] as Tab[]).map((t) => (
            <button
              key={t}
              className={`passive-summary__tab${tab === t ? ' passive-summary__tab--active' : ''}`}
              onClick={() => setTab(t)}
            >
              {t === 'summary' ? 'Live Summary' : 'Saved Sessions'}
            </button>
          ))}
        </div>
      </div>

      {error && <div className="passive-summary__error">{error}</div>}

      {tab === 'summary' && (
        <>
          {/* Device filter bar */}
          <div className="passive-summary__filter-bar">
            <label className="passive-summary__label">Device filter (IPs, comma-separated):</label>
            <input
              className="passive-summary__input"
              value={filterInput}
              onChange={(e) => setFilterInput(e.target.value)}
              placeholder="e.g. 192.168.1.10, 192.168.1.20  (empty = all devices)"
            />
            <button className="passive-summary__btn" onClick={handleApplyFilter}>Apply</button>
            <button className="passive-summary__btn passive-summary__btn--secondary" onClick={handleClearBuffer}>
              Clear Buffer
            </button>
            <button className="passive-summary__btn passive-summary__btn--save" onClick={() => setSaveDialogOpen(true)}>
              Save Session
            </button>
          </div>

          {loading && !summary && <div className="passive-summary__loading">Loading…</div>}

          {summary && (
            <div className="passive-summary__stats-grid">
              {/* Total requests */}
              <div className="passive-summary__stat-card">
                <div className="passive-summary__stat-value">{summary.totalRequests.toLocaleString()}</div>
                <div className="passive-summary__stat-label">Buffered requests</div>
              </div>

              {/* Top hosts */}
              <div className="passive-summary__stat-card passive-summary__stat-card--wide">
                <div className="passive-summary__section-title">Top Hosts</div>
                <ul className="passive-summary__host-list">
                  {summary.topHosts.map((h) => (
                    <li key={h} className="passive-summary__host-item">{h}</li>
                  ))}
                  {summary.topHosts.length === 0 && (
                    <li className="passive-summary__empty">No traffic captured yet</li>
                  )}
                </ul>
              </div>

              {/* Status codes */}
              <div className="passive-summary__stat-card">
                <div className="passive-summary__section-title">Status Codes</div>
                <ul className="passive-summary__code-list">
                  {summary.statusCodes.map((b) => (
                    <li key={b.statusCode} className="passive-summary__code-item">
                      <span
                        className={`passive-summary__code-badge passive-summary__code-badge--${Math.floor(b.statusCode / 100)}xx`}
                      >
                        {b.statusCode}
                      </span>
                      <span className="passive-summary__code-count">{b.count.toLocaleString()}</span>
                    </li>
                  ))}
                  {summary.statusCodes.length === 0 && (
                    <li className="passive-summary__empty">No responses yet</li>
                  )}
                </ul>
              </div>
            </div>
          )}

          {/* Endpoint frequency heatmap */}
          {summary && summary.topEndpoints.length > 0 && (
            <div className="passive-summary__endpoints">
              <div className="passive-summary__section-title">Endpoint Frequency</div>
              <div className="passive-summary__endpoint-list">
                {summary.topEndpoints.map((ep, i) => (
                  <div key={i} className="passive-summary__endpoint-row">
                    <span className="passive-summary__method">{ep.method}</span>
                    <span className="passive-summary__endpoint-path">
                      {ep.host}{ep.path}
                    </span>
                    <div className="passive-summary__bar-wrapper">
                      <div
                        className="passive-summary__bar"
                        style={{ width: `${Math.round((ep.count / maxCount) * 100)}%` }}
                      />
                    </div>
                    <span className="passive-summary__endpoint-count">{ep.count.toLocaleString()}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Save session dialog */}
          {saveDialogOpen && (
            <div className="passive-summary__dialog-overlay" onClick={() => setSaveDialogOpen(false)}>
              <div className="passive-summary__dialog" onClick={(e) => e.stopPropagation()}>
                <h3 className="passive-summary__dialog-title">Save Passive Session</h3>
                <label className="passive-summary__label">Name</label>
                <input
                  className="passive-summary__input"
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                  placeholder="Session name"
                  autoFocus
                />
                <label className="passive-summary__label">Description (optional)</label>
                <input
                  className="passive-summary__input"
                  value={saveDesc}
                  onChange={(e) => setSaveDesc(e.target.value)}
                  placeholder="Description"
                />
                <label className="passive-summary__label passive-summary__label--checkbox">
                  <input
                    type="checkbox"
                    checked={saveClear}
                    onChange={(e) => setSaveClear(e.target.checked)}
                  />
                  Clear buffer after saving
                </label>
                <div className="passive-summary__dialog-actions">
                  <button className="passive-summary__btn passive-summary__btn--save" onClick={handleSaveSession}>
                    Save
                  </button>
                  <button className="passive-summary__btn passive-summary__btn--secondary" onClick={() => setSaveDialogOpen(false)}>
                    Cancel
                  </button>
                </div>
              </div>
            </div>
          )}
        </>
      )}

      {tab === 'sessions' && (
        <div className="passive-summary__sessions">
          {sessions.length === 0 ? (
            <div className="passive-summary__empty-state">
              No saved sessions yet. Use the &quot;Save Session&quot; button in the Live Summary tab to persist a snapshot.
            </div>
          ) : (
            <table className="passive-summary__table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Captures</th>
                  <th>Device Filter</th>
                  <th>Created</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((s) => (
                  <tr key={s.id}>
                    <td>{s.name}</td>
                    <td>{s.entryCount.toLocaleString()}</td>
                    <td>{s.deviceFilter ?? 'All devices'}</td>
                    <td>{new Date(s.createdAt).toLocaleString()}</td>
                    <td>
                      <button
                        className="passive-summary__btn passive-summary__btn--danger"
                        onClick={() => handleDeleteSession(s.id)}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}
