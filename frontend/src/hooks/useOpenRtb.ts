import { useCallback, useEffect, useState } from 'react'
import {
  listEvents,
  listPolicies,
  createPolicy,
  updatePolicy,
  deletePolicy,
  resetDefaultPolicies,
  listAuditLogs,
  getAuditStats,
} from '../api/openrtb'
import type {
  OpenRtbEvent,
  OpenRtbPiiPolicy,
  CreatePiiPolicyRequest,
  UpdatePiiPolicyRequest,
  PiiStrippingLog,
  PiiAuditStats,
  PagedResult,
} from '../types/api'

export function useOpenRtb() {
  // ── Events ──────────────────────────────────────────────────────────────────
  const [events, setEvents] = useState<OpenRtbEvent[]>([])
  const [eventsTotal, setEventsTotal] = useState(0)
  const [eventsLoading, setEventsLoading] = useState(false)
  const [eventsError, setEventsError] = useState<string | null>(null)

  const refreshEvents = useCallback(async (page = 1) => {
    setEventsLoading(true)
    setEventsError(null)
    try {
      const data = await listEvents({ page, pageSize: 50 })
      setEvents(data.items)
      setEventsTotal(data.total)
    } catch (err) {
      setEventsError(err instanceof Error ? err.message : 'Failed to fetch events')
    } finally {
      setEventsLoading(false)
    }
  }, [])

  // ── Policies ────────────────────────────────────────────────────────────────
  const [policies, setPolicies] = useState<OpenRtbPiiPolicy[]>([])
  const [policiesLoading, setPoliciesLoading] = useState(false)
  const [policiesError, setPoliciesError] = useState<string | null>(null)

  const refreshPolicies = useCallback(async () => {
    setPoliciesLoading(true)
    setPoliciesError(null)
    try {
      const data = await listPolicies()
      setPolicies(data)
    } catch (err) {
      setPoliciesError(err instanceof Error ? err.message : 'Failed to fetch policies')
    } finally {
      setPoliciesLoading(false)
    }
  }, [])

  const addPolicy = useCallback(async (req: CreatePiiPolicyRequest) => {
    const policy = await createPolicy(req)
    setPolicies((prev) => [...prev, policy])
    return policy
  }, [])

  const editPolicy = useCallback(async (id: string, req: UpdatePiiPolicyRequest) => {
    const policy = await updatePolicy(id, req)
    setPolicies((prev) => prev.map((p) => (p.id === id ? policy : p)))
    return policy
  }, [])

  const removePolicy = useCallback(async (id: string) => {
    await deletePolicy(id)
    setPolicies((prev) => prev.filter((p) => p.id !== id))
  }, [])

  const resetPolicies = useCallback(async () => {
    const defaults = await resetDefaultPolicies()
    setPolicies(defaults)
  }, [])

  // ── Audit Log ───────────────────────────────────────────────────────────────
  const [auditLogs, setAuditLogs] = useState<PiiStrippingLog[]>([])
  const [auditTotal, setAuditTotal] = useState(0)
  const [auditStats, setAuditStats] = useState<PiiAuditStats | null>(null)
  const [auditLoading, setAuditLoading] = useState(false)
  const [auditError, setAuditError] = useState<string | null>(null)

  const refreshAuditLog = useCallback(async (page = 1) => {
    setAuditLoading(true)
    setAuditError(null)
    try {
      const [logs, stats] = await Promise.all([
        listAuditLogs({ page, pageSize: 50 }),
        getAuditStats(),
      ])
      setAuditLogs(logs.items)
      setAuditTotal(logs.total)
      setAuditStats(stats)
    } catch (err) {
      setAuditError(err instanceof Error ? err.message : 'Failed to fetch audit log')
    } finally {
      setAuditLoading(false)
    }
  }, [])

  // ── Initial load ────────────────────────────────────────────────────────────
  useEffect(() => {
    refreshEvents()
    refreshPolicies()
    refreshAuditLog()
  }, [refreshEvents, refreshPolicies, refreshAuditLog])

  return {
    events, eventsTotal, eventsLoading, eventsError, refreshEvents,
    policies, policiesLoading, policiesError, refreshPolicies,
    addPolicy, editPolicy, removePolicy, resetPolicies,
    auditLogs, auditTotal, auditStats, auditLoading, auditError, refreshAuditLog,
  }
}
