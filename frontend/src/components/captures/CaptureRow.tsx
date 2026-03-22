import type { CapturedRequestSummary } from '../../types/api'
import '../../styles/capture-list.css'

interface Props {
  capture: CapturedRequestSummary
  selected: boolean
  onSelect: (id: string) => void
}

function methodBadgeClass(method: string): string {
  switch (method.toUpperCase()) {
    case 'GET': return 'method-badge method-badge--GET'
    case 'POST': return 'method-badge method-badge--POST'
    case 'PUT': return 'method-badge method-badge--PUT'
    case 'DELETE': return 'method-badge method-badge--DELETE'
    case 'PATCH': return 'method-badge method-badge--PATCH'
    default: return 'method-badge method-badge--OTHER'
  }
}

function statusBadgeClass(code: number): string {
  if (code === 0) return 'status-badge status-badge--0'
  if (code < 300) return 'status-badge status-badge--2xx'
  if (code < 400) return 'status-badge status-badge--3xx'
  if (code < 500) return 'status-badge status-badge--4xx'
  return 'status-badge status-badge--5xx'
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(1)}s`
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes}b`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}k`
  return `${(bytes / (1024 * 1024)).toFixed(1)}M`
}

export default function CaptureRow({ capture, selected, onSelect }: Props) {
  const { id, method, host, path, statusCode, durationMs, isTls, requestBodySize } = capture
  const displayPath = path || '/'

  return (
    <div
      className={`capture-row${selected ? ' capture-row--selected' : ''}${isTls ? ' capture-row--tls' : ''}`}
      onClick={() => onSelect(id)}
      role="row"
      aria-selected={selected}
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onSelect(id)}
    >
      <span className={methodBadgeClass(method)}>{method}</span>
      <span className={statusBadgeClass(statusCode)}>{statusCode || '—'}</span>
      <span className="capture-row__host-path">
        <span className="capture-row__host">{host}</span>
        <span className="capture-row__path">{displayPath}</span>
      </span>
      <span className="capture-row__duration">
        {durationMs > 0 ? formatDuration(durationMs) : ''}
        {requestBodySize > 0 && (
          <span className="capture-row__body-size" title="Request body size">
            {' '}&#8593; {formatBytes(requestBodySize)}
          </span>
        )}
      </span>
      <span className="capture-row__tls-indicator">{isTls ? '🔒' : ''}</span>
    </div>
  )
}
