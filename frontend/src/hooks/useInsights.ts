import { useCallback, useEffect, useRef, useState } from 'react'
import { getBulkInsights } from '../api/analytics'
import type { TrafficInsight } from '../types/analytics'

const DEBOUNCE_MS = 300

export function useInsights(captureIds: string[]) {
  const [insights, setInsights] = useState<Record<string, TrafficInsight>>({})
  const [loading, setLoading] = useState(false)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const idsKey = captureIds.sort().join(',')

  const fetch = useCallback(async (ids: string[]) => {
    if (ids.length === 0) return
    setLoading(true)
    try {
      const result = await getBulkInsights(ids)
      setInsights(prev => ({ ...prev, ...result }))
    } catch {
      // insights are best-effort; swallow errors
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => {
      const missing = captureIds.filter(id => !(id in insights))
      if (missing.length > 0) fetch(missing)
    }, DEBOUNCE_MS)
    return () => { if (timerRef.current) clearTimeout(timerRef.current) }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [idsKey])

  return { insights, loading }
}
