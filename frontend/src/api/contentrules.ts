import { apiFetch } from './client'
import type { ContentReplacementRule, PreviewRuleRequest, PreviewRuleResult } from '../types/api'

export interface CreateContentRuleRequest {
  host: string
  name: string
  matchType: string
  matchPattern: string
  action: string
  enabled?: boolean
  replacementValue?: string
  replacementFilePath?: string
  replacementContentType?: string
  hostPattern?: string
  pathPattern?: string
  priority?: number
  sseInterEventDelayMs?: number
  sseLoop?: boolean
}

export interface UpdateContentRuleRequest {
  host?: string
  name?: string
  enabled?: boolean
  matchType?: string
  matchPattern?: string
  action?: string
  replacementValue?: string
  replacementFilePath?: string
  replacementContentType?: string
  hostPattern?: string
  pathPattern?: string
  priority?: number
  sseInterEventDelayMs?: number
  sseLoop?: boolean
}

export function listContentRules(host?: string): Promise<ContentReplacementRule[]> {
  const qs = host ? `?host=${encodeURIComponent(host)}` : ''
  return apiFetch<ContentReplacementRule[]>(`/api/contentrules${qs}`)
}

export function createContentRule(req: CreateContentRuleRequest): Promise<ContentReplacementRule> {
  return apiFetch<ContentReplacementRule>('/api/contentrules', { method: 'POST', body: JSON.stringify(req) })
}

export function updateContentRule(id: string, req: UpdateContentRuleRequest): Promise<ContentReplacementRule> {
  return apiFetch<ContentReplacementRule>(`/api/contentrules/${id}`, { method: 'PUT', body: JSON.stringify(req) })
}

export function deleteContentRule(id: string): Promise<void> {
  return apiFetch<void>(`/api/contentrules/${id}`, { method: 'DELETE' })
}

export function previewContentRule(id: string, req: PreviewRuleRequest): Promise<PreviewRuleResult> {
  return apiFetch<PreviewRuleResult>(`/api/contentrules/${id}/preview`, { method: 'POST', body: JSON.stringify(req) })
}
