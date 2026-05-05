import { test, expect } from '@playwright/test'
import { loginAsMockUser, mockApi } from './helpers/mockApi'

const MOCK_RULES = [
  {
    id: 'rule-1', name: 'Block Analytics', isEnabled: true, priority: 1,
    hostPattern: 'analytics\\.', pathPattern: null, methodPattern: null,
    action: 'Drop', phase: 'Request',
    headerName: null, headerValue: null, bodyReplace: null, bodyReplaceWith: null,
    overrideStatusCode: null, delayMs: null,
  },
]

test.describe('Manipulation panel', () => {
  test.beforeEach(async ({ page }) => {
    // Override manipulation endpoints to return real data
    await mockApi(page)

    // Patch manipulation rules with a real rule
    await page.route(/\/api\/manipulation\/rules(\?|$)/, (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_RULES, total: 1, page: 1, pageSize: 50, pages: 1 }),
      })
    })

    await loginAsMockUser(page)
    // Navigate to the Manipulation panel
    await page.getByRole('button', { name: /manipulation/i }).click()
  })

  test('shows all seven tab labels', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Traffic Rules' })).toBeVisible({ timeout: 8_000 })
    await expect(page.getByRole('button', { name: 'Breakpoints' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Replay' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Fuzzer' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Content Rules' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Assets' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'API Spec' })).toBeVisible()
  })

  test('Traffic Rules tab lists mock rule', async ({ page }) => {
    await page.getByRole('button', { name: 'Traffic Rules' }).click()
    await expect(page.getByText('Block Analytics')).toBeVisible({ timeout: 8_000 })
  })

  test('switching to Breakpoints tab changes content', async ({ page }) => {
    await page.getByRole('button', { name: 'Traffic Rules' }).click()
    await expect(page.getByText('Block Analytics')).toBeVisible({ timeout: 8_000 })
    await page.getByRole('button', { name: 'Breakpoints' }).click()
    // Block Analytics rule should no longer be visible (different tab)
    await expect(page.getByText('Block Analytics')).not.toBeVisible()
  })
})
