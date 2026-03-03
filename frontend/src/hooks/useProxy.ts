import { useCallback, useEffect, useState } from 'react'
import {
  getProxyStatus,
  startProxy,
  stopProxy,
  updateProxySettings,
} from '../api/proxy'
import type { ProxySettings, ProxySettingsUpdate, ProxyStatus } from '../types/api'

export function useProxy() {
  const [status, setStatus] = useState<ProxyStatus | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      const s = await getProxyStatus()
      setStatus(s)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch proxy status')
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const start = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await startProxy()
      setStatus((prev) =>
        prev ? { ...prev, isRunning: res.isRunning, port: res.port } : null,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start proxy')
    } finally {
      setLoading(false)
    }
  }, [])

  const stop = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await stopProxy()
      setStatus((prev) =>
        prev ? { ...prev, isRunning: res.isRunning } : null,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop proxy')
    } finally {
      setLoading(false)
    }
  }, [])

  const saveSettings = useCallback(async (update: ProxySettingsUpdate): Promise<ProxySettings | null> => {
    setError(null)
    try {
      const saved = await updateProxySettings(update)
      setStatus((prev) => (prev ? { ...prev, settings: saved } : null))
      return saved
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save settings')
      return null
    }
  }, [])

  return { status, loading, error, refresh, start, stop, saveSettings }
}
