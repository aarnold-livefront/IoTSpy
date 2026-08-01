import { useCallback, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { listLayouts, createLayout, updateLayout, deleteLayout } from '../api/dashboardLayouts'
import type { DashboardLayout } from '../types/api'

const KEY = ['dashboard-layouts']

export function useDashboardLayouts() {
  const queryClient = useQueryClient()
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data: layouts = [], isLoading: loading } = useQuery({ queryKey: KEY, queryFn: listLayouts })

  const setLayouts = useCallback((updater: (prev: DashboardLayout[]) => DashboardLayout[]) => {
    queryClient.setQueryData(KEY, (prev: DashboardLayout[] = []) => updater(prev))
  }, [queryClient])

  const save = useCallback(async (
    name: string,
    layoutJson: string,
    filtersJson: string,
    isDefault = false,
  ): Promise<DashboardLayout | null> => {
    setSaving(true)
    setError(null)
    try {
      const created = await createLayout(name, layoutJson, filtersJson)
      if (isDefault) {
        const updated = await updateLayout(created.id, { isDefault: true })
        setLayouts(prev => [updated, ...prev.map(l => ({ ...l, isDefault: false }))])
        return updated
      }
      setLayouts(prev => [created, ...prev])
      return created
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save layout')
      return null
    } finally {
      setSaving(false)
    }
  }, [setLayouts])

  const setDefault = useCallback(async (id: string) => {
    setError(null)
    try {
      const updated = await updateLayout(id, { isDefault: true })
      setLayouts(prev => prev.map(l => (l.id === id ? updated : { ...l, isDefault: false })))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to set default layout')
    }
  }, [setLayouts])

  const remove = useCallback(async (id: string) => {
    setError(null)
    try {
      await deleteLayout(id)
      setLayouts(prev => prev.filter(l => l.id !== id))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete layout')
    }
  }, [setLayouts])

  return { layouts, loading, saving, error, save, setDefault, remove }
}
