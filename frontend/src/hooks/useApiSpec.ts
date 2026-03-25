import { useCallback, useEffect, useState } from 'react'
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

export function useApiSpec() {
  const [specs, setSpecs] = useState<ApiSpecDocument[]>([])
  const [selectedSpec, setSelectedSpec] = useState<ApiSpecDocument | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await listSpecs()
      setSpecs(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch specs')
    } finally {
      setLoading(false)
    }
  }, [])

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

  const generate = useCallback(async (req: GenerateSpecRequest) => {
    setError(null)
    try {
      const doc = await generateSpec(req)
      setSpecs((prev) => [doc, ...prev])
      setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to generate spec')
      return null
    }
  }, [])

  const importSpecFn = useCallback(async (req: ImportSpecRequest) => {
    setError(null)
    try {
      const doc = await importSpec(req)
      setSpecs((prev) => [doc, ...prev])
      setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to import spec')
      return null
    }
  }, [])

  const exportSpecFn = useCallback(async (id: string) => {
    setError(null)
    try {
      return await exportSpec(id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to export spec')
      return null
    }
  }, [])

  const update = useCallback(async (id: string, req: UpdateSpecRequest) => {
    setError(null)
    try {
      const doc = await updateSpec(id, req)
      setSpecs((prev) => prev.map((s) => (s.id === id ? doc : s)))
      if (selectedSpec?.id === id) setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update spec')
      return null
    }
  }, [selectedSpec?.id])

  const remove = useCallback(async (id: string) => {
    setError(null)
    try {
      await deleteSpec(id)
      setSpecs((prev) => prev.filter((s) => s.id !== id))
      if (selectedSpec?.id === id) setSelectedSpec(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete spec')
    }
  }, [selectedSpec?.id])

  const refine = useCallback(async (id: string) => {
    setError(null)
    try {
      const doc = await refineSpec(id)
      setSpecs((prev) => prev.map((s) => (s.id === id ? doc : s)))
      if (selectedSpec?.id === id) setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to refine spec')
      return null
    }
  }, [selectedSpec?.id])

  const activate = useCallback(async (id: string) => {
    setError(null)
    try {
      const doc = await activateSpec(id)
      setSpecs((prev) => prev.map((s) => (s.id === id ? doc : s)))
      if (selectedSpec?.id === id) setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to activate spec')
      return null
    }
  }, [selectedSpec?.id])

  const deactivate = useCallback(async (id: string) => {
    setError(null)
    try {
      const doc = await deactivateSpec(id)
      setSpecs((prev) => prev.map((s) => (s.id === id ? doc : s)))
      if (selectedSpec?.id === id) setSelectedSpec(doc)
      return doc
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to deactivate spec')
      return null
    }
  }, [selectedSpec?.id])

  // ── Replacement rules ───────────────────────────────────────────────────

  const addRule = useCallback(async (specId: string, req: CreateReplacementRuleRequest) => {
    setError(null)
    try {
      const rule = await createRule(specId, req)
      setSelectedSpec((prev) =>
        prev ? { ...prev, replacementRules: [...prev.replacementRules, rule] } : prev,
      )
      return rule
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add rule')
      return null
    }
  }, [])

  const editRule = useCallback(async (specId: string, ruleId: string, req: UpdateReplacementRuleRequest) => {
    setError(null)
    try {
      const rule = await updateRule(specId, ruleId, req)
      setSelectedSpec((prev) =>
        prev
          ? {
              ...prev,
              replacementRules: prev.replacementRules.map((r) => (r.id === ruleId ? rule : r)),
            }
          : prev,
      )
      return rule
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update rule')
      return null
    }
  }, [])

  const removeRule = useCallback(async (specId: string, ruleId: string) => {
    setError(null)
    try {
      await deleteRule(specId, ruleId)
      setSelectedSpec((prev) =>
        prev
          ? {
              ...prev,
              replacementRules: prev.replacementRules.filter((r) => r.id !== ruleId),
            }
          : prev,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete rule')
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

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
