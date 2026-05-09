import { apiFetch, getToken } from './client'
import type {
  ApiSpecDocument,
  AssetInfo,
  GenerateSpecRequest,
  ImportSpecRequest,
  PreviewRuleRequest,
  PreviewRuleResult,
  UpdateSpecRequest,
} from '../types/api'

// ── Specs ───────────────────────────────────────────────────────────────────

export function listSpecs(): Promise<ApiSpecDocument[]> {
  return apiFetch<{ items: ApiSpecDocument[] }>('/api/apispec').then(r => r.items)
}

export function getSpec(id: string): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}`)
}

export function generateSpec(req: GenerateSpecRequest): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>('/api/apispec/generate', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function importSpec(req: ImportSpecRequest): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>('/api/apispec/import', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export async function exportSpec(id: string): Promise<string> {
  const token = getToken()
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`

  const res = await fetch(`/api/apispec/${id}/export`, { headers })
  if (!res.ok) throw new Error(`Export failed: ${res.statusText}`)
  return res.text()
}

export function updateSpec(id: string, req: UpdateSpecRequest): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  })
}

export function updateOpenApi(id: string, openApiJson: string): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}/openapi`, {
    method: 'PATCH',
    body: JSON.stringify({ openApiJson }),
  })
}

export function deleteSpec(id: string): Promise<void> {
  return apiFetch<void>(`/api/apispec/${id}`, { method: 'DELETE' })
}

export function refineSpec(id: string): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}/refine`, { method: 'POST' })
}

export function activateSpec(id: string): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}/activate`, { method: 'POST' })
}

export function deactivateSpec(id: string): Promise<ApiSpecDocument> {
  return apiFetch<ApiSpecDocument>(`/api/apispec/${id}/deactivate`, { method: 'POST' })
}

// ── Replacement Rule Preview ─────────────────────────────────────────────────

export function previewRule(
  specId: string,
  ruleId: string,
  req: PreviewRuleRequest,
): Promise<PreviewRuleResult> {
  return apiFetch<PreviewRuleResult>(`/api/apispec/${specId}/rules/${ruleId}/preview`, {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

// ── Assets ──────────────────────────────────────────────────────────────────

export async function uploadAsset(file: File): Promise<{ filePath: string; fileName: string }> {
  const token = getToken()
  const formData = new FormData()
  formData.append('file', file)

  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`

  const res = await fetch('/api/apispec/assets', {
    method: 'POST',
    headers,
    body: formData,
  })

  if (!res.ok) throw new Error(`Upload failed: ${res.statusText}`)
  return res.json()
}

export function listAssets(): Promise<AssetInfo[]> {
  return apiFetch<AssetInfo[]>('/api/apispec/assets')
}

export function deleteAsset(filename: string): Promise<void> {
  return apiFetch<void>(`/api/apispec/assets/${filename}`, { method: 'DELETE' })
}

export function getAssetContentUrl(fileName: string): string {
  return `/api/apispec/assets/${encodeURIComponent(fileName)}/content`
}

export async function uploadAssets(files: File[]): Promise<{ filePath: string; fileName: string }[]> {
  const results: { filePath: string; fileName: string }[] = []
  for (const file of files) {
    results.push(await uploadAsset(file))
  }
  return results
}
