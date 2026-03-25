import { memo } from 'react'
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

function formatTime(timestamp: string): string {
  const d = new Date(timestamp)
  return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function protocolLabel(protocol: string, isTls: boolean): string | null {
  switch (protocol) {
    case 'Https': return null // TLS lock covers this
    case 'Http': return null
    case 'Mqtt':
    case 'MqttTls': return 'MQTT'
    case 'CoAP': return 'CoAP'
    case 'Dns':
    case 'MDns': return 'DNS'
    case 'WebSocket':
    case 'WebSocketTls': return 'WS'
    case 'Grpc': return 'gRPC'
    case 'Modbus': return 'Modbus'
    case 'TlsPassthrough': return 'TLS'
    default: return isTls ? null : protocol !== 'Other' ? protocol : null
  }
}

export default memo(function CaptureRow({ capture, selected, onSelect }: Props) {
  const { id, method, host, path, statusCode, durationMs, isTls, requestBodySize, protocol, timestamp } = capture
  const displayPath = path || '/'
  const proto = protocolLabel(protocol, isTls)

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
      <span className="capture-row__timing">
        {durationMs > 0 ? formatDuration(durationMs) : ''}
        {requestBodySize > 0 && (
          <span className="capture-row__body-size" title="Request body size">
            {' '}&uarr; {formatBytes(requestBodySize)}
          </span>
        )}
      </span>
      <span className="capture-row__indicators">
        {isTls && <span className="capture-row__tls-lock" title="TLS encrypted">&#x1F512;</span>}
        {proto && <span className="capture-row__protocol-tag" title={`Protocol: ${protocol}`}>{proto}</span>}
      </span>
      <span className="capture-row__timestamp" title={new Date(timestamp).toLocaleString()}>
        {formatTime(timestamp)}
      </span>
    </div>
  )
})
