import { apiFetch } from './client'
import type { Device, DevicePatchRequest } from '../types/api'

export function listDevices(): Promise<Device[]> {
  return apiFetch<{ items: Device[] }>('/api/devices').then(r => r.items)
}

export function getDevice(id: string): Promise<Device> {
  return apiFetch<Device>(`/api/devices/${id}`)
}

export function patchDevice(id: string, patch: DevicePatchRequest): Promise<Device> {
  return apiFetch<Device>(`/api/devices/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  })
}

export function deleteDevice(id: string): Promise<void> {
  return apiFetch<void>(`/api/devices/${id}`, { method: 'DELETE' })
}
