import { expect, test } from '@playwright/test'
import {
  durableSessionStatePath,
  prepareDurableSessionStatePath,
} from './support/session-durability'

test('seed durable browser session before API restart', async ({ page }) => {
  await prepareDurableSessionStatePath()
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()

  const response = await page.request.get('/api/v1/session')
  expect(response.status(), await response.text()).toBe(200)
  const session = await response.json() as { authenticated: boolean }
  expect(session.authenticated).toBe(true)
  await page.context().storageState({ path: durableSessionStatePath })
})
