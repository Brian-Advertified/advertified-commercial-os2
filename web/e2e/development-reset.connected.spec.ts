import { expect, test } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000002'

test('authorised development reset leaves an empty Brief workspace and usable inventory', async ({ page }) => {
  test.skip(process.env.ADVERTIFIED_VERIFY_EMPTY_BRIEFS !== 'true',
    'Run only immediately after the explicitly authorised development reset.')
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await page.goto('/briefs')
  await expect(page.getByRole('heading', { name: 'Briefs', exact: true })).toBeVisible()
  const briefs = await page.request.get(`/api/v1/tenants/${tenantId}/briefs`)
  expect(briefs.status()).toBe(200)
  expect(await briefs.json()).toEqual([])
  await page.getByRole('link', { name: 'Create new Brief', exact: true }).first().click()
  await expect(page).toHaveURL(/\/briefs\/new$/)
  await expect(page.getByRole('heading', { name: 'Start with the Brief, not a form' })).toBeVisible()
  await page.goto('/inventory')
  await expect(page.getByRole('heading', { name: 'Media inventory', exact: true })).toBeVisible()
  await expect(page.locator('.approved-inventory-card').first()).toBeVisible()
  const summary = await page.request.get('/api/v1/public/inventory-summary')
  expect(summary.status()).toBe(200)
  const inventory = await summary.json() as {
    totalCount: number; channels: { channel: string; count: number; countBasis: string; units: unknown[] }[]
  }
  expect(inventory.totalCount).toBeGreaterThan(0)
  expect(inventory.channels.some(channel => channel.units.length > 0)).toBe(true)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  await page.goto('/')
  await expect(page.getByText('Current media counts are temporarily unavailable.')).toHaveCount(0)
  for (const channel of inventory.channels) {
    expect(channel.count).toBe(channel.units.length)
    const card = page.locator(`.public-inventory-card--${channel.channel}`)
    await expect(card.locator('strong')).toHaveText(channel.count.toLocaleString())
  }
  await expect(page.locator('.media-inventory-partners')).toBeVisible()
})
