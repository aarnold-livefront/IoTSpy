import { useRef, useCallback, useEffect, useState, useMemo, memo } from 'react'
import { FixedSizeList } from 'react-window'
import type { ListOnItemsRenderedProps, ListChildComponentProps } from 'react-window'
import { AutoSizer } from 'react-virtualized-auto-sizer'
import CaptureFilterBar from './CaptureFilterBar'
import CaptureRow from './CaptureRow'
import LoadingSpinner from '../common/LoadingSpinner'
import ErrorBanner from '../common/ErrorBanner'
import { exportCaptures } from '../../api/captures'
import type { CaptureFilters, CapturedRequestSummary, Device } from '../../types/api'
import '../../styles/capture-list.css'

const ROW_HEIGHT = 40

interface RowData {
  captures: CapturedRequestSummary[]
  selectedId: string | null
  newId: string | null
  onSelect: (id: string) => void
}

const VirtualRow = memo(function VirtualRow({ index, style, data }: ListChildComponentProps<RowData>) {
  const { captures, selectedId, newId, onSelect } = data
  if (index >= captures.length) {
    return <div style={style} className="capture-list__loading-row"><LoadingSpinner /></div>
  }
  const c = captures[index]
  return (
    <CaptureRow
      capture={c}
      selected={c.id === selectedId}
      isNew={c.id === newId}
      isEven={index % 2 === 0}
      onSelect={onSelect}
      style={style}
    />
  )
})

interface Props {
  captures: CapturedRequestSummary[]
  total: number
  loading: boolean
  loadingMore: boolean
  error: string | null
  hasMore: boolean
  filters: CaptureFilters
  devices: Device[]
  selectedId: string | null
  onSelect: (id: string) => void
  onFiltersChange: (filters: CaptureFilters) => void
  onLoadMore: () => void
}

export default function CaptureList({
  captures,
  total,
  loading,
  loadingMore,
  error,
  hasMore,
  filters,
  devices,
  selectedId,
  onSelect,
  onFiltersChange,
  onLoadMore,
}: Props) {
  const listRef = useRef<FixedSizeList>(null)
  const prevFirstIdRef = useRef<string | null>(null)
  const seededRef = useRef(false)
  const [newId, setNewId] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)
  const [exportOpen, setExportOpen] = useState(false)
  const exportRef = useRef<HTMLDivElement>(null)

  // Detect new prepended captures, flash them, and scroll to top
  useEffect(() => {
    const currentFirstId = captures[0]?.id ?? null
    if (!seededRef.current) {
      if (currentFirstId) seededRef.current = true
      prevFirstIdRef.current = currentFirstId
      return
    }
    if (currentFirstId && currentFirstId !== prevFirstIdRef.current) {
      prevFirstIdRef.current = currentFirstId
      setNewId(currentFirstId)
      listRef.current?.scrollToItem(0, 'start')
      const timer = setTimeout(() => setNewId(null), 1400)
      return () => clearTimeout(timer)
    } else {
      prevFirstIdRef.current = currentFirstId
    }
  }, [captures])

  // Trigger load-more when nearing the bottom of the visible window
  const handleItemsRendered = useCallback(({ visibleStopIndex }: ListOnItemsRenderedProps) => {
    if (hasMore && !loadingMore && visibleStopIndex >= captures.length - 10) {
      onLoadMore()
    }
  }, [captures.length, hasMore, loadingMore, onLoadMore])

  // Close export dropdown when clicking outside
  useEffect(() => {
    if (!exportOpen) return
    const handle = (e: MouseEvent) => {
      if (exportRef.current && !exportRef.current.contains(e.target as Node)) {
        setExportOpen(false)
      }
    }
    document.addEventListener('mousedown', handle)
    return () => document.removeEventListener('mousedown', handle)
  }, [exportOpen])

  const handleExport = async (format: 'csv' | 'json' | 'har') => {
    setExportOpen(false)
    setExporting(true)
    try {
      await exportCaptures(format, filters)
    } catch {
      // silent — download errors are rare and recoverable by the user retrying
    } finally {
      setExporting(false)
    }
  }

  const itemData = useMemo<RowData>(() => ({
    captures,
    selectedId,
    newId,
    onSelect,
  }), [captures, selectedId, newId, onSelect])

  const itemCount = captures.length + (loadingMore ? 1 : 0)

  return (
    <div className="capture-list-pane">
      <CaptureFilterBar devices={devices} filters={filters} onChange={onFiltersChange} />

      {error && <ErrorBanner message={error} />}

      <div className="capture-list__toolbar">
        <div className="capture-list__count">
          {total.toLocaleString()} capture{total !== 1 ? 's' : ''}
        </div>
        <div className="capture-list__export" ref={exportRef}>
          <button
            className="capture-list__export-btn"
            onClick={() => setExportOpen((o) => !o)}
            disabled={exporting || total === 0}
            title="Export captures"
          >
            {exporting ? 'Exporting…' : 'Export ▾'}
          </button>
          {exportOpen && (
            <div className="capture-list__export-menu" role="menu">
              {(['csv', 'json', 'har'] as const).map((fmt) => (
                <button
                  key={fmt}
                  className="capture-list__export-item"
                  role="menuitem"
                  onClick={() => void handleExport(fmt)}
                >
                  {fmt.toUpperCase()}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="capture-list" role="grid">
        {loading ? (
          <LoadingSpinner />
        ) : captures.length === 0 ? (
          <div className="capture-list__empty">
            <svg className="capture-list__empty-icon" width="48" height="48" viewBox="0 0 48 48" fill="none" aria-hidden="true">
              <circle cx="20" cy="21" r="11" stroke="currentColor" strokeWidth="2"/>
              <line x1="28.5" y1="29.5" x2="41" y2="42" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
              <circle cx="20" cy="21" r="4.5" stroke="currentColor" strokeWidth="1.5" opacity="0.4"/>
              <circle cx="20" cy="21" r="1.5" fill="currentColor" opacity="0.5"/>
            </svg>
            <div>No captures yet</div>
            <div className="capture-list__empty-hint">
              Configure your device to use the proxy, then traffic will appear here in real time.
            </div>
          </div>
        ) : (
          <AutoSizer renderProp={({ height, width }: { height: number | undefined; width: number | undefined }) =>
            height == null || width == null ? null : (
              <FixedSizeList
                ref={listRef}
                height={height}
                width={width}
                itemCount={itemCount}
                itemSize={ROW_HEIGHT}
                itemData={itemData}
                onItemsRendered={handleItemsRendered}
                overscanCount={5}
              >
                {VirtualRow}
              </FixedSizeList>
            )
          } />
        )}
      </div>
    </div>
  )
}
