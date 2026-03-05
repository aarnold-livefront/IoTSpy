import { useCallback, useEffect, useRef, useState } from 'react'
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

export function useManipulation() {
  // ── Rules ──────────────────────────────────────────────────────────────────
  const [rules, setRules] = useState<ManipulationRule[]>([])
  const [rulesLoading, setRulesLoading] = useState(false)
  const [rulesError, setRulesError] = useState<string | null>(null)

  const refreshRules = useCallback(async () => {
    setRulesLoading(true)
    setRulesError(null)
    try {
      const data = await listRules()
      setRules(data)
    } catch (err) {
      setRulesError(err instanceof Error ? err.message : 'Failed to fetch rules')
    } finally {
      setRulesLoading(false)
    }
  }, [])

  const addRule = useCallback(async (req: CreateManipulationRuleRequest) => {
    setRulesError(null)
    try {
      const rule = await createRule(req)
      setRules((prev) => [...prev, rule])
      return rule
    } catch (err) {
      setRulesError(err instanceof Error ? err.message : 'Failed to create rule')
      return null
    }
  }, [])

  const editRule = useCallback(async (id: string, req: UpdateManipulationRuleRequest) => {
    setRulesError(null)
    try {
      const rule = await updateRule(id, req)
      setRules((prev) => prev.map((r) => (r.id === id ? rule : r)))
      return rule
    } catch (err) {
      setRulesError(err instanceof Error ? err.message : 'Failed to update rule')
      return null
    }
  }, [])

  const removeRule = useCallback(async (id: string) => {
    setRulesError(null)
    try {
      await deleteRule(id)
      setRules((prev) => prev.filter((r) => r.id !== id))
    } catch (err) {
      setRulesError(err instanceof Error ? err.message : 'Failed to delete rule')
    }
  }, [])

  // ── Breakpoints ────────────────────────────────────────────────────────────
  const [breakpoints, setBreakpoints] = useState<Breakpoint[]>([])
  const [breakpointsLoading, setBreakpointsLoading] = useState(false)
  const [breakpointsError, setBreakpointsError] = useState<string | null>(null)

  const refreshBreakpoints = useCallback(async () => {
    setBreakpointsLoading(true)
    setBreakpointsError(null)
    try {
      const data = await listBreakpoints()
      setBreakpoints(data)
    } catch (err) {
      setBreakpointsError(err instanceof Error ? err.message : 'Failed to fetch breakpoints')
    } finally {
      setBreakpointsLoading(false)
    }
  }, [])

  const addBreakpoint = useCallback(async (req: CreateBreakpointRequest) => {
    setBreakpointsError(null)
    try {
      const bp = await createBreakpoint(req)
      setBreakpoints((prev) => [...prev, bp])
      return bp
    } catch (err) {
      setBreakpointsError(err instanceof Error ? err.message : 'Failed to create breakpoint')
      return null
    }
  }, [])

  const editBreakpoint = useCallback(async (id: string, req: UpdateBreakpointRequest) => {
    setBreakpointsError(null)
    try {
      const bp = await updateBreakpoint(id, req)
      setBreakpoints((prev) => prev.map((b) => (b.id === id ? bp : b)))
      return bp
    } catch (err) {
      setBreakpointsError(err instanceof Error ? err.message : 'Failed to update breakpoint')
      return null
    }
  }, [])

  const removeBreakpoint = useCallback(async (id: string) => {
    setBreakpointsError(null)
    try {
      await deleteBreakpoint(id)
      setBreakpoints((prev) => prev.filter((b) => b.id !== id))
    } catch (err) {
      setBreakpointsError(err instanceof Error ? err.message : 'Failed to delete breakpoint')
    }
  }, [])

  // ── Replay ─────────────────────────────────────────────────────────────────
  const [replays, setReplays] = useState<ReplaySession[]>([])
  const [replaysLoading, setReplaysLoading] = useState(false)
  const [replaysError, setReplaysError] = useState<string | null>(null)

  const refreshReplays = useCallback(async () => {
    setReplaysLoading(true)
    setReplaysError(null)
    try {
      const data = await listReplays()
      setReplays(data)
    } catch (err) {
      setReplaysError(err instanceof Error ? err.message : 'Failed to fetch replays')
    } finally {
      setReplaysLoading(false)
    }
  }, [])

  const replay = useCallback(async (req: CreateReplayRequest) => {
    setReplaysError(null)
    try {
      const session = await createReplay(req)
      setReplays((prev) => [session, ...prev])
      return session
    } catch (err) {
      setReplaysError(err instanceof Error ? err.message : 'Failed to replay request')
      return null
    }
  }, [])

  const removeReplay = useCallback(async (id: string) => {
    setReplaysError(null)
    try {
      await deleteReplay(id)
      setReplays((prev) => prev.filter((r) => r.id !== id))
    } catch (err) {
      setReplaysError(err instanceof Error ? err.message : 'Failed to delete replay')
    }
  }, [])

  // ── Fuzzer ─────────────────────────────────────────────────────────────────
  const [fuzzerJobs, setFuzzerJobs] = useState<FuzzerJob[]>([])
  const [selectedFuzzerResults, setSelectedFuzzerResults] = useState<FuzzerResult[]>([])
  const [fuzzerLoading, setFuzzerLoading] = useState(false)
  const [fuzzerError, setFuzzerError] = useState<string | null>(null)
  const fuzzerPollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const refreshFuzzer = useCallback(async () => {
    setFuzzerLoading(true)
    setFuzzerError(null)
    try {
      const data = await listFuzzerJobs()
      setFuzzerJobs(data)
    } catch (err) {
      setFuzzerError(err instanceof Error ? err.message : 'Failed to fetch fuzzer jobs')
    } finally {
      setFuzzerLoading(false)
    }
  }, [])

  // Poll for running fuzzer jobs
  useEffect(() => {
    const hasRunning = fuzzerJobs.some((j) => j.status === 'Running' || j.status === 'Pending')
    if (hasRunning) {
      fuzzerPollRef.current = setInterval(() => void refreshFuzzer(), 3000)
    } else if (fuzzerPollRef.current) {
      clearInterval(fuzzerPollRef.current)
      fuzzerPollRef.current = null
    }
    return () => {
      if (fuzzerPollRef.current) clearInterval(fuzzerPollRef.current)
    }
  }, [fuzzerJobs, refreshFuzzer])

  const fuzz = useCallback(async (req: StartFuzzerRequest) => {
    setFuzzerError(null)
    try {
      const job = await startFuzzer(req)
      setFuzzerJobs((prev) => [job, ...prev])
      return job
    } catch (err) {
      setFuzzerError(err instanceof Error ? err.message : 'Failed to start fuzzer')
      return null
    }
  }, [])

  const viewFuzzerResults = useCallback(async (id: string) => {
    setFuzzerError(null)
    try {
      // Refresh job details and results
      const [job, results] = await Promise.all([
        getFuzzerJob(id),
        getFuzzerResults(id),
      ])
      setFuzzerJobs((prev) => prev.map((j) => (j.id === id ? job : j)))
      setSelectedFuzzerResults(results)
    } catch (err) {
      setFuzzerError(err instanceof Error ? err.message : 'Failed to fetch fuzzer results')
    }
  }, [])

  const cancelFuzzer = useCallback(async (id: string) => {
    setFuzzerError(null)
    try {
      await cancelFuzzerJob(id)
      void refreshFuzzer()
    } catch (err) {
      setFuzzerError(err instanceof Error ? err.message : 'Failed to cancel fuzzer')
    }
  }, [refreshFuzzer])

  const removeFuzzer = useCallback(async (id: string) => {
    setFuzzerError(null)
    try {
      await deleteFuzzerJob(id)
      setFuzzerJobs((prev) => prev.filter((j) => j.id !== id))
    } catch (err) {
      setFuzzerError(err instanceof Error ? err.message : 'Failed to delete fuzzer job')
    }
  }, [])

  // ── Init ───────────────────────────────────────────────────────────────────

  useEffect(() => {
    void refreshRules()
    void refreshBreakpoints()
    void refreshReplays()
    void refreshFuzzer()
  }, [refreshRules, refreshBreakpoints, refreshReplays, refreshFuzzer])

  return {
    // Rules
    rules,
    rulesLoading,
    rulesError,
    refreshRules,
    addRule,
    editRule,
    removeRule,
    // Breakpoints
    breakpoints,
    breakpointsLoading,
    breakpointsError,
    refreshBreakpoints,
    addBreakpoint,
    editBreakpoint,
    removeBreakpoint,
    // Replay
    replays,
    replaysLoading,
    replaysError,
    refreshReplays,
    replay,
    removeReplay,
    // Fuzzer
    fuzzerJobs,
    selectedFuzzerResults,
    fuzzerLoading,
    fuzzerError,
    refreshFuzzer,
    fuzz,
    viewFuzzerResults,
    cancelFuzzer,
    removeFuzzer,
  }
}
