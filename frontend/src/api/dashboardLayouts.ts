import { apiFetch } from './client'
import type { DashboardLayout } from '../types/api'

export function listLayouts(): Promise<DashboardLayout[]> {
  return apiFetch<DashboardLayout[]>('/api/dashboard/layouts')
}

export function createLayout(name: string, layoutJson: string, filtersJson: string): Promise<DashboardLayout> {
  return apiFetch<DashboardLayout>('/api/dashboard/layouts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, layoutJson, filtersJson }),
  })
}

export interface UpdateLayoutRequest {
  name?: string
  layoutJson?: string
  filtersJson?: string
  isDefault?: boolean
}

export function updateLayout(id: string, req: UpdateLayoutRequest): Promise<DashboardLayout> {
  return apiFetch<DashboardLayout>(`/api/dashboard/layouts/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  })
}

export function deleteLayout(id: string): Promise<void> {
  return apiFetch<void>(`/api/dashboard/layouts/${id}`, { method: 'DELETE' })
}
