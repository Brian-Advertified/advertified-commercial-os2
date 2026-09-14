import { expect, test, type Page } from '@playwright/test'

const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const supplierUserId = '10000000-0000-0000-0000-000000000041'
const buyerUserId = '10000000-0000-0000-0000-000000000001'

type Session = { antiforgeryToken: string }

test('diagnose supplier proof queue', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  let session = await sessionFor(page)
  let response = await page.request.post('/api/v1/development/connected-acceptance/bootstrap', {
    data: {}, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.ok(), await response.text()).toBe(true)

  await switchIdentity(page, supplierUserId)
  const workspaces = await page.request.get('/api/v1/workspaces')
  const bookings = await page.request.get(`/api/v1/tenants/${supplierTenantId}/bookings`)
  const requests = await page.request.get(`/api/v1/tenants/${supplierTenantId}/delivery-proof-requests`)
  console.log('workspaces', JSON.stringify(await workspaces.json(), null, 2))
  console.log('supplier-bookings', JSON.stringify(await bookings.json(), null, 2))
  console.log('proof-requests', JSON.stringify(await requests.json(), null, 2))

  await switchIdentity(page, buyerUserId)
  const buyerBookings = await page.request.get(`/api/v1/tenants/${buyerTenantId}/bookings`)
  const buyerCampaigns = await page.request.get(`/api/v1/tenants/${buyerTenantId}/campaigns`)
  console.log('buyer-bookings', JSON.stringify(await buyerBookings.json(), null, 2))
  console.log('buyer-campaigns', JSON.stringify(await buyerCampaigns.json(), null, 2))
  expect(requests.ok(), await requests.text()).toBe(true)
})

async function switchIdentity(page: Page, userId: string) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId }, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.ok(), await response.text()).toBe(true)
}
async function sessionFor(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Session
}
function mutationHeaders(token: string) {
  return { Origin: 'http://localhost:3017', 'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(), 'X-Correlation-ID': crypto.randomUUID() }
}
