import { useRef, useCallback, useEffect } from 'react'
import CaptureFilterBar from './CaptureFilterBar'
import CaptureRow from './CaptureRow'
import LoadingSpinner from '../common/LoadingSpinner'
import ErrorBanner from '../common/ErrorBanner'
import type { CaptureFilters, CapturedRequestSummary, Device } from '../../types/api'
import '../../styles/capture-list.css'

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
  const scrollRef = useRef<HTMLDivElement>(null)

  const handleScroll = useCallback(() => {
    const el = scrollRef.current
    if (!el || loadingMore || !hasMore) return
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 80) {
      onLoadMore()
    }
  }, [loadingMore, hasMore, onLoadMore])

  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    el.addEventListener('scroll', handleScroll, { passive: true })
    return () => el.removeEventListener('scroll', handleScroll)
  }, [handleScroll])

  return (
    <div className="capture-list-pane">
      <CaptureFilterBar devices={devices} filters={filters} onChange={onFiltersChange} />

      {error && <ErrorBanner message={error} />}

      <div className="capture-list__count">
        {total.toLocaleString()} capture{total !== 1 ? 's' : ''}
      </div>

      <div className="capture-list" ref={scrollRef} role="grid">
        {loading ? (
          <LoadingSpinner />
        ) : captures.length === 0 ? (
          <div className="capture-list__empty">No captures yet</div>
        ) : (
          <>
            {captures.map((c) => (
              <CaptureRow
                key={c.id}
                capture={c}
                selected={c.id === selectedId}
                onSelect={onSelect}
              />
            ))}
            {loadingMore && <LoadingSpinner />}
          </>
        )}
      </div>
    </div>
  )
}
