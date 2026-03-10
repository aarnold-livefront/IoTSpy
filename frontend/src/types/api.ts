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
  // Phase 2 — GatewayRedirect / ArpSpoof settings
  transparentProxyPort: number
  targetDeviceIp: string
  gatewayIp: string
  networkInterface: string
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
  transparentProxyPort?: number
  targetDeviceIp?: string
  gatewayIp?: string
  networkInterface?: string
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

// ── Scanner enums ────────────────────────────────────────────────────────────

export type ScanStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'

export type ScanFindingSeverity = 'Info' | 'Low' | 'Medium' | 'High' | 'Critical'

export type ScanFindingType =
  | 'OpenPort'
  | 'ServiceBanner'
  | 'DefaultCredential'
  | 'KnownCve'
  | 'ConfigIssue'

// ── Scanner models ───────────────────────────────────────────────────────────

export interface ScanJob {
  id: string
  deviceId: string
  device?: Device
  status: ScanStatus
  portRange: string
  maxConcurrency: number
  timeoutMs: number
  enableFingerprinting: boolean
  enableCredentialTest: boolean
  enableCveLookup: boolean
  enableConfigAudit: boolean
  portsScanned: number
  openPortsFound: number
  findingsCount: number
  startedAt?: string
  completedAt?: string
  errorMessage?: string
  createdAt: string
  findings?: ScanFinding[]
}

export interface ScanFinding {
  id: string
  scanJobId: string
  type: ScanFindingType
  severity: ScanFindingSeverity
  port: number
  protocol: string
  service: string
  title: string
  description: string
  evidence: string
  remediation: string
  cveId?: string
  cpeString?: string
  createdAt: string
}

// ── Scanner request DTOs ─────────────────────────────────────────────────────

export interface StartScanRequest {
  deviceId: string
  portRange?: string
  maxConcurrency?: number
  timeoutMs?: number
  enableFingerprinting?: boolean
  enableCredentialTest?: boolean
  enableCveLookup?: boolean
  enableConfigAudit?: boolean
}

// ── Manipulation enums ───────────────────────────────────────────────────────

export type ManipulationRuleAction =
  | 'ModifyHeader'
  | 'ModifyBody'
  | 'ModifyStatus'
  | 'Delay'
  | 'Drop'

export type ManipulationPhase = 'Request' | 'Response'

export type ScriptLanguage = 'CSharp' | 'JavaScript'

export type FuzzerStrategy = 'Random' | 'Boundary' | 'BitFlip'

export type FuzzerJobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'

// ── Manipulation models ──────────────────────────────────────────────────────

export interface ManipulationRule {
  id: string
  name: string
  enabled: boolean
  priority: number
  phase: ManipulationPhase
  action: ManipulationRuleAction
  hostPattern?: string
  pathPattern?: string
  methodPattern?: string
  headerName?: string
  headerValue?: string
  bodyReplace?: string
  bodyReplaceWith?: string
  overrideStatusCode?: number
  delayMs?: number
  createdAt: string
  updatedAt: string
}

export interface Breakpoint {
  id: string
  name: string
  enabled: boolean
  language: ScriptLanguage
  phase: ManipulationPhase
  hostPattern?: string
  pathPattern?: string
  scriptCode: string
  createdAt: string
  updatedAt: string
}

export interface ReplaySession {
  id: string
  captureId: string
  method: string
  host: string
  path: string
  requestHeaders: string
  requestBody: string
  responseStatusCode: number
  responseHeaders: string
  responseBody: string
  durationMs: number
  createdAt: string
}

export interface FuzzerJob {
  id: string
  captureId: string
  strategy: FuzzerStrategy
  status: FuzzerJobStatus
  mutationCount: number
  concurrentRequests: number
  completedRequests: number
  anomaliesFound: number
  startedAt?: string
  completedAt?: string
  errorMessage?: string
  createdAt: string
}

export interface FuzzerResult {
  id: string
  fuzzerJobId: string
  mutationIndex: number
  mutatedBody: string
  responseStatusCode: number
  responseBody: string
  durationMs: number
  isAnomaly: boolean
  anomalyReason?: string
  createdAt: string
}

// ── Manipulation request DTOs ────────────────────────────────────────────────

export interface CreateManipulationRuleRequest {
  name: string
  enabled?: boolean
  priority?: number
  phase?: ManipulationPhase
  action: ManipulationRuleAction
  hostPattern?: string
  pathPattern?: string
  methodPattern?: string
  headerName?: string
  headerValue?: string
  bodyReplace?: string
  bodyReplaceWith?: string
  overrideStatusCode?: number
  delayMs?: number
}

export interface UpdateManipulationRuleRequest extends CreateManipulationRuleRequest {
  id: string
}

export interface CreateBreakpointRequest {
  name: string
  enabled?: boolean
  language: ScriptLanguage
  phase?: ManipulationPhase
  hostPattern?: string
  pathPattern?: string
  scriptCode: string
}

export interface UpdateBreakpointRequest extends CreateBreakpointRequest {
  id: string
}

export interface CreateReplayRequest {
  captureId: string
  method?: string
  host?: string
  path?: string
  requestHeaders?: string
  requestBody?: string
}

export interface StartFuzzerRequest {
  captureId: string
  strategy?: FuzzerStrategy
  mutationCount?: number
  concurrentRequests?: number
}

// ── OpenRTB ──────────────────────────────────────────────────────────────────

export type OpenRtbMessageType = 'BidRequest' | 'BidResponse'

export type PiiRedactionStrategy =
  | 'Redact'
  | 'TruncateIp'
  | 'HashSha256'
  | 'GeneralizeGeo'
  | 'GeneralizeUserAgent'
  | 'Remove'

export interface OpenRtbEvent {
  id: string
  capturedRequestId: string
  version: string
  messageType: OpenRtbMessageType
  impressionCount: number
  bidCount: number
  hasDeviceInfo: boolean
  hasUserData: boolean
  hasGeoData: boolean
  exchange: string
  seatBids?: string
  rawJson: string
  detectedAt: string
}

export interface OpenRtbPiiPolicy {
  id: string
  enabled: boolean
  fieldPath: string
  strategy: PiiRedactionStrategy
  hostPattern?: string
  priority: number
  createdAt: string
  updatedAt: string
}

export interface CreatePiiPolicyRequest {
  fieldPath: string
  strategy: PiiRedactionStrategy
  enabled?: boolean
  hostPattern?: string
  priority?: number
}

export interface UpdatePiiPolicyRequest {
  fieldPath?: string
  strategy?: PiiRedactionStrategy
  enabled?: boolean
  hostPattern?: string
  priority?: number
}

export interface PiiStrippingLog {
  id: string
  capturedRequestId: string
  host: string
  path: string
  fieldPath: string
  strategy: PiiRedactionStrategy
  originalValueHash: string
  redactedPreview: string
  phase: string
  strippedAt: string
}

export interface PiiAuditStats {
  totalStripped: number
  byFieldPath: Record<string, number>
  byHost: Record<string, number>
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
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

// ── Packet Capture types ──────────────────────────────────────────────────────

export interface NetworkDevice {
  name: string
  displayName: string
  ipAddresses: string[]
  macAddress?: string
  isLoopback: boolean
  isUp: boolean
  isRunning: boolean
}

export interface CapturedPacket {
  id: string
  timestamp: string
  sourceIp: string
  destinationIp: string
  sourcePort: number
  destinationPort: number
  protocol: string
  length: number
  payloadPreview?: string
  tcpFlags?: string
  isError?: boolean
  isRetransmission?: boolean
}

