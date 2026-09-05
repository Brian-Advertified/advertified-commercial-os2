import { expect, test } from '@playwright/test'
import { installCampaignDeliveryApi } from './support/campaign-delivery-api'
import { campaignFixture, deliveryIds } from './support/campaign-delivery-data'

test('resource switch hides old campaign and ignores its late mutation completion', async ({ page }) => {
  await page.addInitScript(tenantId => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId }))
  }, deliveryIds.tenant)
  const state = await installCampaignDeliveryApi(page)
  const otherId = 'a1000000-0000-0000-0000-000000000002'
  let releaseMutation = () => {}
  let releaseRead = () => {}
  const mutationPending = new Promise<void>(resolve => { releaseMutation = resolve })
  const readPending = new Promise<void>(resolve => { releaseRead = resolve })
  await page.route(`**/campaigns/${deliveryIds.campaign}:confirm-bookings`, async route => {
    await mutationPending
    await route.fulfill({ json: campaignFixture(state), headers: { ETag: '"2"' } })
  })
  await page.route(`**/campaigns/${otherId}`, async route => {
    await readPending
    await route.fulfill({ json: { ...campaignFixture(state), id: otherId, title: 'Second campaign' } })
  })
  try {
    await page.goto(`/campaigns/${deliveryIds.campaign}`)
    await page.getByLabel('Confirmation reason').fill('Confirm the exact selected booked lines.')
    const submitted = page.waitForRequest(`**/campaigns/${deliveryIds.campaign}:confirm-bookings`)
    await page.getByRole('button', { name: 'Confirm booking coverage' }).click()
    await submitted
    await page.evaluate(id => {
      window.history.pushState({}, '', `/campaigns/${id}`)
      window.dispatchEvent(new PopStateEvent('popstate'))
    }, otherId)
    await expect(page.getByRole('heading', { name: 'Gauteng Growth Campaign' })).toHaveCount(0)
    await expect(page.getByText('Loading campaign delivery')).toBeVisible()
    releaseMutation()
    releaseRead()
    await expect(page.getByRole('heading', { name: 'Second campaign' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Gauteng Growth Campaign' })).toHaveCount(0)
    await expect(page.locator('.Toastify__toast--success')).toHaveCount(0)
  } finally {
    releaseMutation()
    releaseRead()
  }
})
