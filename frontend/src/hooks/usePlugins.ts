import { useCallback, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { listPlugins, reloadPlugins } from '../api/plugins'

const KEY = ['plugins']

export function usePlugins() {
  const queryClient = useQueryClient()
  const [reloading, setReloading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data: plugins = [], isLoading: loading } = useQuery({ queryKey: KEY, queryFn: listPlugins })

  const reload = useCallback(async () => {
    setReloading(true)
    setError(null)
    try {
      const result = await reloadPlugins()
      queryClient.setQueryData(KEY, result.plugins)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Reload failed')
    } finally {
      setReloading(false)
    }
  }, [queryClient])

  return { plugins, loading, reloading, error, reload }
}
