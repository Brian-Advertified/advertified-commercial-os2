import { expect, test } from '@playwright/test'

const BRIEF_VERSION_ID = '3817c516-085c-47ff-8639-bf042e23c8e6'

test('current manual Rayetsa run opens the generated Audience & STP workspace', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)

  await page.goto(`/stp/${BRIEF_VERSION_ID}`)
  await expect(page.getByRole('heading', { name: 'Audience & STP' })).toBeVisible()
  await expect(page.locator('.connected-audience-summary-card').first()).toBeVisible()
  await expect(page.getByRole('region').filter({ hasText: /Channel Planning Signal Matrix/i }).or(
    page.locator('.connected-audience-relevance'))).toBeVisible()
  await expect(page.getByRole('alert')).toHaveCount(0)
})
