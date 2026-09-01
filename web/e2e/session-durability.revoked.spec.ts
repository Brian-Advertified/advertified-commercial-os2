import { expect, test } from '@playwright/test'
import {
  removeDurableSessionState,
  restoreDurableSessionCookies,
} from './support/session-durability'

test('invalidated browser session stays invalid after API restart', async ({ page }) => {
  await restoreDurableSessionCookies(page.context())
  const response = await page.request.get('/api/v1/session')
  expect(response.status(), await response.text()).toBe(200)
  expect((await response.json() as { authenticated: boolean }).authenticated).toBe(false)
  await removeDurableSessionState()
})
