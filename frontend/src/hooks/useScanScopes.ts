import { useCallback, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { listScopes, createScope, toggleScope, deleteScope } from '../api/scanScopes'

const KEY = ['scan-scopes']

export function useScanScopes() {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const { data, isLoading: loading } = useQuery({
    queryKey: KEY,
    queryFn: listScopes,
  })

  const scopes = data?.items ?? []

  const add = useCallback(async (name: string, cidr: string) => {
    setSaving(true)
    setError(null)
    try {
      const created = await createScope(name, cidr)
      queryClient.setQueryData(KEY, (prev: typeof data) => ({
        items: [created, ...(prev?.items ?? [])],
        total: (prev?.total ?? 0) + 1,
      }))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create scope')
    } finally {
      setSaving(false)
    }
  }, [queryClient])

  const toggle = useCallback(async (id: string) => {
    setError(null)
    try {
      const updated = await toggleScope(id)
      queryClient.setQueryData(KEY, (prev: typeof data) => ({
        ...prev,
        items: (prev?.items ?? []).map(s => s.id === id ? updated : s),
      }))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to toggle scope')
    }
  }, [queryClient])

  const remove = useCallback(async (id: string) => {
    setError(null)
    try {
      await deleteScope(id)
      queryClient.setQueryData(KEY, (prev: typeof data) => ({
        items: (prev?.items ?? []).filter(s => s.id !== id),
        total: Math.max(0, (prev?.total ?? 0) - 1),
      }))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete scope')
    }
  }, [queryClient])

  return { scopes, loading, saving, error, add, toggle, remove }
}
