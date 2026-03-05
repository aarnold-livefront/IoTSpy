import { apiFetch } from './client'
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
  FuzzerJobStatus,
} from '../types/api'

// ── Rules ────────────────────────────────────────────────────────────────────

export function listRules(): Promise<ManipulationRule[]> {
  return apiFetch<ManipulationRule[]>('/api/manipulation/rules')
}

export function getRule(id: string): Promise<ManipulationRule> {
  return apiFetch<ManipulationRule>(`/api/manipulation/rules/${id}`)
}

export function createRule(request: CreateManipulationRuleRequest): Promise<ManipulationRule> {
  return apiFetch<ManipulationRule>('/api/manipulation/rules', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function updateRule(id: string, request: UpdateManipulationRuleRequest): Promise<ManipulationRule> {
  return apiFetch<ManipulationRule>(`/api/manipulation/rules/${id}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export function deleteRule(id: string): Promise<void> {
  return apiFetch<void>(`/api/manipulation/rules/${id}`, { method: 'DELETE' })
}

// ── Breakpoints ──────────────────────────────────────────────────────────────

export function listBreakpoints(): Promise<Breakpoint[]> {
  return apiFetch<Breakpoint[]>('/api/manipulation/breakpoints')
}

export function getBreakpoint(id: string): Promise<Breakpoint> {
  return apiFetch<Breakpoint>(`/api/manipulation/breakpoints/${id}`)
}

export function createBreakpoint(request: CreateBreakpointRequest): Promise<Breakpoint> {
  return apiFetch<Breakpoint>('/api/manipulation/breakpoints', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function updateBreakpoint(id: string, request: UpdateBreakpointRequest): Promise<Breakpoint> {
  return apiFetch<Breakpoint>(`/api/manipulation/breakpoints/${id}`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export function deleteBreakpoint(id: string): Promise<void> {
  return apiFetch<void>(`/api/manipulation/breakpoints/${id}`, { method: 'DELETE' })
}

// ── Replay ───────────────────────────────────────────────────────────────────

export function listReplays(): Promise<ReplaySession[]> {
  return apiFetch<ReplaySession[]>('/api/manipulation/replays')
}

export function getReplay(id: string): Promise<ReplaySession> {
  return apiFetch<ReplaySession>(`/api/manipulation/replays/${id}`)
}

export function createReplay(request: CreateReplayRequest): Promise<ReplaySession> {
  return apiFetch<ReplaySession>('/api/manipulation/replays', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function deleteReplay(id: string): Promise<void> {
  return apiFetch<void>(`/api/manipulation/replays/${id}`, { method: 'DELETE' })
}

// ── Fuzzer ───────────────────────────────────────────────────────────────────

export function listFuzzerJobs(): Promise<FuzzerJob[]> {
  return apiFetch<FuzzerJob[]>('/api/manipulation/fuzzer/jobs')
}

export function startFuzzer(request: StartFuzzerRequest): Promise<FuzzerJob> {
  return apiFetch<FuzzerJob>('/api/manipulation/fuzzer', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function getFuzzerJob(id: string): Promise<FuzzerJob> {
  return apiFetch<FuzzerJob>(`/api/manipulation/fuzzer/jobs/${id}`)
}

export function getFuzzerResults(id: string): Promise<FuzzerResult[]> {
  return apiFetch<FuzzerResult[]>(`/api/manipulation/fuzzer/jobs/${id}/results`)
}

export function getFuzzerStatus(id: string): Promise<{ status: FuzzerJobStatus }> {
  return apiFetch<{ status: FuzzerJobStatus }>(`/api/manipulation/fuzzer/jobs/${id}/status`)
}

export function cancelFuzzerJob(id: string): Promise<void> {
  return apiFetch<void>(`/api/manipulation/fuzzer/jobs/${id}/cancel`, { method: 'POST' })
}

export function deleteFuzzerJob(id: string): Promise<void> {
  return apiFetch<void>(`/api/manipulation/fuzzer/jobs/${id}`, { method: 'DELETE' })
}
