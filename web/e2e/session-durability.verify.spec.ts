import { expect, test } from '@playwright/test'
import { restoreDurableSessionCookies } from './support/session-durability'

const connectedOrigin = process.env.ADVERTIFIED_CONNECTED_ORIGIN ?? 'http://localhost:3017'

test('durable browser session survives API restart and logout invalidates it', async ({ page }) => {
  await restoreDurableSessionCookies(page.context())

  const statusResponse = await page.request.get('/api/v1/session')
  expect(statusResponse.status(), await statusResponse.text()).toBe(200)
  const status = await statusResponse.json() as {
    authenticated: boolean
    antiforgeryToken: string
  }
  expect(status.authenticated).toBe(true)

  const logout = await page.request.delete('/api/v1/session', {
    headers: {
      Origin: connectedOrigin,
      'X-CSRF-TOKEN': status.antiforgeryToken,
    },
  })
  expect(logout.status(), await logout.text()).toBe(204)

  const afterLogout = await page.request.get('/api/v1/session')
  expect(afterLogout.status(), await afterLogout.text()).toBe(200)
  expect((await afterLogout.json() as { authenticated: boolean }).authenticated).toBe(false)
})
