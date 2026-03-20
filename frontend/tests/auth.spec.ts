import { test, expect } from '@playwright/test'
import { mockApi } from './helpers/mockApi'

test.describe('Authentication flows', () => {
  test('first-run: redirects to /setup when no password is set', async ({ page }) => {
    await mockApi(page, { passwordSet: false })
    await page.goto('/')
    await page.waitForURL('**/setup', { timeout: 10_000 })
    await expect(page.locator('.auth-title')).toHaveText('IoTSpy')
    await expect(page.getByText('Create your admin password')).toBeVisible()
  })

  test('setup: shows validation error for short password', async ({ page }) => {
    await mockApi(page, { passwordSet: false })
    await page.goto('/setup')
    await page.fill('#password', 'short')
    await page.fill('#confirm', 'short')
    await page.click('button[type="submit"]')
    await expect(page.getByText('Password must be at least 8 characters')).toBeVisible()
  })

  test('setup: shows error when passwords do not match', async ({ page }) => {
    await mockApi(page, { passwordSet: false })
    await page.goto('/setup')
    await page.fill('#password', 'validpassword1')
    await page.fill('#confirm', 'differentpassword1')
    await page.click('button[type="submit"]')
    await expect(page.getByText('Passwords do not match')).toBeVisible()
  })

  test('setup: successful setup navigates to /login', async ({ page }) => {
    await mockApi(page, { passwordSet: false })
    await page.goto('/setup')
    await page.fill('#password', 'securepass123')
    await page.fill('#confirm', 'securepass123')
    await page.click('button[type="submit"]')
    await page.waitForURL('**/login', { timeout: 10_000 })
    await expect(page.getByText('Sign in to your dashboard')).toBeVisible()
  })

  test('login: redirects to /login when password is set but not authenticated', async ({ page }) => {
    await mockApi(page, { passwordSet: true })
    await page.goto('/')
    await page.waitForURL('**/login', { timeout: 10_000 })
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible()
  })

  test('login: shows error for wrong password', async ({ page }) => {
    await mockApi(page, { passwordSet: true })
    // Override login to return 401
    await page.route('**/api/auth/login', (route) => {
      route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid credentials' }),
      })
    })
    await page.goto('/login')
    await page.fill('#password', 'wrongpassword')
    await page.click('button[type="submit"]')
    await expect(page.getByText('Invalid credentials')).toBeVisible()
  })

  test('login: successful login redirects to dashboard', async ({ page }) => {
    await mockApi(page, { passwordSet: true })
    await page.goto('/login')
    await page.fill('#password', 'correctpassword')
    await page.click('button[type="submit"]')
    await page.waitForURL('**/', { timeout: 10_000 })
    // Dashboard should be visible
    await expect(page.getByRole('button', { name: /list/i })).toBeVisible()
  })

  test('login: loading state shown while submitting', async ({ page }) => {
    await mockApi(page, { passwordSet: true })
    // Delay login response to see loading state
    await page.route('**/api/auth/login', async (route) => {
      await new Promise((r) => setTimeout(r, 300))
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'tok' }) })
    })
    await page.goto('/login')
    await page.fill('#password', 'anypassword')
    await page.click('button[type="submit"]')
    await expect(page.getByRole('button', { name: /signing in/i })).toBeVisible()
  })
})
