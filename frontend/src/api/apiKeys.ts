import { apiFetch } from './client'
import type { ApiKeyCreated, ApiKeySummary } from '../types/api'

export async function listApiKeys(): Promise<ApiKeySummary[]> {
  return apiFetch<ApiKeySummary[]>('/api/auth/api-keys')
}

export async function createApiKey(
  name: string,
  scopes: string[],
  expiresAt?: string | null,
): Promise<ApiKeyCreated> {
  return apiFetch<ApiKeyCreated>('/api/auth/api-keys', {
    method: 'POST',
    body: JSON.stringify({ name, scopes: scopes.join(' '), expiresAt: expiresAt ?? null }),
  })
}

export async function revokeApiKey(id: string): Promise<void> {
  return apiFetch<void>(`/api/auth/api-keys/${id}`, { method: 'DELETE' })
}

export async function rotateApiKey(id: string): Promise<ApiKeyCreated> {
  return apiFetch<ApiKeyCreated>(`/api/auth/api-keys/${id}/rotate`, { method: 'POST' })
}
