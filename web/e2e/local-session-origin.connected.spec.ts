import { expect, test } from '@playwright/test'

test('local loopback address can start the deterministic browser session', async ({ page }) => {
  await page.goto('http://127.0.0.1:3017/sign-in')
  await expect(page.getByRole('button', { name: /Continue to Advertified/ })).toBeVisible()

  const sessionStarted = page.waitForResponse(response =>
    response.url().endsWith('/api/v1/session') && response.request().method() === 'POST')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()

  const response = await sessionStarted
  expect(response.status()).toBe(200)
  await expect(page.getByText('Open Advertified from its approved address.')).toHaveCount(0)
})
