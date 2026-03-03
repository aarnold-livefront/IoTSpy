import { useCallback, useEffect, useState } from 'react'
import { listDevices } from '../api/devices'
import type { Device } from '../types/api'

export function useDevices() {
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await listDevices()
      setDevices(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch devices')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return { devices, loading, error, refresh }
}
