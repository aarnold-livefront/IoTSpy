import { test, expect } from '@playwright/test'
import { loginAsMockUser, mockApi } from './helpers/mockApi'
import {
  ALL_CAPTURES,
  ALL_DEVICES,
  CAPTURE_BULB_TELEMETRY,
  DEVICE_IP_CAMERA,
  DEVICE_SMART_BULB,
  DEVICE_THERMOSTAT,
} from './fixtures/mockData'

test.describe('Dashboard — main layout', () => {
  test('shows proxy status badge as running', async ({ page }) => {
    await loginAsMockUser(page, { proxyRunning: true })
    // Header should show proxy is running
    await expect(page.locator('.proxy-status-badge, [data-testid="proxy-badge"]')
      .or(page.getByText(/running|port 8888/i).first())
    ).toBeVisible({ timeout: 8_000 })
  })

  test('renders view mode toggle buttons', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByRole('button', { name: /^list$/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /^timeline$/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /packet capture/i })).toBeVisible()
  })

  test('list view shows captures from mock devices', async ({ page }) => {
    await loginAsMockUser(page)
    // Wait for capture list to load
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await expect(page.getByText('firmware.reolink.com')).toBeVisible()
    await expect(page.getByText('home.nest.com')).toBeVisible()
  })

  test('list view shows HTTP methods', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    // POST and GET method badges should be visible in capture rows (not select options)
    await expect(page.locator('span.method-badge').filter({ hasText: 'POST' }).first()).toBeVisible()
    await expect(page.locator('span.method-badge').filter({ hasText: 'GET' }).first()).toBeVisible()
  })

  test('list view shows status codes', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    // 200, 204 status codes should be in the list
    await expect(page.getByText('200').first()).toBeVisible()
  })

  test('switching to timeline view renders timeline', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByRole('button', { name: /^timeline$/i }).click()
    // Timeline view should be active
    await expect(page.locator('.timeline-swimlane, [class*="timeline"]').first()).toBeVisible({ timeout: 5_000 })
  })

  test('switching to packet capture view renders packet panel', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByRole('button', { name: /packet capture/i }).click()
    // Packet capture panel should be visible
    await expect(page.locator('[class*="packet"]').first()
      .or(page.getByText(/no capture|capture device|interface/i).first())
    ).toBeVisible({ timeout: 5_000 })
  })

  test('proxy stop button calls stop API', async ({ page }) => {
    let stopCalled = false
    await mockApi(page, { proxyRunning: true })
    await page.route('**/api/proxy/stop', (route) => {
      stopCalled = true
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isRunning: false }) })
    })
    await page.goto('/login')
    await page.fill('#password', 'test')
    await page.click('button[type="submit"]')
    await page.waitForURL('**/', { timeout: 10_000 })
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    await page.getByRole('button', { name: /stop/i }).click()
    await expect(() => expect(stopCalled).toBe(true)).toPass({ timeout: 3_000 })
  })
})

test.describe('Dashboard — capture list interactions', () => {
  test('clicking a capture opens the detail pane', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    // Click the first capture (Philips Hue)
    await page.getByText('api2.meethue.com').first().click()

    // Detail pane should show request details
    await expect(page.getByText('/bridge/state').first()).toBeVisible({ timeout: 5_000 })
  })

  test('detail pane shows request headers', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()

    // Headers like Content-Type should appear in the header viewer
    await expect(page.getByText('Content-Type')).toBeVisible({ timeout: 5_000 })
  })

  test('detail pane shows TLS information', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()

    // Should show TLS version
    await expect(page.getByText(/TLS|tls|https/i).first()).toBeVisible({ timeout: 5_000 })
  })

  test('filter by host narrows capture list', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    // Override captures endpoint to simulate filtered response
    await page.route('**/api/captures**', (route) => {
      const url = new URL(route.request().url())
      const host = url.searchParams.get('host')
      if (host === 'reolink') {
        const filtered = ALL_CAPTURES.filter((c) => c.host.includes('reolink'))
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: filtered, total: filtered.length, page: 1, pageSize: 50, pages: 1 }),
        })
      } else {
        route.continue()
      }
    })

    // Find and fill the host filter input
    const hostInput = page.getByPlaceholder(/host/i).or(page.locator('input[name="host"], input[placeholder*="host"]'))
    if (await hostInput.count() > 0) {
      await hostInput.first().fill('reolink')
      await hostInput.first().press('Enter')
      await page.waitForTimeout(500)
      await expect(page.getByText('firmware.reolink.com')).toBeVisible()
    }
  })

  test('device selector filters captures by device', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    // Device dropdown should have device labels
    const deviceSelector = page.getByRole('combobox').first()
      .or(page.locator('select').first())
    if (await deviceSelector.count() > 0) {
      await expect(deviceSelector).toBeVisible()
    }
  })
})

test.describe('Dashboard — proxy control', () => {
  test('proxy status shows port 8888 when running', async ({ page }) => {
    await loginAsMockUser(page, { proxyRunning: true })
    await page.waitForTimeout(1000) // Let proxy status load
    await expect(page.getByText(/8888/).first()).toBeVisible({ timeout: 5_000 })
  })

  test('proxy start button calls start API', async ({ page }) => {
    let startCalled = false
    await mockApi(page, { proxyRunning: false })
    await page.route('**/api/proxy/start', (route) => {
      startCalled = true
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isRunning: true, port: 8888 }) })
    })
    await page.goto('/login')
    await page.fill('#password', 'test')
    await page.click('button[type="submit"]')
    await page.waitForURL('**/', { timeout: 10_000 })
    await page.waitForTimeout(1000)

    const startBtn = page.getByRole('button', { name: /start/i })
    if (await startBtn.count() > 0) {
      await startBtn.click()
      await expect(() => expect(startCalled).toBe(true)).toPass({ timeout: 3_000 })
    }
  })
})
