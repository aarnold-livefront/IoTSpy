import { apiFetch } from './client'
import type {
  PassiveCaptureSummary,
  PassiveCaptureSession,
  CapturedRequestSummary,
} from '../types/api'

export function getPassiveSummary(): Promise<PassiveCaptureSummary> {
  return apiFetch<PassiveCaptureSummary>('/api/passive/summary')
}

export function getBufferedCaptures(page = 1, pageSize = 100): Promise<{ items: CapturedRequestSummary[]; total: number }> {
  return apiFetch(`/api/passive/captures?page=${page}&pageSize=${pageSize}`)
}

export function clearBuffer(): Promise<void> {
  return apiFetch('/api/passive/captures', { method: 'DELETE' })
}

export function getDeviceFilter(): Promise<{ deviceIps: string[] }> {
  return apiFetch('/api/passive/filter')
}

export function setDeviceFilter(deviceIps: string[]): Promise<{ deviceIps: string[] }> {
  return apiFetch('/api/passive/filter', {
    method: 'PUT',
    body: JSON.stringify({ deviceIps }),
  })
}

export function clearDeviceFilter(): Promise<void> {
  return apiFetch('/api/passive/filter', { method: 'DELETE' })
}

export function listPassiveSessions(): Promise<PassiveCaptureSession[]> {
  return apiFetch('/api/passive/sessions')
}

export function savePassiveSession(
  name: string,
  description?: string,
  deviceIps?: string[],
  clearBufferAfterSave = false,
): Promise<PassiveCaptureSession> {
  return apiFetch('/api/passive/sessions', {
    method: 'POST',
    body: JSON.stringify({ name, description, deviceIps, clearBufferAfterSave }),
  })
}

export function getPassiveSession(id: string): Promise<PassiveCaptureSession> {
  return apiFetch(`/api/passive/sessions/${id}`)
}

export function getSessionCaptures(id: string): Promise<CapturedRequestSummary[]> {
  return apiFetch(`/api/passive/sessions/${id}/captures`)
}

export function deletePassiveSession(id: string): Promise<void> {
  return apiFetch(`/api/passive/sessions/${id}`, { method: 'DELETE' })
}
