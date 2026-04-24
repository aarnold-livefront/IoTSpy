import { useState } from 'react'
import ConfirmDialog from '../common/ConfirmDialog'
import type { ScanJob } from '../../types/api'
import '../../styles/scanner.css'

interface Props {
  jobs: ScanJob[]
  selectedId: string | null
  onSelect: (id: string) => void
  onCancel: (id: string) => void
  onDelete: (id: string) => void
}

const statusClass: Record<string, string> = {
  Pending: 'scan-status--pending',
  Running: 'scan-status--running',
  Completed: 'scan-status--completed',
  Failed: 'scan-status--failed',
  Cancelled: 'scan-status--cancelled',
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function ScanJobList({ jobs, selectedId, onSelect, onCancel, onDelete }: Props) {
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)

  if (jobs.length === 0) {
    return (
      <div className="scan-job-list__empty">
        No scan jobs yet. Start a scan above.
      </div>
    )
  }

  return (
    <>
      {confirmDeleteId && (
        <ConfirmDialog
          title="Delete scan job"
          message="Delete this scan job and all its findings? This cannot be undone."
          confirmLabel="Delete"
          danger
          onConfirm={() => { onDelete(confirmDeleteId); setConfirmDeleteId(null) }}
          onCancel={() => setConfirmDeleteId(null)}
        />
      )}

      <div className="scan-job-list">
        {jobs.map((job) => (
          <div
            key={job.id}
            className={`scan-job-row ${selectedId === job.id ? 'scan-job-row--selected' : ''}`}
            onClick={() => onSelect(job.id)}
          >
            <div className="scan-job-row__info">
              <span className={`scan-status ${statusClass[job.status] ?? ''}`}>
                {job.status}
              </span>
              <span className="scan-job-row__device">{job.deviceId.slice(0, 8)}...</span>
              <span className="scan-job-row__ports">
                {job.openPortsFound} open / {job.portsScanned} scanned
              </span>
              <span className="scan-job-row__findings">
                {job.findingsCount} finding{job.findingsCount !== 1 ? 's' : ''}
              </span>
            </div>
            <div className="scan-job-row__meta">
              <span className="scan-job-row__date">{formatDate(job.createdAt)}</span>
              <span className="scan-job-row__actions">
                {(job.status === 'Running' || job.status === 'Pending') && (
                  <button
                    className="scan-btn scan-btn--small scan-btn--danger"
                    onClick={(e) => { e.stopPropagation(); onCancel(job.id) }}
                  >
                    Cancel
                  </button>
                )}
                {(job.status === 'Completed' || job.status === 'Failed' || job.status === 'Cancelled') && (
                  <button
                    className="scan-btn scan-btn--small scan-btn--ghost"
                    onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(job.id) }}
                  >
                    Delete
                  </button>
                )}
              </span>
            </div>
          </div>
        ))}
      </div>
    </>
  )
}
