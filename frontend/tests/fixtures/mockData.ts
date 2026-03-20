/**
 * Mock fixtures simulating a realistic IoTSpy deployment with
 * three IoT devices generating varied HTTP/HTTPS traffic.
 */

// ── Devices ──────────────────────────────────────────────────────────────────

export const DEVICE_SMART_BULB = {
  id: 'aaaaaaaa-0001-0000-0000-000000000001',
  ipAddress: '192.168.1.101',
  macAddress: 'AA:BB:CC:11:22:33',
  hostname: 'smart-bulb-01.local',
  vendor: 'Philips Hue',
  label: 'Living Room Bulb',
  notes: 'Connected via bridge',
  interceptionEnabled: true,
  firstSeen: '2026-03-01T10:00:00Z',
  lastSeen: '2026-03-20T12:00:00Z',
  securityScore: 72,
}

export const DEVICE_IP_CAMERA = {
  id: 'bbbbbbbb-0002-0000-0000-000000000002',
  ipAddress: '192.168.1.102',
  macAddress: 'AA:BB:CC:44:55:66',
  hostname: 'ipcam-front-door.local',
  vendor: 'Reolink',
  label: 'Front Door Camera',
  notes: 'Outdoor 4K camera',
  interceptionEnabled: true,
  firstSeen: '2026-03-01T10:05:00Z',
  lastSeen: '2026-03-20T12:00:00Z',
  securityScore: 45,
}

export const DEVICE_THERMOSTAT = {
  id: 'cccccccc-0003-0000-0000-000000000003',
  ipAddress: '192.168.1.103',
  macAddress: 'AA:BB:CC:77:88:99',
  hostname: 'nest-thermostat.local',
  vendor: 'Google Nest',
  label: 'Hallway Thermostat',
  notes: '',
  interceptionEnabled: true,
  firstSeen: '2026-03-02T09:00:00Z',
  lastSeen: '2026-03-20T12:00:00Z',
  securityScore: 88,
}

export const ALL_DEVICES = [DEVICE_SMART_BULB, DEVICE_IP_CAMERA, DEVICE_THERMOSTAT]

// ── Captures ─────────────────────────────────────────────────────────────────

export const CAPTURE_BULB_TELEMETRY = {
  id: '11111111-aaaa-0000-0000-capture00001',
  deviceId: DEVICE_SMART_BULB.id,
  device: DEVICE_SMART_BULB,
  method: 'POST',
  scheme: 'https',
  host: 'api2.meethue.com',
  port: 443,
  path: '/bridge/state',
  query: '',
  requestHeaders: JSON.stringify({
    'Content-Type': 'application/json',
    'Authorization': 'Bearer hue-token-xyz',
    'User-Agent': 'Hue/2.5.1 (iOS)',
  }),
  requestBody: JSON.stringify({ on: true, bri: 200, hue: 8417, sat: 140 }),
  requestBodySize: 48,
  statusCode: 200,
  statusMessage: 'OK',
  responseHeaders: JSON.stringify({
    'Content-Type': 'application/json',
    'X-Request-Id': 'req-abc-123',
  }),
  responseBody: JSON.stringify([{ success: { '/lights/1/state/on': true } }]),
  responseBodySize: 52,
  isTls: true,
  tlsVersion: 'TLSv1.3',
  tlsCipherSuite: 'TLS_AES_256_GCM_SHA384',
  protocol: 'Https',
  timestamp: '2026-03-20T11:45:00Z',
  durationMs: 142,
  clientIp: '192.168.1.101',
  isModified: false,
  notes: '',
}

export const CAPTURE_CAMERA_FIRMWARE = {
  id: '22222222-bbbb-0000-0000-capture00002',
  deviceId: DEVICE_IP_CAMERA.id,
  device: DEVICE_IP_CAMERA,
  method: 'GET',
  scheme: 'https',
  host: 'firmware.reolink.com',
  port: 443,
  path: '/check',
  query: '?model=RLC-810A&version=3.0.0.2356',
  requestHeaders: JSON.stringify({
    'User-Agent': 'Reolink/3.0 (Linux armv7l)',
    'Accept': 'application/json',
  }),
  requestBody: '',
  requestBodySize: 0,
  statusCode: 200,
  statusMessage: 'OK',
  responseHeaders: JSON.stringify({
    'Content-Type': 'application/json',
    'Cache-Control': 'no-cache',
  }),
  responseBody: JSON.stringify({ updateAvailable: true, version: '3.1.0.2450', url: 'https://firmware.reolink.com/RLC-810A_3.1.0.zip' }),
  responseBodySize: 96,
  isTls: true,
  tlsVersion: 'TLSv1.3',
  tlsCipherSuite: 'TLS_AES_128_GCM_SHA256',
  protocol: 'Https',
  timestamp: '2026-03-20T11:44:00Z',
  durationMs: 287,
  clientIp: '192.168.1.102',
  isModified: false,
  notes: '',
}

export const CAPTURE_CAMERA_TELEMETRY = {
  id: '33333333-cccc-0000-0000-capture00003',
  deviceId: DEVICE_IP_CAMERA.id,
  device: DEVICE_IP_CAMERA,
  method: 'POST',
  scheme: 'https',
  host: 'telemetry.reolink.com',
  port: 443,
  path: '/events',
  query: '',
  requestHeaders: JSON.stringify({
    'Content-Type': 'application/json',
    'X-Device-Id': 'reolink-front-door',
  }),
  requestBody: JSON.stringify({ event: 'motion_detected', timestamp: Date.now(), zone: 'full_frame', confidence: 0.91 }),
  requestBodySize: 82,
  statusCode: 204,
  statusMessage: 'No Content',
  responseHeaders: JSON.stringify({}),
  responseBody: '',
  responseBodySize: 0,
  isTls: true,
  tlsVersion: 'TLSv1.3',
  tlsCipherSuite: 'TLS_AES_128_GCM_SHA256',
  protocol: 'Https',
  timestamp: '2026-03-20T11:43:30Z',
  durationMs: 98,
  clientIp: '192.168.1.102',
  isModified: false,
  notes: '',
}

export const CAPTURE_THERMOSTAT_SCHEDULE = {
  id: '44444444-dddd-0000-0000-capture00004',
  deviceId: DEVICE_THERMOSTAT.id,
  device: DEVICE_THERMOSTAT,
  method: 'GET',
  scheme: 'https',
  host: 'home.nest.com',
  port: 443,
  path: '/api/0.1/user/abc123/app_launch',
  query: '',
  requestHeaders: JSON.stringify({
    'Authorization': 'Basic nest-auth-token',
    'User-Agent': 'Nest/5.9.3 (iOS 17.4)',
    'Accept': 'application/json',
  }),
  requestBody: '',
  requestBodySize: 0,
  statusCode: 200,
  statusMessage: 'OK',
  responseHeaders: JSON.stringify({
    'Content-Type': 'application/json',
    'Strict-Transport-Security': 'max-age=31536000',
  }),
  responseBody: JSON.stringify({
    current_temperature: 21.5,
    target_temperature: 22.0,
    hvac_state: 'heating',
    schedule_mode: 'HOME',
  }),
  responseBodySize: 110,
  isTls: true,
  tlsVersion: 'TLSv1.3',
  tlsCipherSuite: 'TLS_AES_256_GCM_SHA384',
  protocol: 'Https',
  timestamp: '2026-03-20T11:42:00Z',
  durationMs: 201,
  clientIp: '192.168.1.103',
  isModified: false,
  notes: '',
}

export const CAPTURE_THERMOSTAT_DNS = {
  id: '55555555-eeee-0000-0000-capture00005',
  deviceId: DEVICE_THERMOSTAT.id,
  device: DEVICE_THERMOSTAT,
  method: 'GET',
  scheme: 'http',
  host: 'time.cloudflare.com',
  port: 80,
  path: '/cdn-cgi/trace',
  query: '',
  requestHeaders: JSON.stringify({
    'User-Agent': 'Nest/5.9.3',
  }),
  requestBody: '',
  requestBodySize: 0,
  statusCode: 200,
  statusMessage: 'OK',
  responseHeaders: JSON.stringify({
    'Content-Type': 'text/plain',
  }),
  responseBody: 'fl=123\nip=192.168.1.103\nts=1742000000\n',
  responseBodySize: 40,
  isTls: false,
  tlsVersion: '',
  tlsCipherSuite: '',
  protocol: 'Http',
  timestamp: '2026-03-20T11:41:00Z',
  durationMs: 45,
  clientIp: '192.168.1.103',
  isModified: false,
  notes: '',
}

export const ALL_CAPTURES = [
  CAPTURE_BULB_TELEMETRY,
  CAPTURE_CAMERA_FIRMWARE,
  CAPTURE_CAMERA_TELEMETRY,
  CAPTURE_THERMOSTAT_SCHEDULE,
  CAPTURE_THERMOSTAT_DNS,
]

// ── Proxy settings ────────────────────────────────────────────────────────────

export const PROXY_SETTINGS = {
  id: 1,
  proxyPort: 8888,
  mode: 'ExplicitProxy',
  isRunning: true,
  captureTls: true,
  captureRequestBodies: true,
  captureResponseBodies: true,
  maxBodySizeKb: 1024,
  listenAddress: '0.0.0.0',
  passwordHash: 'hashed',
  transparentProxyPort: 9999,
  targetDeviceIp: '',
  gatewayIp: '',
  networkInterface: '',
}

export const PROXY_STATUS_RUNNING = {
  isRunning: true,
  port: 8888,
  settings: PROXY_SETTINGS,
}

export const PROXY_STATUS_STOPPED = {
  isRunning: false,
  port: 8888,
  settings: { ...PROXY_SETTINGS, isRunning: false },
}

// ── Auth ──────────────────────────────────────────────────────────────────────

// A compact but valid-structure JWT for mock use
export const MOCK_JWT =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
  'eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJhZG1pbiIsImV4cCI6OTk5OTk5OTk5OX0.' +
  'mock_signature_placeholder'

// ── Scan jobs ─────────────────────────────────────────────────────────────────

export const SCAN_JOB_CAMERA = {
  id: 'scanjob-0001-0000-0000-000000000001',
  deviceId: DEVICE_IP_CAMERA.id,
  targetIp: DEVICE_IP_CAMERA.ipAddress,
  portRange: '1-1024',
  maxConcurrency: 100,
  timeoutMs: 3000,
  enableFingerprinting: true,
  enableCredentialTest: true,
  enableCveLookup: true,
  enableConfigAudit: true,
  status: 'Completed',
  totalFindings: 3,
  createdAt: '2026-03-20T10:00:00Z',
  startedAt: '2026-03-20T10:00:01Z',
  completedAt: '2026-03-20T10:01:35Z',
  errorMessage: null,
  findings: [
    {
      id: 'finding-0001',
      scanJobId: 'scanjob-0001-0000-0000-000000000001',
      type: 'OpenPort',
      severity: 'Medium',
      title: 'Port 80 open (HTTP)',
      description: 'Unencrypted HTTP admin panel exposed on port 80',
      evidence: 'HTTP/1.1 200 OK — Reolink Admin Panel',
      remediation: 'Disable HTTP interface or redirect to HTTPS',
      createdAt: '2026-03-20T10:01:00Z',
    },
    {
      id: 'finding-0002',
      scanJobId: 'scanjob-0001-0000-0000-000000000001',
      type: 'DefaultCredential',
      severity: 'Critical',
      title: 'Default credentials accepted',
      description: 'admin:admin login succeeded on HTTP admin interface',
      evidence: 'HTTP 200 with session cookie set',
      remediation: 'Change default password immediately',
      createdAt: '2026-03-20T10:01:05Z',
    },
    {
      id: 'finding-0003',
      scanJobId: 'scanjob-0001-0000-0000-000000000001',
      type: 'CveMatch',
      severity: 'High',
      title: 'CVE-2021-40494 — Remote code execution',
      description: 'Firmware version 3.0.0.2356 is vulnerable to unauthenticated RCE',
      evidence: 'Fingerprint matches affected version range',
      remediation: 'Update firmware to 3.1.0.2450 or later',
      createdAt: '2026-03-20T10:01:10Z',
    },
  ],
}
