import { apiFetch } from './client'
import type { AnalyticsStats, InsightTriageResponse, TrafficInsight } from '../types/analytics'

export function getTriageQueue(
  page = 1,
  pageSize = 50,
  unreviewedOnly = true
): Promise<InsightTriageResponse> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    unreviewedOnly: String(unreviewedOnly)
  })
  return apiFetch<InsightTriageResponse>(`/api/analytics/triage?${params}`)
}

export function getInsight(captureId: string): Promise<TrafficInsight | null> {
  return apiFetch<TrafficInsight>(`/api/analytics/insights/${captureId}`).catch(() => null)
}

export function getBulkInsights(captureIds: string[]): Promise<Record<string, TrafficInsight>> {
  return apiFetch<Record<string, TrafficInsight>>('/api/analytics/insights/bulk', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ captureIds })
  })
}

export function reviewInsight(id: string, dismissed: boolean, note?: string): Promise<void> {
  return apiFetch<void>(`/api/analytics/insights/${id}/review`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dismissed, note: note ?? null })
  })
}

export function scoreCapture(captureId: string): Promise<TrafficInsight> {
  return apiFetch<TrafficInsight>(`/api/analytics/score/${captureId}`, { method: 'POST' })
}

export function triggerBatchScore(): Promise<void> {
  return apiFetch<void>('/api/analytics/batch-score', { method: 'POST' })
}

export function getAnalyticsStats(): Promise<AnalyticsStats> {
  return apiFetch<AnalyticsStats>('/api/analytics/stats')
}
