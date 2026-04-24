import { useCallback, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
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
} from '../types/api'

const POLICIES_KEY = ['openrtb-policies']

export function useOpenRtb() {
  const queryClient = useQueryClient()

  // ── Events ──────────────────────────────────────────────────────────────────
  const [eventsPage, setEventsPage] = useState(1)

  const {
    data: eventsData,
    isLoading: eventsLoading,
    error: eventsQueryError,
    refetch: refetchEvents,
  } = useQuery({
    queryKey: ['openrtb-events', eventsPage],
    queryFn: () => listEvents({ page: eventsPage, pageSize: 50 }),
  })

  const events: OpenRtbEvent[] = eventsData?.items ?? []
  const eventsTotal = eventsData?.total ?? 0
  const eventsError = eventsQueryError instanceof Error ? eventsQueryError.message : null

  const refreshEvents = useCallback(
    (page = 1) => {
      setEventsPage(page)
      void refetchEvents()
    },
    [refetchEvents],
  )

  // ── Policies ────────────────────────────────────────────────────────────────
  const {
    data: policies = [],
    isLoading: policiesLoading,
    error: policiesQueryError,
    refetch: refetchPolicies,
  } = useQuery({ queryKey: POLICIES_KEY, queryFn: listPolicies })

  const policiesError = policiesQueryError instanceof Error ? policiesQueryError.message : null

  const refreshPolicies = useCallback(() => { void refetchPolicies() }, [refetchPolicies])

  const addPolicyMutation = useMutation({
    mutationFn: (req: CreatePiiPolicyRequest) => createPolicy(req),
    onSuccess: (policy) => {
      queryClient.setQueryData<OpenRtbPiiPolicy[]>(POLICIES_KEY, (prev = []) => [
        ...prev,
        policy,
      ])
    },
  })

  const editPolicyMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdatePiiPolicyRequest }) =>
      updatePolicy(id, req),
    onSuccess: (policy) => {
      queryClient.setQueryData<OpenRtbPiiPolicy[]>(POLICIES_KEY, (prev = []) =>
        prev.map((p) => (p.id === policy.id ? policy : p)),
      )
    },
  })

  const removePolicyMutation = useMutation({
    mutationFn: (id: string) => deletePolicy(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<OpenRtbPiiPolicy[]>(POLICIES_KEY, (prev = []) =>
        prev.filter((p) => p.id !== id),
      )
    },
  })

  const resetPoliciesMutation = useMutation({
    mutationFn: () => resetDefaultPolicies(),
    onSuccess: (defaults) => {
      queryClient.setQueryData<OpenRtbPiiPolicy[]>(POLICIES_KEY, defaults)
    },
  })

  const addPolicy = useCallback(
    (req: CreatePiiPolicyRequest) => addPolicyMutation.mutateAsync(req),
    [addPolicyMutation],
  )

  const editPolicy = useCallback(
    (id: string, req: UpdatePiiPolicyRequest) => editPolicyMutation.mutateAsync({ id, req }),
    [editPolicyMutation],
  )

  const removePolicy = useCallback(
    (id: string) => removePolicyMutation.mutateAsync(id),
    [removePolicyMutation],
  )

  const resetPolicies = useCallback(
    async () => { await resetPoliciesMutation.mutateAsync() },
    [resetPoliciesMutation],
  )

  // ── Audit Log ───────────────────────────────────────────────────────────────
  const [auditPage, setAuditPage] = useState(1)

  const {
    data: auditData,
    isLoading: auditLoading,
    error: auditQueryError,
    refetch: refetchAudit,
  } = useQuery({
    queryKey: ['openrtb-audit', auditPage],
    queryFn: () =>
      Promise.all([
        listAuditLogs({ page: auditPage, pageSize: 50 }),
        getAuditStats(),
      ]),
  })

  const auditLogs: PiiStrippingLog[] = auditData?.[0]?.items ?? []
  const auditTotal = auditData?.[0]?.total ?? 0
  const auditStats: PiiAuditStats | null = auditData?.[1] ?? null
  const auditError = auditQueryError instanceof Error ? auditQueryError.message : null

  const refreshAuditLog = useCallback(
    (page = 1) => {
      setAuditPage(page)
      void refetchAudit()
    },
    [refetchAudit],
  )

  return {
    events, eventsTotal, eventsLoading, eventsError, refreshEvents,
    policies, policiesLoading, policiesError, refreshPolicies,
    addPolicy, editPolicy, removePolicy, resetPolicies,
    auditLogs, auditTotal, auditStats, auditLoading, auditError, refreshAuditLog,
  }
}
