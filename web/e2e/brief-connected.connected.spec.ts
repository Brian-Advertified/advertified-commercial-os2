import { expect, test } from '@playwright/test'

test('connected clear Brief reaches OOH planning without a fake approval', async ({ page }) => {
  const failedApiResponses: string[] = []
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failedApiResponses.push(`${response.status()} ${new URL(response.url()).pathname}`)
    }
  })

  await page.goto('/sign-in')
  await expect(page.getByRole('heading', {
    name: 'The calm centre of campaign delivery.',
  })).toBeVisible()
  const sessionResponsePromise = page.waitForResponse(response =>
    response.url().endsWith('/api/v1/session') &&
    response.request().method() === 'POST')
  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  const sessionResponse = await sessionResponsePromise
  expect(sessionResponse.status(), await sessionResponse.text()).toBe(200)

  await expect(page.getByRole('heading', {
    name: 'Where are you working today?',
  })).toBeVisible()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()

  await page.goto('/briefs/new')
  await expect(page.getByRole('heading', {
    name: 'Start with the Brief, not a form',
  })).toBeVisible()
  await page.getByLabel('Campaign or Brief name')
    .fill(`Connected OOH Brief ${Date.now()}`)
  await page.getByLabel('Original Brief').fill([
    'Client: Client One',
    'Problem: Local buyers do not know about the new workspace range.',
    'Objective: Generate 500 qualified enquiries.',
    'Audience: Small business owners and office managers.',
    'Geography: Johannesburg and Pretoria.',
    'Timing: 2026-10-01 to 2026-10-31.',
    'Budget: ZAR 100,000 including VAT.',
    'Media: OOH and DOOH only.',
    'Measurement: Qualified enquiries.',
  ].join('\n'))
  await page.getByRole('button', { name: 'Understand this Brief' }).click()

  await expect(page).toHaveURL(/\/planning\/[0-9a-f-]{36}$/, { timeout: 30_000 })
  await expect(page.getByRole('heading', { name: 'Planning workbench' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'OOH and DOOH only' })).toBeVisible()
  await expect(page.getByRole('button', { name: /Approve Brief/ })).toHaveCount(0)

  await page.getByRole('link', { name: /Back to Brief/ }).click()
  await expect(page.getByText('Ready', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Confirm this Brief' })).toHaveCount(0)
  expect(failedApiResponses).toEqual([])
})
