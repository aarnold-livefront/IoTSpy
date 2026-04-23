import { useCallback, useEffect, useState } from 'react'
import {
  createContentRule,
  deleteContentRule,
  listContentRules,
  updateContentRule,
  type CreateContentRuleRequest,
  type UpdateContentRuleRequest,
} from '../api/contentrules'
import type { ContentReplacementRule } from '../types/api'

export function useContentRules(host?: string) {
  const [rules, setRules] = useState<ContentReplacementRule[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setRules(await listContentRules(host || undefined))
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [host])

  useEffect(() => { void refresh() }, [refresh])

  const addRule = useCallback(async (_specId: string, req: CreateContentRuleRequest) => {
    try {
      const created = await createContentRule(req)
      setRules((prev) => [...prev, created])
      return created
    } catch (e) {
      setError((e as Error).message)
      return null
    }
  }, [])

  const editRule = useCallback(async (_specId: string, id: string, req: UpdateContentRuleRequest) => {
    try {
      const updated = await updateContentRule(id, req)
      setRules((prev) => prev.map((r) => r.id === id ? updated : r))
      return updated
    } catch (e) {
      setError((e as Error).message)
      return null
    }
  }, [])

  const removeRule = useCallback(async (_specId: string, id: string) => {
    try {
      await deleteContentRule(id)
      setRules((prev) => prev.filter((r) => r.id !== id))
    } catch (e) {
      setError((e as Error).message)
    }
  }, [])

  return { rules, loading, error, refresh, addRule, editRule, removeRule }
}
