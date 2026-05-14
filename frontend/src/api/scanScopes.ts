import { apiFetch } from './client'
import type { ScanScope } from '../types/api'

export function listScopes(): Promise<{ items: ScanScope[]; total: number }> {
  return apiFetch<{ items: ScanScope[]; total: number }>('/api/scopes')
}

export function createScope(name: string, cidr: string): Promise<ScanScope> {
  return apiFetch<ScanScope>('/api/scopes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, cidr }),
  })
}

export function toggleScope(id: string): Promise<ScanScope> {
  return apiFetch<ScanScope>(`/api/scopes/${id}/toggle`, { method: 'PATCH' })
}

export function deleteScope(id: string): Promise<void> {
  return apiFetch<void>(`/api/scopes/${id}`, { method: 'DELETE' })
}
