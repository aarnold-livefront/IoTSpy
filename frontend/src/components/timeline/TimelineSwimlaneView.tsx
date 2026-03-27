import { useMemo, useState, useRef, useCallback, useEffect } from 'react'
import type { CapturedRequestSummary, Device } from '../../types/api'
import '../../styles/timeline.css'

interface Props {
  captures: CapturedRequestSummary[]
  devices: Device[]
  selectedId: string | null
  onSelect: (id: string) => void
}

type ZoomLevel = '1m' | '5m' | '15m' | '30m' | '1h' | 'auto'

const ZOOM_DURATIONS: Record<Exclude<ZoomLevel, 'auto'>, number> = {
  '1m': 60_000,
  '5m': 300_000,
  '15m': 900_000,
  '30m': 1_800_000,
  '1h': 3_600_000,
}

export default function TimelineSwimlaneView({ captures, devices, selectedId, onSelect }: Props) {
  const [zoom, setZoom] = useState<ZoomLevel>('auto')
  const [tooltip, setTooltip] = useState<{ x: number; y: number; capture: CapturedRequestSummary } | null>(null)
  const canvasRef = useRef<HTMLDivElement>(null)
  const [labelsWidth, setLabelsWidth] = useState(180)
  const draggingLabels = useRef(false)

  useEffect(() => {
    function onMove(e: MouseEvent) {
      if (!draggingLabels.current) return
      setLabelsWidth(w => Math.max(100, Math.min(320, w + e.movementX)))
    }
    function onUp() { draggingLabels.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  // Group captures by device
  const { deviceMap, sortedDeviceIds, timeRange, canvasWidth } = useMemo(() => {
    const map = new Map<string, CapturedRequestSummary[]>()
    let minTs = Infinity
    let maxTs = -Infinity

    for (const cap of captures) {
      const key = cap.deviceId ?? '_unknown'
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(cap)

      const ts = new Date(cap.timestamp).getTime()
      if (ts < minTs) minTs = ts
      if (ts > maxTs) maxTs = ts
    }

    // Calculate time range
    let duration: number
    if (zoom === 'auto') {
      duration = maxTs > minTs ? maxTs - minTs + 10_000 : 60_000 // add 10s padding, min 1min
    } else {
      duration = ZOOM_DURATIONS[zoom]
    }

    const pxPerMs = 1.5 // pixels per millisecond at default zoom
    const width = Math.max(800, duration * pxPerMs)

    const sorted = Array.from(map.keys()).sort((a, b) => {
      if (a === '_unknown') return 1
      if (b === '_unknown') return -1
      return a.localeCompare(b)
    })

    return {
      deviceMap: map,
      sortedDeviceIds: sorted,
      timeRange: { start: minTs === Infinity ? Date.now() - 60_000 : minTs, end: minTs === Infinity ? Date.now() : minTs + duration },
      canvasWidth: width,
    }
  }, [captures, zoom])

  // Device info lookup
  const deviceInfo = useMemo(() => {
    const info = new Map<string, Device>()
    for (const d of devices) info.set(d.id, d)
    return info
  }, [devices])

  // Time axis ticks
  const ticks = useMemo(() => {
    const duration = timeRange.end - timeRange.start
    // Pick a good tick interval
    let interval: number
    if (duration <= 60_000) interval = 10_000
    else if (duration <= 300_000) interval = 30_000
    else if (duration <= 900_000) interval = 60_000
    else if (duration <= 3_600_000) interval = 300_000
    else interval = 600_000

    const result: { position: number; label: string }[] = []
    const firstTick = Math.ceil(timeRange.start / interval) * interval
    for (let t = firstTick; t <= timeRange.end; t += interval) {
      const pos = ((t - timeRange.start) / (timeRange.end - timeRange.start)) * canvasWidth
      const date = new Date(t)
      const label = `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
      result.push({ position: pos, label })
    }
    return result
  }, [timeRange, canvasWidth])

  const getEventPosition = useCallback(
    (timestamp: string, durationMs: number) => {
      const ts = new Date(timestamp).getTime()
      const totalDuration = timeRange.end - timeRange.start
      const left = ((ts - timeRange.start) / totalDuration) * canvasWidth
      const width = Math.max(4, (durationMs / totalDuration) * canvasWidth)
      return { left, width }
    },
    [timeRange, canvasWidth]
  )

  const getStatusClass = (statusCode: number, protocol: unknown): string => {
    const proto = typeof protocol === 'string' ? protocol.toLowerCase() : ''
    if (proto === 'mqtt' || proto === 'mqtttls') return 'protocol-mqtt'
    if (proto === 'dns' || proto === 'mdns') return 'protocol-dns'
    if (proto === 'coap') return 'protocol-coap'
    if (statusCode >= 500) return 'status-5xx'
    if (statusCode >= 400) return 'status-4xx'
    if (statusCode >= 300) return 'status-3xx'
    if (statusCode >= 200) return 'status-2xx'
    return 'status-0'
  }

  const handleMouseEnter = (e: React.MouseEvent, capture: CapturedRequestSummary) => {
    setTooltip({ x: e.clientX + 12, y: e.clientY - 10, capture })
  }

  const handleMouseLeave = () => setTooltip(null)

  if (captures.length === 0) {
    return (
      <div className="timeline-container">
        <div className="timeline-empty">No captures to display on the timeline.</div>
      </div>
    )
  }

  return (
    <div className="timeline-container">
      {/* Toolbar */}
      <div className="timeline-toolbar">
        <span style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-muted)' }}>Zoom:</span>
        {(['auto', '1m', '5m', '15m', '30m', '1h'] as ZoomLevel[]).map((z) => (
          <button key={z} className={zoom === z ? 'active' : ''} onClick={() => setZoom(z)}>
            {z === 'auto' ? 'Fit' : z}
          </button>
        ))}
        <span className="timeline-time-range">
          {new Date(timeRange.start).toLocaleTimeString()} — {new Date(timeRange.end).toLocaleTimeString()}
        </span>
      </div>

      {/* Body */}
      <div className="timeline-body">
        <div className="timeline-inner">
          {/* Device labels */}
          <div className="timeline-labels" style={{ width: labelsWidth, minWidth: labelsWidth }}>
            <div className="timeline-label-header">Device</div>
            {sortedDeviceIds.map((deviceId) => {
              const dev = deviceInfo.get(deviceId)
              return (
                <div key={deviceId} className="timeline-label-row">
                  <span className="label-name">{dev?.label || dev?.hostname || 'Unknown'}</span>
                  <span className="label-ip">{dev?.ipAddress ?? (deviceId === '_unknown' ? '—' : deviceId)}</span>
                </div>
              )
            })}
            <div
              className="timeline-labels-resize"
              onMouseDown={() => { draggingLabels.current = true }}
            />
          </div>

          {/* Timeline canvas */}
          <div className="timeline-canvas" ref={canvasRef} style={{ minWidth: canvasWidth }}>
            {/* Time axis */}
            <div className="timeline-axis">
              {ticks.map((tick) => (
                <span key={tick.position} className="timeline-axis-tick" style={{ left: tick.position }}>
                  {tick.label}
                </span>
              ))}
            </div>

            {/* Grid lines */}
            {ticks.map((tick) => (
              <div key={tick.position} className="timeline-gridline" style={{ left: tick.position }} />
            ))}

            {/* Swimlane rows */}
            {sortedDeviceIds.map((deviceId) => {
              const events = deviceMap.get(deviceId) ?? []
              return (
                <div key={deviceId} className="timeline-swimlane">
                  {events.map((cap) => {
                    const { left, width } = getEventPosition(cap.timestamp, cap.durationMs)
                    const className = [
                      'timeline-event',
                      getStatusClass(cap.statusCode, cap.protocol),
                      cap.id === selectedId ? 'selected' : '',
                    ]
                      .filter(Boolean)
                      .join(' ')

                    return (
                      <div
                        key={cap.id}
                        className={className}
                        style={{ left, width }}
                        onClick={() => onSelect(cap.id)}
                        onMouseEnter={(e) => handleMouseEnter(e, cap)}
                        onMouseLeave={handleMouseLeave}
                      >
                        {width > 40 ? `${cap.method} ${cap.statusCode}` : ''}
                      </div>
                    )
                  })}
                </div>
              )
            })}
          </div>
        </div>
      </div>

      {/* Tooltip */}
      {tooltip && (
        <div className="timeline-tooltip" style={{ left: tooltip.x, top: tooltip.y }}>
          <div className="tooltip-row">
            <span className="tooltip-label">Method:</span> {tooltip.capture.method}
          </div>
          <div className="tooltip-row">
            <span className="tooltip-label">Host:</span> {tooltip.capture.host}
            {tooltip.capture.path}
          </div>
          <div className="tooltip-row">
            <span className="tooltip-label">Status:</span> {tooltip.capture.statusCode} {tooltip.capture.statusMessage}
          </div>
          <div className="tooltip-row">
            <span className="tooltip-label">Duration:</span> {tooltip.capture.durationMs}ms
          </div>
          <div className="tooltip-row">
            <span className="tooltip-label">Time:</span> {new Date(tooltip.capture.timestamp).toLocaleTimeString()}
          </div>
        </div>
      )}
    </div>
  )
}
