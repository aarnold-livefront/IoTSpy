import { useState, useEffect, useCallback } from 'react'
import type { AuditEntry } from '../../types/api'
import { apiFetch } from '../../api/client'

const PAGE_SIZE = 50

export default function AuditLogTab() {
  const [entries, setEntries] = useState<AuditEntry[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (p: number) => {
    setLoading(true)
    setError(null)
    try {
      const all = await apiFetch<AuditEntry[]>(`/api/auth/audit?count=${PAGE_SIZE * p}`)
      setTotal(all.length)
      const start = (p - 1) * PAGE_SIZE
      setEntries(all.slice(start, start + PAGE_SIZE))
    } catch {
      setError('Failed to load audit log')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load(page)
  }, [load, page])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Timestamp</th>
              <th>User</th>
              <th>Action</th>
              <th>Entity</th>
              <th>Details</th>
              <th>IP Address</th>
            </tr>
          </thead>
          <tbody>
            {entries.length === 0 ? (
              <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--color-text-faint)' }}>No entries</td></tr>
            ) : entries.map(e => (
              <tr key={e.id}>
                <td>{new Date(e.timestamp).toLocaleString()}</td>
                <td>{e.username}</td>
                <td>{e.action}</td>
                <td>{e.entityType}{e.entityId ? ` / ${e.entityId.slice(0, 8)}…` : ''}</td>
                <td style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {e.details ?? '—'}
                </td>
                <td>{e.ipAddress}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-pagination">
        <button className="admin-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Prev</button>
        <span>Page {page} of {totalPages}</span>
        <button className="admin-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next →</button>
      </div>
    </>
  )
}
