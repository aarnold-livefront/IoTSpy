// ── Enums ────────────────────────────────────────────────────────────────────

export type InterceptionProtocol =
  | 'Http'
  | 'Https'
  | 'Mqtt'
  | 'MqttTls'
  | 'CoAP'
  | 'Dns'
  | 'MDns'
  | 'Other'

export type ProxyMode = 'ExplicitProxy' | 'ArpSpoof' | 'GatewayRedirect'

// ── Domain models ─────────────────────────────────────────────────────────────

export interface CapturedRequest {
  id: string
  deviceId?: string
  device?: Device
  // Request
  method: string
  scheme: string
  host: string
  port: number
  path: string
  query: string
  requestHeaders: string   // JSON-serialized dictionary
  requestBody: string
  requestBodySize: number
  // Response
  statusCode: number
  statusMessage: string
  responseHeaders: string  // JSON-serialized dictionary
  responseBody: string
  responseBodySize: number
  // TLS
  isTls: boolean
  tlsVersion: string
  tlsCipherSuite: string
  // Meta
  protocol: InterceptionProtocol
  timestamp: string        // ISO 8601
  durationMs: number
  clientIp: string
  isModified: boolean
  notes: string
}

export interface Device {
  id: string
  ipAddress: string
  macAddress: string
  hostname: string
  vendor: string
  label: string
  notes: string
  interceptionEnabled: boolean
  firstSeen: string
  lastSeen: string
  securityScore: number    // -1 = unscored
}

export interface ProxySettings {
  id: number
  proxyPort: number
  mode: ProxyMode
  isRunning: boolean
  captureTls: boolean
  captureRequestBodies: boolean
  captureResponseBodies: boolean
  maxBodySizeKb: number
  listenAddress: string
  passwordHash: string
}

export interface CertificateEntry {
  id: string
  commonName: string
  subjectAltNames: string
  certificatePem: string
  privateKeyPem: string
  notBefore: string
  notAfter: string
  serialNumber: string
  isRootCa: boolean
  createdAt: string
}

// ── API response shapes ───────────────────────────────────────────────────────

export interface CaptureListResponse {
  items: CapturedRequest[]
  total: number
  page: number
  pageSize: number
  pages: number
}

export interface ProxyStatus {
  isRunning: boolean
  port: number
  settings: ProxySettings
}

export interface AuthStatusResponse {
  passwordSet: boolean
}

export interface LoginResponse {
  token: string
}

export interface RootCaSummary {
  id: string
  commonName: string
  serialNumber: string
  notBefore: string
  notAfter: string
  isRootCa: boolean
  createdAt: string
  certificatePem: string
}

// ── Request payloads ──────────────────────────────────────────────────────────

export interface LoginRequest {
  username: string
  password: string
}

export interface SetupRequest {
  password: string
}

export interface DevicePatchRequest {
  label?: string
  notes?: string
  interceptionEnabled?: boolean
}

export interface ProxySettingsUpdate {
  proxyPort?: number
  listenAddress?: string
  captureTls?: boolean
  captureRequestBodies?: boolean
  captureResponseBodies?: boolean
  maxBodySizeKb?: number
  mode?: ProxyMode
}

// ── Filter params ─────────────────────────────────────────────────────────────

export interface CaptureFilters {
  deviceId?: string
  host?: string
  method?: string
  statusCode?: number
  from?: string
  to?: string
  q?: string
  page?: number
  pageSize?: number
}

// ── SignalR DTOs ──────────────────────────────────────────────────────────────

/** Trimmed capture event pushed via SignalR — no body content */
export interface TrafficCaptureEvent {
  id: string
  deviceId?: string
  method: string
  scheme: string
  host: string
  port: number
  path: string
  query: string
  statusCode: number
  statusMessage: string
  protocol: InterceptionProtocol
  isTls: boolean
  tlsVersion: string
  timestamp: string
  durationMs: number
  clientIp: string
  requestBodySize: number
  responseBodySize: number
}
