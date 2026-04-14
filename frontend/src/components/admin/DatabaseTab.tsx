import { useState, useEffect, useCallback } from 'react'
import type { AdminStats } from '../../types/api'
import { apiFetch, getToken } from '../../api/client'

interface ConfirmState {
  title: string
  message: string
  onConfirm: () => Promise<void>
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString()
}

export default function DatabaseTab() {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<ConfirmState | null>(null)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  const [captureDays, setCaptureDays] = useState(30)
  const [captureHost, setCaptureHost] = useState('')
  const [packetDays, setPacketDays] = useState(30)

  const loadStats = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await apiFetch<AdminStats>('/api/admin/stats')
      setStats(data)
    } catch {
      setError('Failed to load stats')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadStats()
  }, [loadStats])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const runWithConfirm = (title: string, message: string, action: () => Promise<void>) => {
    setConfirm({ title, message, onConfirm: action })
  }

  const purgeCaptures = async (params: string) => {
    setBusy(true)
    try {
      const result = await apiFetch<{ deleted: number }>(`/api/admin/captures?${params}`, { method: 'DELETE' })
      showToast(`Deleted ${result.deleted} captures`)
      await loadStats()
    } catch {
      showToast('Purge failed')
    } finally {
      setBusy(false)
    }
  }

  const purgePackets = async (params: string) => {
    setBusy(true)
    try {
      const result = await apiFetch<{ deleted: number }>(`/api/admin/packets?${params}`, { method: 'DELETE' })
      showToast(`Deleted ${result.deleted} packets`)
      await loadStats()
    } catch {
      showToast('Purge failed')
    } finally {
      setBusy(false)
    }
  }

  const downloadExport = async (url: string, filename: string) => {
    const token = getToken()
    const resp = await fetch(url, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
    if (!resp.ok) {
      showToast('Export failed')
      return
    }
    const blob = await resp.blob()
    const objUrl = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = objUrl
    a.download = filename
    a.click()
    URL.revokeObjectURL(objUrl)
  }

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading stats…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>
  if (!stats) return null

  return (
    <>
      {toast && <div className="admin-toast">{toast}</div>}

      <div className="admin-cards">
        <div className="admin-card">
          <div className="admin-card__title">Captures &amp; Logs</div>
          <div className="admin-card__stats">
            {stats.captures.count.toLocaleString()} rows
            &nbsp;·&nbsp; ~{formatBytes(stats.captures.estimatedSizeBytes)}
            &nbsp;·&nbsp; oldest: {formatDate(stats.captures.oldestTimestamp)}
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge older than {captureDays} days
            </label>
            <input type="range" min={1} max={365} value={captureDays}
              onChange={e => setCaptureDays(+e.target.value)}
              style={{ width: '100%', marginBottom: 4 }} />
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge old captures',
                `Delete all captures older than ${captureDays} days?`,
                () => purgeCaptures(`olderThanDays=${captureDays}`)
              )}>Purge by age</button>
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge by host
            </label>
            <div style={{ display: 'flex', gap: 'var(--space-1)' }}>
              <input placeholder="e.g. api.example.com" value={captureHost}
                onChange={e => setCaptureHost(e.target.value)}
                style={{ flex: 1, fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }} />
              <button className="admin-btn admin-btn--danger" disabled={busy || !captureHost.trim()} onClick={() =>
                runWithConfirm(
                  'Purge captures by host',
                  `Delete all captures for host "${captureHost}"?`,
                  () => purgeCaptures(`host=${encodeURIComponent(captureHost)}`)
                )}>Purge</button>
            </div>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all captures',
                `Delete ALL ${stats.captures.count.toLocaleString()} captures? This cannot be undone.`,
                () => purgeCaptures('purgeAll=true')
              )}>Purge all</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/logs?format=json', 'captures.json')}>Export JSON</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/logs?format=csv', 'captures.csv')}>Export CSV</button>
          </div>
        </div>

        <div className="admin-card">
          <div className="admin-card__title">Packets</div>
          <div className="admin-card__stats">
            {stats.packets.count.toLocaleString()} rows
            &nbsp;·&nbsp; ~{formatBytes(stats.packets.estimatedSizeBytes)}
            &nbsp;·&nbsp; oldest: {formatDate(stats.packets.oldestTimestamp)}
          </div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
              Purge older than {packetDays} days
            </label>
            <input type="range" min={1} max={365} value={packetDays}
              onChange={e => setPacketDays(+e.target.value)}
              style={{ width: '100%', marginBottom: 4 }} />
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge old packets',
                `Delete all packets older than ${packetDays} days?`,
                () => purgePackets(`olderThanDays=${packetDays}`)
              )}>Purge by age</button>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all packets',
                `Delete ALL ${stats.packets.count.toLocaleString()} packets?`,
                () => purgePackets('purgeAll=true')
              )}>Purge all</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/packets?format=json', 'packets.json')}>Export JSON</button>
            <button className="admin-btn" onClick={() => downloadExport('/api/admin/export/packets?format=csv', 'packets.csv')}>Export CSV</button>
          </div>
        </div>

        <div className="admin-card">
          <div className="admin-card__title">Configuration</div>
          <div className="admin-card__stats">
            Rules, breakpoints, scheduled scans, OpenRTB policies, API specs
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--primary"
              onClick={() => downloadExport('/api/admin/export/config', 'iotspy-config.json')}>
              Export JSON
            </button>
          </div>
        </div>
      </div>

      {confirm && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>{confirm.title}</h3>
            <p>{confirm.message}</p>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirm(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={async () => {
                await confirm.onConfirm()
                setConfirm(null)
              }}>Confirm</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
