import { apiFetch } from './client'
import type {
  InvestigationSession,
  SessionCapture,
  CaptureAnnotation,
  SessionActivity,
  ShareTokenResponse,
  SharedSessionPayload,
} from '../types/sessions'

// ── Sessions CRUD ────────────────────────────────────────────────────────────

export function listSessions(includeInactive = false): Promise<InvestigationSession[]> {
  return apiFetch<InvestigationSession[]>(`/api/sessions?includeInactive=${includeInactive}`)
}

export function getSession(id: string): Promise<InvestigationSession> {
  return apiFetch<InvestigationSession>(`/api/sessions/${id}`)
}

export function createSession(name: string, description?: string): Promise<InvestigationSession> {
  return apiFetch<InvestigationSession>('/api/sessions', {
    method: 'POST',
    body: JSON.stringify({ name, description }),
  })
}

export function updateSession(id: string, name?: string, description?: string): Promise<InvestigationSession> {
  return apiFetch<InvestigationSession>(`/api/sessions/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ name, description }),
  })
}

export function deleteSession(id: string): Promise<void> {
  return apiFetch<void>(`/api/sessions/${id}`, { method: 'DELETE' })
}

// ── Session captures ─────────────────────────────────────────────────────────

export function getSessionCaptures(sessionId: string): Promise<SessionCapture[]> {
  return apiFetch<SessionCapture[]>(`/api/sessions/${sessionId}/captures`)
}

export function addCaptureToSession(sessionId: string, captureId: string): Promise<void> {
  return apiFetch<void>(`/api/sessions/${sessionId}/captures`, {
    method: 'POST',
    body: JSON.stringify({ captureId }),
  })
}

export function removeCaptureFromSession(sessionId: string, captureId: string): Promise<void> {
  return apiFetch<void>(`/api/sessions/${sessionId}/captures/${captureId}`, { method: 'DELETE' })
}

// ── Annotations ──────────────────────────────────────────────────────────────

export function getAnnotations(sessionId: string): Promise<CaptureAnnotation[]> {
  return apiFetch<CaptureAnnotation[]>(`/api/sessions/${sessionId}/annotations`)
}

export function addAnnotation(
  sessionId: string,
  captureId: string,
  note: string,
  tags?: string,
): Promise<CaptureAnnotation> {
  return apiFetch<CaptureAnnotation>(`/api/sessions/${sessionId}/annotations`, {
    method: 'POST',
    body: JSON.stringify({ captureId, note, tags }),
  })
}

export function updateAnnotation(
  sessionId: string,
  annotationId: string,
  note: string,
  tags?: string,
): Promise<CaptureAnnotation> {
  return apiFetch<CaptureAnnotation>(`/api/sessions/${sessionId}/annotations/${annotationId}`, {
    method: 'PUT',
    body: JSON.stringify({ note, tags }),
  })
}

export function deleteAnnotation(sessionId: string, annotationId: string): Promise<void> {
  return apiFetch<void>(`/api/sessions/${sessionId}/annotations/${annotationId}`, { method: 'DELETE' })
}

// ── Activity feed ────────────────────────────────────────────────────────────

export function getActivity(sessionId: string, count = 100): Promise<SessionActivity[]> {
  return apiFetch<SessionActivity[]>(`/api/sessions/${sessionId}/activity?count=${count}`)
}

// ── Sharing (AirDrop / URL) ───────────────────────────────────────────────────

export function generateShareToken(sessionId: string): Promise<ShareTokenResponse> {
  return apiFetch<ShareTokenResponse>(`/api/sessions/${sessionId}/share`, { method: 'POST' })
}

export function revokeShareToken(sessionId: string): Promise<void> {
  return apiFetch<void>(`/api/sessions/${sessionId}/share`, { method: 'DELETE' })
}

export function getSharedSession(token: string): Promise<SharedSessionPayload> {
  return apiFetch<SharedSessionPayload>(`/api/sessions/share/${token}`)
}

// ── Export ───────────────────────────────────────────────────────────────────

export async function exportSession(sessionId: string, name: string): Promise<void> {
  const { getToken } = await import('./client')
  const token = getToken()
  const res = await fetch(`/api/sessions/${sessionId}/export`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!res.ok) throw new Error('Export failed')
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `session-${name.replace(/\s+/g, '_')}.zip`
  a.click()
  URL.revokeObjectURL(url)
}
