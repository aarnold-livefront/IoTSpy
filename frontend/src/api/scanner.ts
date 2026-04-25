import { apiFetch } from './client'
import type { ScanJob, StartScanRequest } from '../types/api'

export function startScan(request: StartScanRequest): Promise<ScanJob> {
  return apiFetch<ScanJob>('/api/scanner/scan', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function listScanJobs(): Promise<ScanJob[]> {
  return apiFetch<{ items: ScanJob[] }>('/api/scanner/jobs').then(r => r.items)
}

export function cancelAllScanJobs(): Promise<{ cancelled: number }> {
  return apiFetch<{ cancelled: number }>('/api/scanner/jobs/cancel-all', { method: 'POST' })
}

export function getScanJob(id: string): Promise<ScanJob> {
  return apiFetch<ScanJob>(`/api/scanner/jobs/${id}`)
}

export function cancelScanJob(id: string): Promise<void> {
  return apiFetch<void>(`/api/scanner/jobs/${id}/cancel`, { method: 'POST' })
}

export function deleteScanJob(id: string): Promise<void> {
  return apiFetch<void>(`/api/scanner/jobs/${id}`, { method: 'DELETE' })
}
