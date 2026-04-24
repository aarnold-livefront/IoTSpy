import { useCallback, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  listSpecs,
  getSpec,
  generateSpec,
  importSpec,
  exportSpec,
  updateSpec,
  deleteSpec,
  refineSpec,
  activateSpec,
  deactivateSpec,
  createRule,
  updateRule,
  deleteRule,
} from '../api/apispec'
import type {
  ApiSpecDocument,
  GenerateSpecRequest,
  ImportSpecRequest,
  UpdateSpecRequest,
  CreateReplacementRuleRequest,
  UpdateReplacementRuleRequest,
} from '../types/api'

const SPECS_KEY = ['api-specs']

export function useApiSpec() {
  const queryClient = useQueryClient()
  const [selectedSpec, setSelectedSpec] = useState<ApiSpecDocument | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data: specs = [], isLoading: loading, refetch } = useQuery({
    queryKey: SPECS_KEY,
    queryFn: listSpecs,
  })

  const refresh = useCallback(() => { void refetch() }, [refetch])

  const select = useCallback(async (id: string) => {
    setError(null)
    try {
      const doc = await getSpec(id)
      setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch spec')
      return null
    }
  }, [])

  const generateMutation = useMutation({
    mutationFn: (req: GenerateSpecRequest) => generateSpec(req),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) => [doc, ...prev])
      setSelectedSpec(doc)
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to generate spec'),
  })

  const importSpecMutation = useMutation({
    mutationFn: (req: ImportSpecRequest) => importSpec(req),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) => [doc, ...prev])
      setSelectedSpec(doc)
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to import spec'),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateSpecRequest }) => updateSpec(id, req),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) =>
        prev.map((s) => (s.id === doc.id ? doc : s)),
      )
      setSelectedSpec((prev) => (prev?.id === doc.id ? doc : prev))
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to update spec'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteSpec(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) =>
        prev.filter((s) => s.id !== id),
      )
      setSelectedSpec((prev) => (prev?.id === id ? null : prev))
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to delete spec'),
  })

  const refineMutation = useMutation({
    mutationFn: (id: string) => refineSpec(id),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) =>
        prev.map((s) => (s.id === doc.id ? doc : s)),
      )
      setSelectedSpec((prev) => (prev?.id === doc.id ? doc : prev))
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to refine spec'),
  })

  const activateMutation = useMutation({
    mutationFn: (id: string) => activateSpec(id),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) =>
        prev.map((s) => (s.id === doc.id ? doc : s)),
      )
      setSelectedSpec((prev) => (prev?.id === doc.id ? doc : prev))
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to activate spec'),
  })

  const deactivateMutation = useMutation({
    mutationFn: (id: string) => deactivateSpec(id),
    onSuccess: (doc) => {
      queryClient.setQueryData<ApiSpecDocument[]>(SPECS_KEY, (prev = []) =>
        prev.map((s) => (s.id === doc.id ? doc : s)),
      )
      setSelectedSpec((prev) => (prev?.id === doc.id ? doc : prev))
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to deactivate spec'),
  })

  const generate = useCallback(
    async (req: GenerateSpecRequest) => {
      setError(null)
      try {
        return await generateMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [generateMutation],
  )

  const importSpecFn = useCallback(
    async (req: ImportSpecRequest) => {
      setError(null)
      try {
        return await importSpecMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [importSpecMutation],
  )

  const exportSpecFn = useCallback(async (id: string) => {
    setError(null)
    try {
      return await exportSpec(id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to export spec')
      return null
    }
  }, [])

  const update = useCallback(
    async (id: string, req: UpdateSpecRequest) => {
      setError(null)
      try {
        return await updateMutation.mutateAsync({ id, req })
      } catch {
        return null
      }
    },
    [updateMutation],
  )

  const remove = useCallback(
    async (id: string) => {
      setError(null)
      try {
        await deleteMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [deleteMutation],
  )

  const refine = useCallback(
    async (id: string) => {
      setError(null)
      try {
        return await refineMutation.mutateAsync(id)
      } catch {
        return null
      }
    },
    [refineMutation],
  )

  const activate = useCallback(
    async (id: string) => {
      setError(null)
      try {
        return await activateMutation.mutateAsync(id)
      } catch {
        return null
      }
    },
    [activateMutation],
  )

  const deactivate = useCallback(
    async (id: string) => {
      setError(null)
      try {
        return await deactivateMutation.mutateAsync(id)
      } catch {
        return null
      }
    },
    [deactivateMutation],
  )

  // ── Replacement rules ───────────────────────────────────────────────────────

  const addRuleMutation = useMutation({
    mutationFn: ({ specId, req }: { specId: string; req: CreateReplacementRuleRequest }) =>
      createRule(specId, req),
    onSuccess: (rule) => {
      setSelectedSpec((prev) =>
        prev ? { ...prev, replacementRules: [...prev.replacementRules, rule] } : prev,
      )
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to add rule'),
  })

  const editRuleMutation = useMutation({
    mutationFn: ({
      specId,
      ruleId,
      req,
    }: {
      specId: string
      ruleId: string
      req: UpdateReplacementRuleRequest
    }) => updateRule(specId, ruleId, req),
    onSuccess: (rule) => {
      setSelectedSpec((prev) =>
        prev
          ? {
              ...prev,
              replacementRules: prev.replacementRules.map((r) => (r.id === rule.id ? rule : r)),
            }
          : prev,
      )
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to update rule'),
  })

  const removeRuleMutation = useMutation({
    mutationFn: ({ specId, ruleId }: { specId: string; ruleId: string }) =>
      deleteRule(specId, ruleId),
    onSuccess: (_data, { ruleId }) => {
      setSelectedSpec((prev) =>
        prev
          ? {
              ...prev,
              replacementRules: prev.replacementRules.filter((r) => r.id !== ruleId),
            }
          : prev,
      )
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Failed to delete rule'),
  })

  const addRule = useCallback(
    async (specId: string, req: CreateReplacementRuleRequest) => {
      setError(null)
      try {
        return await addRuleMutation.mutateAsync({ specId, req })
      } catch {
        return null
      }
    },
    [addRuleMutation],
  )

  const editRule = useCallback(
    async (specId: string, ruleId: string, req: UpdateReplacementRuleRequest) => {
      setError(null)
      try {
        return await editRuleMutation.mutateAsync({ specId, ruleId, req })
      } catch {
        return null
      }
    },
    [editRuleMutation],
  )

  const removeRule = useCallback(
    async (specId: string, ruleId: string) => {
      setError(null)
      try {
        await removeRuleMutation.mutateAsync({ specId, ruleId })
      } catch { /* swallow */ }
    },
    [removeRuleMutation],
  )

  return {
    specs,
    selectedSpec,
    loading,
    error,
    refresh,
    select,
    generate,
    importSpec: importSpecFn,
    exportSpec: exportSpecFn,
    update,
    remove,
    refine,
    activate,
    deactivate,
    addRule,
    editRule,
    removeRule,
  }
}
