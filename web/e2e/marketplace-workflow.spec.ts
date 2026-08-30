import { expect, test, type Route } from '@playwright/test'

const buyerTenantId = 'f1000000-0000-0000-0000-000000000001'
const supplierTenantId = 'f1000000-0000-0000-0000-000000000002'
const userId = 'f2000000-0000-0000-0000-000000000001'
const listingId = 'f3000000-0000-0000-0000-000000000001'
const listingVersionId = 'f3000000-0000-0000-0000-000000000002'
const rfqId = 'f4000000-0000-0000-0000-000000000001'
const now = '2026-08-30T10:00:00Z'

type State = { rfq: ReturnType<typeof rfqFixture> | null }

test('buyer creates and explicitly sends a marketplace request', async ({ page }) => {
  const state: State = { rfq: null }
  await page.addInitScript((tenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId }))
  }, buyerTenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/marketplace')
  await expect(page.getByRole('heading', { name: 'Supplier marketplace' })).toBeVisible()
  await expect(page.getByText('Acceptance never creates a booking.')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'N1 Highway Digital Billboard' })).toBeVisible()
  await page.getByRole('button', { name: 'Request availability' }).click()
  await page.getByLabel('Request subject').fill('September Johannesburg launch')
  await page.getByLabel('Start date').fill('2026-09-15')
  await page.getByLabel('End date').fill('2026-10-15')
  await page.getByLabel('Supplier response due').fill('2026-09-10T12:00')
  await page.getByRole('button', { name: 'Create draft request' }).click()

  await expect(page.getByText('DRAFT', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Send to supplier' }).click()
  await expect(page.getByText('SENT', { exact: true })).toBeVisible()
  await expect(page.getByText('No booking was created.')).toHaveCount(0)
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (request.method() === 'GET') {
    if (path === '/api/v1/session') return json(route, sessionFixture())
    if (path === '/api/v1/workspaces') return json(route, [workspaceFixture()])
    if (path.endsWith('/marketplace-listings')) return json(route,
      { items: [listingFixture()], nextCursor: null })
    if (path.endsWith('/marketplace-rfqs')) return json(route,
      { items: state.rfq ? [state.rfq] : [], nextCursor: null })
  }
  if (request.method() === 'POST') {
    assertMutation(route)
    if (path.endsWith('/marketplace-rfqs')) {
      expect(request.headers()['if-match']).toBeUndefined()
      state.rfq = rfqFixture('DRAFT', 1)
      return json(route, state.rfq, 201)
    }
    if (path.endsWith(`${rfqId}:send`)) {
      expect(request.headers()['if-match']).toBe('"1"')
      state.rfq = rfqFixture('SENT', 2)
      return json(route, state.rfq)
    }
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function listingFixture() {
  return {
    id: listingId, supplierTenantId, productId: 'f5000000-0000-0000-0000-000000000001',
    status: 'PUBLISHED', version: 2, updatedAtUtc: now,
    currentVersion: { id: listingVersionId, versionNumber: 1,
      productVersionId: 'f5000000-0000-0000-0000-000000000002',
      rateId: 'f5000000-0000-0000-0000-000000000003',
      availabilityId: 'f5000000-0000-0000-0000-000000000004',
      supplierName: 'Verified Outdoor Media', productName: 'N1 Highway Digital Billboard',
      channel: 'OOH', productType: 'OOH_SITE', geography: 'Johannesburg',
      rateType: 'MONTH_RATE', amountMinor: 1250000, currency: 'ZAR',
      availability: 'AVAILABLE', availabilityValidUntilUtc: '2027-08-30T10:00:00Z',
      terms: 'Subject to human-approved booking.', publishedBy: userId, publishedAtUtc: now },
  }
}

function rfqFixture(status = 'DRAFT', version = 1) {
  return { id: rfqId, buyerTenantId, supplierTenantId, listingVersionId,
    supplierName: 'Verified Outdoor Media', productName: 'N1 Highway Digital Billboard',
    subject: 'September Johannesburg launch', requestedStart: '2026-09-15',
    requestedEnd: '2026-10-15', quantity: 1, dueAtUtc: '2026-09-10T10:00:00Z',
    status, response: null, createdBy: userId,
    sentBy: status === 'SENT' ? userId : null, sentAtUtc: status === 'SENT' ? now : null,
    version, updatedAtUtc: now }
}

function assertMutation(route: Route) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-marketplace')
  expect(headers['idempotency-key']).toBeTruthy()
}

function sessionFixture() {
  return { authenticated: true, antiforgeryToken: 'csrf-marketplace',
    expiresAtUtc: '2026-08-30T18:00:00Z' }
}

function workspaceFixture() {
  return { membershipId: 'f6000000-0000-0000-0000-000000000001',
    tenantId: buyerTenantId, name: 'Buyer Agency', slug: 'buyer-agency',
    roleCode: 'agency_admin', version: 1 }
}

async function json(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}
