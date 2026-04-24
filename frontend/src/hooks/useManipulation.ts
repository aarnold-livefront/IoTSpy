import { useCallback, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  listRules,
  createRule,
  updateRule,
  deleteRule,
  listBreakpoints,
  createBreakpoint,
  updateBreakpoint,
  deleteBreakpoint,
  listReplays,
  createReplay,
  deleteReplay,
  listFuzzerJobs,
  startFuzzer,
  getFuzzerJob,
  getFuzzerResults,
  cancelFuzzerJob,
  deleteFuzzerJob,
} from '../api/manipulation'
import type {
  ManipulationRule,
  CreateManipulationRuleRequest,
  UpdateManipulationRuleRequest,
  Breakpoint,
  CreateBreakpointRequest,
  UpdateBreakpointRequest,
  ReplaySession,
  CreateReplayRequest,
  FuzzerJob,
  FuzzerResult,
  StartFuzzerRequest,
} from '../types/api'

const RULES_KEY = ['manipulation-rules']
const BP_KEY = ['manipulation-breakpoints']
const REPLAYS_KEY = ['manipulation-replays']
const FUZZER_KEY = ['fuzzer-jobs']

export function useManipulation() {
  const queryClient = useQueryClient()
  const [selectedFuzzerResults, setSelectedFuzzerResults] = useState<FuzzerResult[]>([])

  // ── Rules ──────────────────────────────────────────────────────────────────

  const {
    data: rules = [],
    isLoading: rulesLoading,
    error: rulesQueryError,
  } = useQuery({ queryKey: RULES_KEY, queryFn: listRules })

  const rulesError = rulesQueryError instanceof Error ? rulesQueryError.message : null

  const addRuleMutation = useMutation({
    mutationFn: (req: CreateManipulationRuleRequest) => createRule(req),
    onSuccess: (rule) => {
      queryClient.setQueryData<ManipulationRule[]>(RULES_KEY, (prev = []) => [...prev, rule])
    },
  })

  const editRuleMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateManipulationRuleRequest }) =>
      updateRule(id, req),
    onSuccess: (rule) => {
      queryClient.setQueryData<ManipulationRule[]>(RULES_KEY, (prev = []) =>
        prev.map((r) => (r.id === rule.id ? rule : r)),
      )
    },
  })

  const removeRuleMutation = useMutation({
    mutationFn: (id: string) => deleteRule(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<ManipulationRule[]>(RULES_KEY, (prev = []) =>
        prev.filter((r) => r.id !== id),
      )
    },
  })

  const addRule = useCallback(
    async (req: CreateManipulationRuleRequest) => {
      try {
        return await addRuleMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [addRuleMutation],
  )

  const editRule = useCallback(
    async (id: string, req: UpdateManipulationRuleRequest) => {
      try {
        return await editRuleMutation.mutateAsync({ id, req })
      } catch {
        return null
      }
    },
    [editRuleMutation],
  )

  const removeRule = useCallback(
    async (id: string) => {
      try {
        await removeRuleMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [removeRuleMutation],
  )

  // ── Breakpoints ────────────────────────────────────────────────────────────

  const {
    data: breakpoints = [],
    isLoading: breakpointsLoading,
    error: bpQueryError,
  } = useQuery({ queryKey: BP_KEY, queryFn: listBreakpoints })

  const breakpointsError = bpQueryError instanceof Error ? bpQueryError.message : null

  const addBpMutation = useMutation({
    mutationFn: (req: CreateBreakpointRequest) => createBreakpoint(req),
    onSuccess: (bp) => {
      queryClient.setQueryData<Breakpoint[]>(BP_KEY, (prev = []) => [...prev, bp])
    },
  })

  const editBpMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateBreakpointRequest }) =>
      updateBreakpoint(id, req),
    onSuccess: (bp) => {
      queryClient.setQueryData<Breakpoint[]>(BP_KEY, (prev = []) =>
        prev.map((b) => (b.id === bp.id ? bp : b)),
      )
    },
  })

  const removeBpMutation = useMutation({
    mutationFn: (id: string) => deleteBreakpoint(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<Breakpoint[]>(BP_KEY, (prev = []) =>
        prev.filter((b) => b.id !== id),
      )
    },
  })

  const addBreakpoint = useCallback(
    async (req: CreateBreakpointRequest) => {
      try {
        return await addBpMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [addBpMutation],
  )

  const editBreakpoint = useCallback(
    async (id: string, req: UpdateBreakpointRequest) => {
      try {
        return await editBpMutation.mutateAsync({ id, req })
      } catch {
        return null
      }
    },
    [editBpMutation],
  )

  const removeBreakpoint = useCallback(
    async (id: string) => {
      try {
        await removeBpMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [removeBpMutation],
  )

  // ── Replay ─────────────────────────────────────────────────────────────────

  const {
    data: replays = [],
    isLoading: replaysLoading,
    error: replaysQueryError,
  } = useQuery({ queryKey: REPLAYS_KEY, queryFn: listReplays })

  const replaysError = replaysQueryError instanceof Error ? replaysQueryError.message : null

  const replayMutation = useMutation({
    mutationFn: (req: CreateReplayRequest) => createReplay(req),
    onSuccess: (session) => {
      queryClient.setQueryData<ReplaySession[]>(REPLAYS_KEY, (prev = []) => [session, ...prev])
    },
  })

  const removeReplayMutation = useMutation({
    mutationFn: (id: string) => deleteReplay(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<ReplaySession[]>(REPLAYS_KEY, (prev = []) =>
        prev.filter((r) => r.id !== id),
      )
    },
  })

  const replay = useCallback(
    async (req: CreateReplayRequest) => {
      try {
        return await replayMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [replayMutation],
  )

  const removeReplay = useCallback(
    async (id: string) => {
      try {
        await removeReplayMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [removeReplayMutation],
  )

  // ── Fuzzer ─────────────────────────────────────────────────────────────────

  const {
    data: fuzzerJobs = [],
    isLoading: fuzzerLoading,
    error: fuzzerQueryError,
  } = useQuery({
    queryKey: FUZZER_KEY,
    queryFn: listFuzzerJobs,
    staleTime: 0,
    refetchInterval: (query) => {
      const data = query.state.data as FuzzerJob[] | undefined
      const hasRunning =
        Array.isArray(data) &&
        data.some((j) => j.status === 'Running' || j.status === 'Pending')
      return hasRunning ? 3000 : false
    },
  })

  const fuzzerError = fuzzerQueryError instanceof Error ? fuzzerQueryError.message : null

  const fuzzMutation = useMutation({
    mutationFn: (req: StartFuzzerRequest) => startFuzzer(req),
    onSuccess: (job) => {
      queryClient.setQueryData<FuzzerJob[]>(FUZZER_KEY, (prev = []) => [job, ...prev])
    },
  })

  const cancelFuzzerMutation = useMutation({
    mutationFn: (id: string) => cancelFuzzerJob(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: FUZZER_KEY }),
  })

  const removeFuzzerMutation = useMutation({
    mutationFn: (id: string) => deleteFuzzerJob(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<FuzzerJob[]>(FUZZER_KEY, (prev = []) =>
        prev.filter((j) => j.id !== id),
      )
    },
  })

  const fuzz = useCallback(
    async (req: StartFuzzerRequest) => {
      try {
        return await fuzzMutation.mutateAsync(req)
      } catch {
        return null
      }
    },
    [fuzzMutation],
  )

  const viewFuzzerResults = useCallback(async (id: string) => {
    try {
      const [job, results] = await Promise.all([getFuzzerJob(id), getFuzzerResults(id)])
      queryClient.setQueryData<FuzzerJob[]>(FUZZER_KEY, (prev = []) =>
        prev.map((j) => (j.id === id ? job : j)),
      )
      setSelectedFuzzerResults(results)
    } catch { /* swallow */ }
  }, [queryClient])

  const cancelFuzzer = useCallback(
    async (id: string) => {
      try {
        await cancelFuzzerMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [cancelFuzzerMutation],
  )

  const removeFuzzer = useCallback(
    async (id: string) => {
      try {
        await removeFuzzerMutation.mutateAsync(id)
      } catch { /* swallow */ }
    },
    [removeFuzzerMutation],
  )

  return {
    // Rules
    rules,
    rulesLoading,
    rulesError,
    addRule,
    editRule,
    removeRule,
    // Breakpoints
    breakpoints,
    breakpointsLoading,
    breakpointsError,
    addBreakpoint,
    editBreakpoint,
    removeBreakpoint,
    // Replay
    replays,
    replaysLoading,
    replaysError,
    replay,
    removeReplay,
    // Fuzzer
    fuzzerJobs,
    selectedFuzzerResults,
    fuzzerLoading,
    fuzzerError,
    fuzz,
    viewFuzzerResults,
    cancelFuzzer,
    removeFuzzer,
  }
}
