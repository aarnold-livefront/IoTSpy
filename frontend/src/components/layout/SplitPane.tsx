import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import '../../styles/layout.css'

interface Props {
  left: ReactNode
  right: ReactNode
  initialLeftPercent?: number
  minLeftPx?: number
  minRightPx?: number
  vertical?: boolean
}

export default function SplitPane({
  left,
  right,
  initialLeftPercent = 40,
  minLeftPx = 200,
  minRightPx = 200,
  vertical = false,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [leftSize, setLeftSize] = useState<number | null>(null)
  const dragging = useRef(false)
  const [isDragging, setIsDragging] = useState(false)

  const onMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault()
    dragging.current = true
    setIsDragging(true)
  }, [])

  useEffect(() => {
    function onMouseMove(e: MouseEvent) {
      if (!dragging.current || !containerRef.current) return
      const rect = containerRef.current.getBoundingClientRect()
      const totalSize = vertical ? rect.height : rect.width
      const offset = vertical ? e.clientY - rect.top : e.clientX - rect.left
      const clamped = Math.max(minLeftPx, Math.min(totalSize - minRightPx, offset))
      setLeftSize(clamped)
    }

    function onMouseUp() {
      if (dragging.current) {
        dragging.current = false
        setIsDragging(false)
      }
    }

    window.addEventListener('mousemove', onMouseMove)
    window.addEventListener('mouseup', onMouseUp)
    return () => {
      window.removeEventListener('mousemove', onMouseMove)
      window.removeEventListener('mouseup', onMouseUp)
    }
  }, [minLeftPx, minRightPx, vertical])

  const leftStyle = leftSize != null
    ? { flexBasis: `${leftSize}px`, flexGrow: 0, flexShrink: 0 }
    : { flexBasis: `${initialLeftPercent}%`, flexGrow: 0, flexShrink: 0 }

  return (
    <div
      ref={containerRef}
      className={`split-pane${vertical ? ' split-pane--vertical' : ''}${isDragging ? ' split-pane--dragging' : ''}`}
      style={{ userSelect: isDragging ? 'none' : undefined }}
    >
      <div className="split-pane__panel" style={leftStyle}>
        {left}
      </div>
      <div
        className={`split-pane__divider${isDragging ? ' split-pane__divider--dragging' : ''}`}
        onMouseDown={onMouseDown}
      />
      <div className="split-pane__panel" style={{ flex: 1 }}>
        {right}
      </div>
    </div>
  )
}
