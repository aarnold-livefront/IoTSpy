import type { Page, Route } from '@playwright/test'
import {
  ALL_CAPTURES,
  ALL_DEVICES,
  MOCK_JWT,
  PROXY_STATUS_RUNNING,
  PROXY_STATUS_STOPPED,
  SCAN_JOB_CAMERA,
} from '../fixtures/mockData'

type ApiMockOptions = {
  /** Whether the password has been set (controls /api/auth/status response) */
  passwordSet?: boolean
  /** Whether the proxy is currently running */
  proxyRunning?: boolean
}

/**
 * Registers page.route() intercepts for all /api/* endpoints.
 * Must be called before navigation so the intercepts are installed first.
 */
export async function mockApi(page: Page, opts: ApiMockOptions = {}) {
  const { passwordSet = true, proxyRunning = true } = opts

  // Block SignalR WebSocket upgrades gracefully (hub endpoints)
  await page.route('**/hubs/**', (route) => {
    route.abort()
  })

  // ── Auth ────────────────────────────────────────────────────────────────────
  await page.route('**/api/auth/status', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ passwordSet }),
    })
  })

  await page.route('**/api/auth/login', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: MOCK_JWT }),
    })
  })

  await page.route('**/api/auth/setup', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Password set' }),
    })
  })

  // ── Proxy ────────────────────────────────────────────────────────────────────
  await page.route('**/api/proxy/status', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(proxyRunning ? PROXY_STATUS_RUNNING : PROXY_STATUS_STOPPED),
    })
  })

  await page.route('**/api/proxy/start', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isRunning: true, port: 8888 }),
    })
  })

  await page.route('**/api/proxy/stop', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isRunning: false }),
    })
  })

  await page.route('**/api/proxy/settings', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(PROXY_STATUS_RUNNING.settings) })
  })

  // ── Devices ───────────────────────────────────────────────────────────────────
  await page.route('**/api/devices', (route) => {
    if (route.request().method() === 'GET') {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ALL_DEVICES),
      })
    } else {
      route.continue()
    }
  })

  await page.route('**/api/devices/**', (route) => {
    const url = route.request().url()
    const id = url.split('/api/devices/')[1]?.split('?')[0]
    const device = ALL_DEVICES.find((d) => d.id === id)
    if (device) {
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(device) })
    } else {
      route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"Not found"}' })
    }
  })

  // ── Captures ──────────────────────────────────────────────────────────────────
  // Use regex so query strings (?page=1&pageSize=50 etc.) are also matched
  await page.route(/\/api\/captures(\?|$)/, async (route) => {
    if (route.request().method() !== 'GET') { route.continue(); return }
    const url = new URL(route.request().url())
    const deviceId = url.searchParams.get('deviceId')
    const host = url.searchParams.get('host')
    const method = url.searchParams.get('method')

    let filtered = ALL_CAPTURES
    if (deviceId) filtered = filtered.filter((c) => c.deviceId === deviceId)
    if (host) filtered = filtered.filter((c) => c.host.includes(host))
    if (method) filtered = filtered.filter((c) => c.method === method)

    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: filtered, total: filtered.length, page: 1, pageSize: 50, pages: 1 }),
    })
  })

  await page.route('**/api/captures/**', (route) => {
    const url = route.request().url()
    const id = url.split('/api/captures/')[1]?.split('?')[0]
    const capture = ALL_CAPTURES.find((c) => c.id === id)
    if (capture) {
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(capture) })
    } else {
      route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"Not found"}' })
    }
  })

  // ── Scanner ───────────────────────────────────────────────────────────────────
  await page.route('**/api/scanner/jobs', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([SCAN_JOB_CAMERA]),
    })
  })

  await page.route('**/api/scanner/jobs/**', (route) => {
    const url = route.request().url()
    if (url.includes('/findings')) {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SCAN_JOB_CAMERA.findings),
      })
    } else {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SCAN_JOB_CAMERA),
      })
    }
  })

  // ── Certificates ───────────────────────────────────────────────────────────────
  await page.route('**/api/certificates', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })

  await page.route('**/api/certificates/**', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })

  // ── Manipulation ──────────────────────────────────────────────────────────────
  await page.route('**/api/manipulation/**', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })

  // ── Packet capture ────────────────────────────────────────────────────────────
  await page.route('**/api/packet-capture/**', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })

  // ── OpenRTB ───────────────────────────────────────────────────────────────────
  await page.route('**/api/openrtb/**', (route) => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })
}

/**
 * Performs the full login flow by mocking the auth check and using the mock JWT.
 * After calling this, the app will be in an authenticated state on the dashboard.
 */
export async function loginAsMockUser(page: Page, opts: ApiMockOptions = {}) {
  await mockApi(page, { passwordSet: true, ...opts })
  await page.goto('/')
  // Wait for redirect to /login (auth status check)
  await page.waitForURL('**/login', { timeout: 10_000 })
  // Fill and submit login form
  await page.fill('input[type="password"]', 'test-password')
  await page.click('button[type="submit"]')
  // Wait for dashboard to load
  await page.waitForURL('**/', { timeout: 10_000 })
}
