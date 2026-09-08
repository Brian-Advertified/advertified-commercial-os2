import { expect, test } from '@playwright/test'

const seededBriefVersionId = 'b32a2e9d-f6c7-4a03-bfcd-1788802ad8b9'

test('seeded Audience Strategy is actionable and has no dead section links', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()

  await page.goto(`/stp/${seededBriefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Audience Strategy' })).toBeVisible()
  await expect(page.getByText('Audience discovery and human validation')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Segmentation', exact: true })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Targeting', exact: true })).toHaveCount(0)
  await expect(page.locator('a[href*="#stp-"]')).toHaveCount(0)
  await expect(page.locator('.approved-stp-page')).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})
