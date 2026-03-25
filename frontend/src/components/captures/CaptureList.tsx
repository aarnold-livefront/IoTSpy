import { useRef, useCallback, useEffect, useState } from 'react'
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
  const prevFirstIdRef = useRef<string | null>(null)
  const seededRef = useRef(false)
  const [newId, setNewId] = useState<string | null>(null)

  useEffect(() => {
    const currentFirstId = captures[0]?.id ?? null
    if (!seededRef.current) {
      // Seed on first non-empty load — don't flash, it's not a live arrival
      if (currentFirstId) seededRef.current = true
      prevFirstIdRef.current = currentFirstId
      return
    }
    if (currentFirstId && currentFirstId !== prevFirstIdRef.current) {
      prevFirstIdRef.current = currentFirstId
      setNewId(currentFirstId)
      const timer = setTimeout(() => setNewId(null), 1400)
      return () => clearTimeout(timer)
    } else {
      prevFirstIdRef.current = currentFirstId
    }
  }, [captures])

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
          <>
            {captures.map((c) => (
              <CaptureRow
                key={c.id}
                capture={c}
                selected={c.id === selectedId}
                isNew={c.id === newId}
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
