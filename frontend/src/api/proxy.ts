import { apiFetch } from './client'
import type { ProxySettings, ProxySettingsUpdate, ProxyStatus } from '../types/api'

export function getProxyStatus(): Promise<ProxyStatus> {
  return apiFetch<ProxyStatus>('/api/proxy/status')
}

export function startProxy(): Promise<{ isRunning: boolean; port: number }> {
  return apiFetch('/api/proxy/start', { method: 'POST' })
}

export function stopProxy(): Promise<{ isRunning: boolean }> {
  return apiFetch('/api/proxy/stop', { method: 'POST' })
}

export function updateProxySettings(settings: ProxySettingsUpdate): Promise<ProxySettings> {
  return apiFetch<ProxySettings>('/api/proxy/settings', {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}
