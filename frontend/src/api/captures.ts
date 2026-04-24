import { apiFetch, getToken } from './client'
import type { CaptureFilters, CaptureListResponse, CapturedRequest, ExportCaptureAsAssetResult } from '../types/api'

export function listCaptures(filters: CaptureFilters = {}): Promise<CaptureListResponse> {
  const params = new URLSearchParams()
  if (filters.deviceId) params.set('deviceId', filters.deviceId)
  if (filters.host) params.set('host', filters.host)
  if (filters.method) params.set('method', filters.method)
  if (filters.statusCode != null) params.set('statusCode', String(filters.statusCode))
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (filters.q) params.set('q', filters.q)
  if (filters.clientIp) params.set('clientIp', filters.clientIp)
  if (filters.page != null) params.set('page', String(filters.page))
  if (filters.pageSize != null) params.set('pageSize', String(filters.pageSize))
  const qs = params.toString()
  return apiFetch<CaptureListResponse>(`/api/captures${qs ? `?${qs}` : ''}`)
}

export function getCapture(id: string): Promise<CapturedRequest> {
  return apiFetch<CapturedRequest>(`/api/captures/${id}`)
}

export function deleteCapture(id: string): Promise<void> {
  return apiFetch<void>(`/api/captures/${id}`, { method: 'DELETE' })
}

export function exportAsAsset(id: string): Promise<ExportCaptureAsAssetResult> {
  return apiFetch<ExportCaptureAsAssetResult>(`/api/captures/${id}/export-as-asset`, { method: 'POST' })
}

export function downloadBodyUrl(id: string): string {
  return `/api/captures/${id}/download-body`
}

export async function exportCaptures(
  format: 'csv' | 'json' | 'har',
  filters: CaptureFilters = {},
): Promise<void> {
  const params = new URLSearchParams({ format })
  if (filters.deviceId) params.set('deviceId', filters.deviceId)
  if (filters.host) params.set('host', filters.host)
  if (filters.method) params.set('method', filters.method)
  if (filters.statusCode != null) params.set('statusCode', String(filters.statusCode))
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (filters.q) params.set('q', filters.q)

  const token = getToken()
  const res = await fetch(`/api/captures/export?${params.toString()}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!res.ok) throw new Error(`Export failed: ${res.statusText}`)

  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `captures.${format}`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
