import { apiFetch } from './client'
import type {
  OpenRtbEvent,
  OpenRtbPiiPolicy,
  CreatePiiPolicyRequest,
  UpdatePiiPolicyRequest,
  PiiStrippingLog,
  PiiAuditStats,
  PagedResult,
  OpenRtbMessageType,
} from '../types/api'

// ── Events ────────────────────────────────────────────────────────────────────

export function listEvents(params: {
  host?: string
  messageType?: OpenRtbMessageType
  from?: string
  to?: string
  hasPii?: boolean
  page?: number
  pageSize?: number
} = {}): Promise<PagedResult<OpenRtbEvent>> {
  const query = new URLSearchParams()
  if (params.host) query.set('host', params.host)
  if (params.messageType) query.set('messageType', params.messageType)
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  if (params.hasPii !== undefined) query.set('hasPii', String(params.hasPii))
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return apiFetch<PagedResult<OpenRtbEvent>>(`/api/openrtb/events${qs ? `?${qs}` : ''}`)
}

export function getEvent(id: string): Promise<OpenRtbEvent> {
  return apiFetch<OpenRtbEvent>(`/api/openrtb/events/${id}`)
}

export function deleteEvent(id: string): Promise<void> {
  return apiFetch<void>(`/api/openrtb/events/${id}`, { method: 'DELETE' })
}

// ── PII Policies ──────────────────────────────────────────────────────────────

export function listPolicies(): Promise<OpenRtbPiiPolicy[]> {
  return apiFetch<OpenRtbPiiPolicy[]>('/api/openrtb/policies')
}

export function createPolicy(req: CreatePiiPolicyRequest): Promise<OpenRtbPiiPolicy> {
  return apiFetch<OpenRtbPiiPolicy>('/api/openrtb/policies', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function updatePolicy(id: string, req: UpdatePiiPolicyRequest): Promise<OpenRtbPiiPolicy> {
  return apiFetch<OpenRtbPiiPolicy>(`/api/openrtb/policies/${id}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  })
}

export function deletePolicy(id: string): Promise<void> {
  return apiFetch<void>(`/api/openrtb/policies/${id}`, { method: 'DELETE' })
}

export function resetDefaultPolicies(): Promise<OpenRtbPiiPolicy[]> {
  return apiFetch<OpenRtbPiiPolicy[]>('/api/openrtb/policies/reset-defaults', { method: 'POST' })
}

// ── Audit Log ─────────────────────────────────────────────────────────────────

export function listAuditLogs(params: {
  host?: string
  fieldPath?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
} = {}): Promise<PagedResult<PiiStrippingLog>> {
  const query = new URLSearchParams()
  if (params.host) query.set('host', params.host)
  if (params.fieldPath) query.set('fieldPath', params.fieldPath)
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return apiFetch<PagedResult<PiiStrippingLog>>(`/api/openrtb/audit-log${qs ? `?${qs}` : ''}`)
}

export function getAuditLogForCapture(captureId: string): Promise<PiiStrippingLog[]> {
  return apiFetch<PiiStrippingLog[]>(`/api/openrtb/audit-log/capture/${captureId}`)
}

export function getAuditStats(params: {
  host?: string
  fieldPath?: string
  from?: string
  to?: string
} = {}): Promise<PiiAuditStats> {
  const query = new URLSearchParams()
  if (params.host) query.set('host', params.host)
  if (params.fieldPath) query.set('fieldPath', params.fieldPath)
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  const qs = query.toString()
  return apiFetch<PiiAuditStats>(`/api/openrtb/audit-log/stats${qs ? `?${qs}` : ''}`)
}
