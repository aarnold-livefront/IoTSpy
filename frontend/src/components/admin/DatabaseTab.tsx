import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { AdminStats } from '../../types/api'
import { apiFetch, getToken } from '../../api/client'

interface ConfirmState {
  title: string
  message: string
  onConfirm: () => Promise<unknown>
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

const STATS_KEY = ['admin-stats']

export default function DatabaseTab() {
  const queryClient = useQueryClient()
  const [confirm, setConfirm] = useState<ConfirmState | null>(null)
  const [toast, setToast] = useState<string | null>(null)

  const [captureDays, setCaptureDays] = useState(30)
  const [captureHost, setCaptureHost] = useState('')
  const [packetDays, setPacketDays] = useState(30)

  const {
    data: stats,
    isLoading: loading,
    error: queryError,
  } = useQuery<AdminStats>({
    queryKey: STATS_KEY,
    queryFn: () => apiFetch<AdminStats>('/api/admin/stats'),
  })

  const purgeMutation = useMutation({
    mutationFn: (url: string) =>
      apiFetch<{ deleted: number }>(url, { method: 'DELETE' }),
    onSuccess: (result, url) => {
      const type = url.includes('captures') ? 'captures' : 'packets'
      showToast(`Deleted ${result.deleted} ${type}`)
      void queryClient.invalidateQueries({ queryKey: STATS_KEY })
    },
    onError: () => showToast('Purge failed'),
  })

  const busy = purgeMutation.isPending

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const runWithConfirm = (title: string, message: string, action: () => Promise<unknown>) => {
    setConfirm({ title, message, onConfirm: action })
  }

  const purgeCaptures = (params: string) =>
    purgeMutation.mutateAsync(`/api/admin/captures?${params}`)

  const purgePackets = (params: string) =>
    purgeMutation.mutateAsync(`/api/admin/packets?${params}`)

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
  if (queryError) return <p style={{ color: 'var(--color-error)' }}>Failed to load stats</p>
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
                () => purgeCaptures(`olderThanDays=${captureDays}`),
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
                  () => purgeCaptures(`host=${encodeURIComponent(captureHost)}`),
                )}>Purge</button>
            </div>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all captures',
                `Delete ALL ${stats.captures.count.toLocaleString()} captures? This cannot be undone.`,
                () => purgeCaptures('purgeAll=true'),
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
                () => purgePackets(`olderThanDays=${packetDays}`),
              )}>Purge by age</button>
          </div>
          <div className="admin-card__actions">
            <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() =>
              runWithConfirm(
                'Purge all packets',
                `Delete ALL ${stats.packets.count.toLocaleString()} packets?`,
                () => purgePackets('purgeAll=true'),
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
