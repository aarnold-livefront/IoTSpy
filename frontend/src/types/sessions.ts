// ── Investigation sessions ────────────────────────────────────────────────────

export interface InvestigationSession {
  id: string
  name: string
  description?: string
  createdByUserId: string
  createdByUsername: string
  createdAt: string      // ISO 8601 (stored as Unix ms, serialized as string)
  closedAt?: string
  isActive: boolean
  hasShareToken: boolean
}

export interface SessionCapture {
  id: string
  sessionId: string
  captureId: string
  addedAt: string
  addedByUserId: string
  capture?: {
    id: string
    method: string
    scheme: string
    host: string
    path: string
    statusCode: number
    protocol: string
    timestamp: string
  }
}

export interface CaptureAnnotation {
  id: string
  sessionId: string
  captureId: string
  userId: string
  username: string
  note: string
  tags?: string
  createdAt: string
  updatedAt?: string
}

export interface SessionActivity {
  id: string
  sessionId: string
  userId: string
  username: string
  action: string
  details?: string
  timestamp: string
}

export interface PresenceEntry {
  userId: string
  username: string
  joinedAt: string
}

export interface ShareTokenResponse {
  token: string
  url: string
}

export interface SharedSessionPayload {
  session: InvestigationSession
  captures: Array<SessionCapture['capture'] | null>
  annotations: CaptureAnnotation[]
  exportedAt: string
  format: string
}
