import { expect, test, type Page } from '@playwright/test'

const BRIEF_VERSION_ID = 'c4fcbe9d-77ca-4367-b2f8-f72f0d4a8c04'

test('Rayetsa STP shows governed age and socio-economic market context', async ({ page }) => {
  await signIn(page)
  await page.goto(`/stp/${BRIEF_VERSION_ID}`)
  await expect(page.getByRole('tab', { name: 'Demographics' })).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByText('Age Distribution', { exact: true })).toBeVisible()
  await expect(page.getByText('Age distribution requires approved audience research.', { exact: true })).toHaveCount(0)
  await expect(page.getByText('15+ market context', { exact: true })).toBeVisible()
  await expect(page.getByText('Socio-economic Context', { exact: true })).toBeVisible()
  await expect(page.getByText('Labour-force context', { exact: true })).toBeVisible()
  await expect(page.getByText('LSM / SEM distribution requires governed segment evidence.', { exact: true })).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'Channel Planning Signal Matrix' })).toBeVisible()
  await expect(page.getByText('Geo-ready', { exact: true }).first()).toBeVisible()
  await expect(page.getByText(/% access/).first()).toBeVisible()
  await expect(page.getByText('Needs evidence', { exact: true }).first()).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Multi-Channel Audience Relevance Matrix' })).toHaveCount(0)
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
