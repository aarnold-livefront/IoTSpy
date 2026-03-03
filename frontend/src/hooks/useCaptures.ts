import { useCallback, useEffect, useRef, useState } from 'react'
import { listCaptures } from '../api/captures'
import type { CaptureFilters, CapturedRequest, TrafficCaptureEvent } from '../types/api'

const PAGE_SIZE = 50

export function useCaptures(filters: CaptureFilters) {
  const [captures, setCaptures] = useState<CapturedRequest[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [pages, setPages] = useState(1)
  const [loading, setLoading] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Reset when filters change
  const filtersRef = useRef(filters)
  const filtersKey = JSON.stringify(filters)

  const fetchPage = useCallback(async (pageNum: number, replace: boolean) => {
    const setter = replace ? setLoading : setLoadingMore
    setter(true)
    setError(null)
    try {
      const res = await listCaptures({ ...filtersRef.current, page: pageNum, pageSize: PAGE_SIZE })
      if (replace) {
        setCaptures(res.items)
      } else {
        setCaptures((prev) => [...prev, ...res.items])
      }
      setTotal(res.total)
      setPage(res.page)
      setPages(res.pages)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch captures')
    } finally {
      setter(false)
    }
  }, [])

  // Re-fetch when filters change
  useEffect(() => {
    filtersRef.current = filters
    setPage(1)
    void fetchPage(1, true)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtersKey, fetchPage])

  const loadMore = useCallback(() => {
    if (!loadingMore && page < pages) {
      void fetchPage(page + 1, false)
    }
  }, [loadingMore, page, pages, fetchPage])

  /** Prepend a live SignalR capture to the top of the list */
  const prependCapture = useCallback((event: TrafficCaptureEvent) => {
    const partial: CapturedRequest = {
      ...event,
      requestHeaders: '',
      requestBody: '',
      responseHeaders: '',
      responseBody: '',
      tlsCipherSuite: '',
      isModified: false,
      notes: '',
      device: undefined,
    }
    setCaptures((prev) => [partial, ...prev])
    setTotal((t) => t + 1)
  }, [])

  return {
    captures,
    total,
    page,
    pages,
    loading,
    loadingMore,
    error,
    loadMore,
    prependCapture,
    hasMore: page < pages,
  }
}
