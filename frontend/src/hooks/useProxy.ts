import { useCallback, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getProxyStatus, startProxy, stopProxy, updateProxySettings } from '../api/proxy'
import type { ProxySettings, ProxySettingsUpdate, ProxyStatus } from '../types/api'

const QUERY_KEY = ['proxy-status']

export function useProxy() {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const { data: status = null } = useQuery({
    queryKey: QUERY_KEY,
    queryFn: getProxyStatus,
    refetchInterval: 3000,
    staleTime: 0,
  })

  const refresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: QUERY_KEY })
  }, [queryClient])

  const startMutation = useMutation({
    mutationFn: () => startProxy(),
    onSuccess: (res) => {
      queryClient.setQueryData<ProxyStatus>(QUERY_KEY, (prev) =>
        prev ? { ...prev, isRunning: res.isRunning, port: res.port } : prev,
      )
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : 'Failed to start proxy')
    },
  })

  const stopMutation = useMutation({
    mutationFn: () => stopProxy(),
    onSuccess: (res) => {
      queryClient.setQueryData<ProxyStatus>(QUERY_KEY, (prev) =>
        prev ? { ...prev, isRunning: res.isRunning } : prev,
      )
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : 'Failed to stop proxy')
    },
  })

  const saveSettingsMutation = useMutation({
    mutationFn: (update: ProxySettingsUpdate) => updateProxySettings(update),
    onSuccess: (saved) => {
      queryClient.setQueryData<ProxyStatus>(QUERY_KEY, (prev) =>
        prev ? { ...prev, settings: saved } : prev,
      )
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : 'Failed to save settings')
    },
  })

  const loading = startMutation.isPending || stopMutation.isPending

  const start = useCallback(async () => {
    setError(null)
    try {
      await startMutation.mutateAsync()
    } catch { /* handled in onError */ }
  }, [startMutation])

  const stop = useCallback(async () => {
    setError(null)
    try {
      await stopMutation.mutateAsync()
    } catch { /* handled in onError */ }
  }, [stopMutation])

  const saveSettings = useCallback(
    async (update: ProxySettingsUpdate): Promise<ProxySettings | null> => {
      setError(null)
      try {
        return await saveSettingsMutation.mutateAsync(update)
      } catch {
        return null
      }
    },
    [saveSettingsMutation],
  )

  return { status, loading, error, refresh, start, stop, saveSettings }
}
