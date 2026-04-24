import { useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
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
  const queryClient = useQueryClient()
  const queryKey = ['content-rules', host ?? '']

  const { data: rules = [], isLoading: loading, error: queryError, refetch } = useQuery({
    queryKey,
    queryFn: () => listContentRules(host || undefined),
  })

  const refresh = useCallback(() => { void refetch() }, [refetch])

  const addMutation = useMutation({
    mutationFn: (req: CreateContentRuleRequest) => createContentRule(req),
    onSuccess: (created) => {
      queryClient.setQueryData<ContentReplacementRule[]>(queryKey, (prev = []) => [
        ...prev,
        created,
      ])
    },
  })

  const editMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateContentRuleRequest }) =>
      updateContentRule(id, req),
    onSuccess: (updated) => {
      queryClient.setQueryData<ContentReplacementRule[]>(queryKey, (prev = []) =>
        prev.map((r) => (r.id === updated.id ? updated : r)),
      )
    },
  })

  const removeMutation = useMutation({
    mutationFn: (id: string) => deleteContentRule(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<ContentReplacementRule[]>(queryKey, (prev = []) =>
        prev.filter((r) => r.id !== id),
      )
    },
  })

  const addRule = useCallback(
    async (_specId: string, req: CreateContentRuleRequest) => {
      try {
        return await addMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [addMutation],
  )

  const editRule = useCallback(
    async (_specId: string, id: string, req: UpdateContentRuleRequest) => {
      try {
        return await editMutation.mutateAsync({ id, req })
      } catch {
        return null
      }
    },
    [editMutation],
  )

  const removeRule = useCallback(
    async (_specId: string, id: string) => {
      try {
        await removeMutation.mutateAsync(id)
      } catch { /* caller can check error state */ }
    },
    [removeMutation],
  )

  return {
    rules,
    loading,
    error: queryError instanceof Error ? queryError.message : null,
    refresh,
    addRule,
    editRule,
    removeRule,
  }
}
