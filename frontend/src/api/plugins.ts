import { apiFetch } from './client'
import type { PluginInfo } from '../types/api'

export function listPlugins(): Promise<PluginInfo[]> {
  return apiFetch<PluginInfo[]>('/api/plugins')
}

export function reloadPlugins(): Promise<{ reloaded: number; plugins: PluginInfo[] }> {
  return apiFetch<{ reloaded: number; plugins: PluginInfo[] }>('/api/plugins/reload', { method: 'POST' })
}
