import { expect, test, type Locator, type Page } from '@playwright/test'

const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const supplierUserId = '10000000-0000-0000-0000-000000000041'
const png = { name: 'connected-delivery-proof.png', mimeType: 'image/png', buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10, 1]) }

type Session = { antiforgeryToken: string }
type ProofRequest = {
  campaignId: string
  bookingId: string
  productName: string
  flightStart: string
  flightEnd: string
  latestProofId: string | null
  latestProofStatus: string | null
}
type Campaign = {
  id: string
  title: string
  status: string
  deliveryProofs: Array<{ id: string; bookingId: string; status: string }>
}

test('supplier submits and buyer approves delivery proof for every completed Marketplace booking', async ({ page }) => {
  test.setTimeout(120_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)

  await switchIdentity(page, supplierUserId)
  await chooseWorkspace(page, supplierTenantId)
  let requests = await proofRequests(page)
  const targets = requests.filter(item => /Local Demo Johannesburg/i.test(item.productName))
  expect(targets.length).toBeGreaterThan(0)

  for (const request of targets) {
    if (request.latestProofId && request.latestProofStatus !== 'REJECTED') continue
    await page.goto(`/campaigns/${request.campaignId}/bookings/${request.bookingId}/delivery-proof/new`)
    await expect(page.getByRole('heading', { name: 'Submit proof for the exact confirmed Booking.', exact: true })).toBeVisible()
    await page.getByLabel('Proof type').selectOption('PHOTO')
    await page.getByLabel('Captured at').fill('2026-10-20T09:00')
    await page.getByLabel('Location description').fill(`${request.productName}, Johannesburg`)
    await page.getByLabel('Source reference').fill(`supplier-camera:${request.bookingId}`)
    await page.getByLabel('Submission reason').fill('This evidence was captured inside the exact booked October flight and is linked to this confirmed Booking.')
    await page.getByLabel('Evidence file').setInputFiles(png)
    await mutate(page, page.getByRole('button', { name: 'Submit delivery proof', exact: true }), /\/campaigns\/[^/]+\/delivery-proofs$/, [200, 201])
    await expect(page).toHaveURL(/\/delivery-proofs\/[0-9a-f-]{36}$/)
  }

  requests = await proofRequests(page)
  for (const request of targets) {
    const current = requests.find(item => item.bookingId === request.bookingId)
    expect(current?.latestProofId).toBeTruthy()
    expect(['SUBMITTED', 'APPROVED']).toContain(current?.latestProofStatus)
  }

  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)
  let campaign = await targetCampaign(page)
  expect(campaign.status).toBe('COMPLETED')
  expect(campaign.deliveryProofs.length).toBeGreaterThanOrEqual(targets.length)

  for (const proof of campaign.deliveryProofs) {
    if (!targets.some(item => item.bookingId === proof.bookingId) || proof.status === 'APPROVED') continue
    expect(proof.status).toBe('SUBMITTED')
    await page.goto(`/delivery-proofs/${proof.id}`)
    await expect(page.getByRole('heading', { name: 'Review the exact supplier proof', exact: true })).toBeVisible()
    await page.getByLabel('Review reason').fill('The immutable supplier evidence matches the exact confirmed Booking, booked flight and retained location context.')
    await mutate(page, page.getByRole('button', { name: 'Approve proof', exact: true }), /\/campaigns\/[^/]+\/delivery-proofs\/[^/]+:review$/, [200])
  }

  campaign = await targetCampaign(page)
  const targetBookingIds = new Set(targets.map(item => item.bookingId))
  const approved = campaign.deliveryProofs.filter(item => targetBookingIds.has(item.bookingId) && item.status === 'APPROVED')
  expect(approved).toHaveLength(targets.length)
})

async function proofRequests(page: Page): Promise<ProofRequest[]> {
  const response = await page.request.get(`/api/v1/tenants/${supplierTenantId}/delivery-proof-requests`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as ProofRequest[]
}

async function targetCampaign(page: Page): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${buyerTenantId}/campaigns`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Campaign[]
  const candidate = rows.find(item => /Connected Marketplace OOH Proposal/i.test(item.title))
  expect(candidate).toBeTruthy()
  const detail = await page.request.get(`/api/v1/tenants/${buyerTenantId}/campaigns/${candidate!.id}`)
  expect(detail.ok(), await detail.text()).toBe(true)
  return await detail.json() as Campaign
}

async function mutate(page: Page, action: Locator, route: RegExp, statuses: number[]) {
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && route.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await action.click()
  const response = await responsePromise
  expect(statuses, await response.text()).toContain(response.status())
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
}
async function bootstrap(page: Page) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/bootstrap', {
    data: {}, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
}
async function switchIdentity(page: Page, userId: string) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId }, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
}
async function sessionFor(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Session
}
async function chooseWorkspace(page: Page, id: string) {
  await page.evaluate((tenantId) => sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId })), id)
}
function mutationHeaders(token: string) {
  return { Origin: 'http://localhost:3017', 'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(), 'X-Correlation-ID': crypto.randomUUID() }
}
