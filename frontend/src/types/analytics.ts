export type RiskTag =
  | 'ExfiltrationRisk'
  | 'PiiDetected'
  | 'DataBroker'
  | 'SuspiciousTls'
  | 'UnusualPort'
  | 'MqttCredentialExposure'
  | 'DnsTunneling'
  | 'HighEntropyPayload'

export interface TrafficInsight {
  id: string
  captureId: string
  tagsJson: string
  confidenceJson: string
  riskScore: number
  modelVersion: string
  source: 'rule' | 'ml' | 'hybrid'
  isReviewed: boolean
  isDismissed: boolean
  reviewNote?: string
  reviewedByUserId?: string
  reviewedAt?: string
  createdAt: string
}

export interface InsightTriageResponse {
  items: TrafficInsight[]
  total: number
  page: number
  pageSize: number
  pages: number
}

export interface AnalyticsStats {
  total: number
  unreviewed: number
  reviewed: number
}

export function parseTags(insight: TrafficInsight): RiskTag[] {
  try {
    return JSON.parse(insight.tagsJson) as RiskTag[]
  } catch {
    return []
  }
}

export function parseConfidence(insight: TrafficInsight): Record<string, number> {
  try {
    return JSON.parse(insight.confidenceJson) as Record<string, number>
  } catch {
    return {}
  }
}
