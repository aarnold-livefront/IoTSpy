import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { AuditEntry } from '../../types/api'
import { apiFetch } from '../../api/client'

const PAGE_SIZE = 50

export default function AuditLogTab() {
  const [page, setPage] = useState(1)

  const { data, isLoading: loading, error: queryError } = useQuery<AuditEntry[]>({
    queryKey: ['admin-audit', page],
    queryFn: () => apiFetch<AuditEntry[]>(`/api/auth/audit?count=${PAGE_SIZE * page}`),
  })

  const allEntries = data ?? []
  const total = allEntries.length
  const start = (page - 1) * PAGE_SIZE
  const entries = allEntries.slice(start, start + PAGE_SIZE)
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (queryError) return <p style={{ color: 'var(--color-error)' }}>Failed to load audit log</p>

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
