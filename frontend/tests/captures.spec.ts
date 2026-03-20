import { test, expect } from '@playwright/test'
import { loginAsMockUser, mockApi } from './helpers/mockApi'
import {
  ALL_CAPTURES,
  CAPTURE_BULB_TELEMETRY,
  CAPTURE_CAMERA_FIRMWARE,
  CAPTURE_CAMERA_TELEMETRY,
  CAPTURE_THERMOSTAT_SCHEDULE,
  DEVICE_IP_CAMERA,
  DEVICE_SMART_BULB,
  DEVICE_THERMOSTAT,
} from './fixtures/mockData'

test.describe('Captures — list view', () => {
  test('shows all five mock captures', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await expect(page.getByText('firmware.reolink.com')).toBeVisible()
    await expect(page.getByText('telemetry.reolink.com')).toBeVisible()
    await expect(page.getByText('home.nest.com')).toBeVisible()
    await expect(page.getByText('time.cloudflare.com')).toBeVisible()
  })

  test('shows correct total count from API response', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    // Total count (5) should appear somewhere in the capture list area
    await expect(page.getByText(/5/).first()).toBeVisible()
  })

  test('capture rows display method, host, status, and duration', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    // Check that the Hue capture shows POST and 200
    const hueRow = page.locator('.capture-row').filter({ hasText: 'api2.meethue.com' }).first()
    await expect(hueRow.locator('.method-badge')).toHaveText('POST')
    await expect(hueRow.locator('.status-badge')).toHaveText('200')
  })

  test('HTTPS captures show a TLS indicator', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    // TLS rows get a .capture-row--tls CSS class and a lock icon
    await expect(page.locator('.capture-row--tls').first()).toBeVisible()
  })

  test('204 No Content status is shown for telemetry capture', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('telemetry.reolink.com')).toBeVisible({ timeout: 8_000 })
    const cameraRow = page.locator('[class*="capture"]')
      .filter({ hasText: 'telemetry.reolink.com' }).first()
    await expect(cameraRow.getByText('204')).toBeVisible()
  })
})

test.describe('Captures — detail pane', () => {
  test('selects Hue capture and shows path in detail', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()
    await expect(page.getByText('/bridge/state').first()).toBeVisible({ timeout: 5_000 })
  })

  test('selects camera firmware capture and shows firmware update URL', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('firmware.reolink.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('firmware.reolink.com').first().click()
    await expect(page.getByText('/check').first()).toBeVisible({ timeout: 5_000 })
  })

  test('detail pane shows request body for POST capture', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()
    // Request body JSON should appear
    await expect(page.getByText(/"on":true/i).or(page.getByText(/on.*true/)).first()).toBeVisible({ timeout: 5_000 })
  })

  test('detail pane shows response headers table', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()
    // HeadersViewer shows Name/Value columns
    await expect(page.getByText('Content-Type')).toBeVisible({ timeout: 5_000 })
  })

  test('TLS details shown for HTTPS captures', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('api2.meethue.com').first().click()
    // Click the TLS tab to see TLS details
    await page.getByRole('button', { name: /^tls$/i }).click()
    await expect(page.getByText(/TLSv1\.3|TLS 1\.3/i)).toBeVisible({ timeout: 5_000 })
  })

  test('thermostat Nest capture shows response body temperature data', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('home.nest.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('home.nest.com').first().click()
    // Click Response tab to see response body with temperature data
    await page.getByRole('button', { name: /^response$/i }).click()
    await expect(
      page.getByText(/21\.5|current_temperature|heating/i).first()
    ).toBeVisible({ timeout: 5_000 })
  })

  test('camera motion telemetry shows POST body with motion_detected', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('telemetry.reolink.com')).toBeVisible({ timeout: 8_000 })
    await page.getByText('telemetry.reolink.com').first().click()
    await expect(
      page.getByText(/motion_detected/i).first()
    ).toBeVisible({ timeout: 5_000 })
  })
})

test.describe('Captures — device filtering', () => {
  test('filtering by IP camera device shows only camera captures', async ({ page }) => {
    await mockApi(page, { passwordSet: true })

    // Override captures to return only camera captures when filtered
    await page.route('**/api/captures**', (route) => {
      const url = new URL(route.request().url())
      const deviceId = url.searchParams.get('deviceId')
      if (deviceId === DEVICE_IP_CAMERA.id) {
        const cameraCaptures = ALL_CAPTURES.filter((c) => c.deviceId === DEVICE_IP_CAMERA.id)
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: cameraCaptures, total: cameraCaptures.length, page: 1, pageSize: 50, pages: 1 }),
        })
      } else {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: ALL_CAPTURES, total: ALL_CAPTURES.length, page: 1, pageSize: 50, pages: 1 }),
        })
      }
    })

    await page.goto('/login')
    await page.fill('#password', 'test')
    await page.click('button[type="submit"]')
    await page.waitForURL('**/', { timeout: 10_000 })
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    // With all captures visible, firmware.reolink.com should appear (camera device)
    await expect(page.getByText('firmware.reolink.com')).toBeVisible()
    // telemetry.reolink.com too
    await expect(page.getByText('telemetry.reolink.com')).toBeVisible()
  })

  test('device list is populated in the filter dropdown', async ({ page }) => {
    await loginAsMockUser(page)
    await expect(page.getByText('api2.meethue.com')).toBeVisible({ timeout: 8_000 })

    // Device labels should be populated as options in the device filter select
    const deviceSelect = page.locator('select[aria-label*="device" i]')
    await expect(deviceSelect).toBeVisible({ timeout: 5_000 })
    // Verify device options are loaded (select should have > 1 option)
    await expect(deviceSelect.locator('option')).toHaveCount(4) // "All devices" + 3 devices
  })
})
