import { expect, test, type Route } from '@playwright/test'

const buyerTenantId = 'b1000000-0000-0000-0000-000000000001'
const supplierTenantId = 'b1000000-0000-0000-0000-000000000002'
const userId = 'b2000000-0000-0000-0000-000000000001'
const bookingId = 'b3000000-0000-0000-0000-000000000001'
const proposalId = 'b4000000-0000-0000-0000-000000000001'
const optionId = 'b4000000-0000-0000-0000-000000000002'
const decisionId = 'b4000000-0000-0000-0000-000000000003'
const planId = 'b5000000-0000-0000-0000-000000000001'
const lineId = 'b5000000-0000-0000-0000-000000000002'
const listingVersionId = 'b6000000-0000-0000-0000-000000000001'
const now = '2026-08-30T12:00:00Z'

type BookingStatus = 'DRAFT' | 'PENDING_SUPPLIER' | 'CONFIRMED'
type State = { status: BookingStatus | null; version: number }

test('buyer requests and supplier explicitly confirms the frozen booking line', async ({ page }) => {
  const state: State = { status: null, version: 0 }
  await page.addInitScript((tenantId) => {
    if (!sessionStorage.getItem('advertified.workspace')) {
      sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId }))
    }
  }, buyerTenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto('/bookings')
  await expect(page.getByRole('heading', { name: 'Bookings', exact: true })).toBeVisible()
  await expect(page.getByRole('heading', {
    name: 'N1 Highway Digital Billboard',
  })).toBeVisible()
  await page.getByRole('button', { name: 'Create booking draft' }).click()
  await expect(page.getByText('Draft', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Request supplier confirmation' }).click()
  await expect(page.getByText('Supplier review', { exact: true })).toBeVisible()

  await page.evaluate((tenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId }))
  }, supplierTenantId)
  await page.reload()
  await expect(page.getByText('Supplier amount', { exact: true })).toBeVisible()
  await expect(page.getByText('Client-approved total', { exact: true })).toHaveCount(0)
  await page.getByLabel(/I confirm the current rate/).check()
  await page.getByLabel('Supplier note').fill('Current supply confirmed by the supplier.')
  await page.getByRole('button', { name: 'Confirm booking' }).click()
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  await expect(page.getByText(
    'Confirmed by both buyer workflow and supplier.',
  )).toBeVisible()
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (request.method() === 'GET') return handleGet(route, state, path)
  if (request.method() === 'POST') return handlePost(route, state, path)
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function handleGet(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, workspaceFixtures())
  if (path.endsWith('/bookings/bookable-lines')) {
    return json(route, [bookableFixture(state.status !== null)])
  }
  if (path.endsWith('/bookings')) {
    const supplierView = path.includes(supplierTenantId)
    return json(route, state.status ? [bookingFixture(state, supplierView)] : [])
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function handlePost(route: Route, state: State, path: string) {
  assertMutation(route)
  if (path.endsWith('/bookings')) {
    expect(route.request().headers()['if-match']).toBeUndefined()
    state.status = 'DRAFT'; state.version = 1
    return json(route, bookingFixture(state, false), 201)
  }
  if (path.endsWith(`${bookingId}:request-confirmation`)) {
    expect(route.request().headers()['if-match']).toBe('"1"')
    state.status = 'PENDING_SUPPLIER'; state.version = 2
    return json(route, bookingFixture(state, false))
  }
  if (path.endsWith(`${bookingId}:confirm`)) {
    expect(route.request().headers()['if-match']).toBe('"2"')
    expect(route.request().postDataJSON()).toMatchObject({ acceptTerms: true })
    state.status = 'CONFIRMED'; state.version = 3
    return json(route, bookingFixture(state, true))
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function bookableFixture(alreadyBooked: boolean) {
  return {
    proposalVersionId: proposalId, proposalOptionId: optionId,
    proposalDecisionId: decisionId, planVersionId: planId, mediaPlanLineId: lineId,
    supplierName: 'Verified Outdoor Media', productName: 'N1 Highway Digital Billboard',
    channel: 'OOH', geography: 'Johannesburg', flightStart: '2026-09-01',
    flightEnd: '2026-09-30', runningPeriods: 1, quantity: 1,
    clientPriceMinor: 1443250, feesMinor: 5000, vatMinor: 188250,
    currency: 'ZAR', alreadyBooked,
  }
}

function bookingFixture(state: State, supplierView: boolean) {
  const pending = state.status !== 'DRAFT'
  const confirmed = state.status === 'CONFIRMED'
  const sensitive = supplierView
    ? { proposalVersionId: null, proposalOptionId: null, proposalDecisionId: null,
        planVersionId: null, mediaPlanLineId: null,
        clientPriceMinor: null, feesMinor: null, vatMinor: null }
    : { proposalVersionId: proposalId, proposalOptionId: optionId,
        proposalDecisionId: decisionId, planVersionId: planId, mediaPlanLineId: lineId,
        clientPriceMinor: 1443250, feesMinor: 5000, vatMinor: 188250 }
  const request = pending
    ? { requestedBy: userId, requestedAtUtc: now,
        requestReason: 'Buyer requested supplier confirmation.' }
    : { requestedBy: null, requestedAtUtc: null, requestReason: null }
  const confirmation = confirmed
    ? { confirmedBy: userId, confirmedAtUtc: now,
        confirmationReason: 'Supplier confirmed current supply.',
        supplierNote: 'Current supply confirmed by the supplier.' }
    : { confirmedBy: null, confirmedAtUtc: null,
        confirmationReason: null, supplierNote: null }
  return {
    id: bookingId, buyerTenantId, supplierTenantId, ...sensitive,
    marketplaceListingVersionId: listingVersionId,
    supplierName: 'Verified Outdoor Media', productName: 'N1 Highway Digital Billboard',
    channel: 'OOH', geography: 'Johannesburg', flightStart: '2026-09-01',
    flightEnd: '2026-09-30', runningPeriods: 1, quantity: 1,
    supplierCostMinor: 1250000,
    currency: 'ZAR', terms: 'Frozen client-selected booking terms.', status: state.status,
    createdBy: userId, createdAtUtc: now, ...request, ...confirmation,
    termsAccepted: confirmed, version: state.version, updatedAtUtc: now,
  }
}

function assertMutation(route: Route) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-booking')
  expect(headers['idempotency-key']).toBeTruthy()
}

function sessionFixture() {
  return { authenticated: true, antiforgeryToken: 'csrf-booking',
    expiresAtUtc: '2026-08-30T18:00:00Z' }
}

function workspaceFixtures() {
  return [
    { membershipId: 'b7000000-0000-0000-0000-000000000001',
      tenantId: buyerTenantId, name: 'Buyer Agency', slug: 'buyer-agency',
      roleCode: 'agency_admin', version: 1 },
    { membershipId: 'b7000000-0000-0000-0000-000000000002',
      tenantId: supplierTenantId, name: 'Supplier Workspace', slug: 'supplier-workspace',
      roleCode: 'supplier_admin', version: 1 },
  ]
}

async function json(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}
