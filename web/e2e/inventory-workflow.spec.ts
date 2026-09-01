import { expect, test, type Route } from '@playwright/test'

const tenantId = 'd1000000-0000-0000-0000-000000000001'
const userId = 'd2000000-0000-0000-0000-000000000001'
const supplierId = 'd3000000-0000-0000-0000-000000000001'
const importId = 'd4000000-0000-0000-0000-000000000001'
const candidateId = 'd5000000-0000-0000-0000-000000000001'
const productId = 'd6000000-0000-0000-0000-000000000001'
const now = '2026-08-29T18:00:00Z'

type State = {
  role: 'supplier_admin' | 'inventory_ops'
  importStatus: 'UPLOADED' | 'REVIEW_REQUIRED' | 'COMPLETED'
  candidateStatus: 'REVIEW_REQUIRED' | 'APPROVED'
  published: boolean
}

test('supplier intake reaches operator review and searchable inventory', async ({ page }) => {
  const state: State = {
    role: 'supplier_admin', importStatus: 'UPLOADED',
    candidateStatus: 'REVIEW_REQUIRED', published: false,
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/inventory')
  await expect(page.getByRole('heading', { name: 'Inventory' })).toBeVisible()
  await page.getByLabel('Supplier name').fill('City Media')
  await page.getByLabel('Source file').setInputFiles({
    name: 'city-sites.csv', mimeType: 'text/csv',
    buffer: Buffer.from('product_code,name\nOOH-001,Bree Street Gantry\n'),
  })
  await page.getByRole('button', { name: 'Protect and import' }).click()
  await expect(page.getByRole('heading', { name: 'city-sites.csv' })).toBeVisible()
  await page.getByRole('button', { name: 'Extract candidates' }).click()
  await expect(page.getByRole('heading', { name: 'Bree Street Gantry' })).toBeVisible()

  state.role = 'inventory_ops'
  await page.reload()
  await page.getByRole('button', { name: 'Approve source values' }).click()
  await expect(page.getByText('APPROVED', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Publish reviewed inventory' }).click()
  await expect(page.getByText('COMPLETED', { exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Inventory', exact: true }).click()
  const productLink = page.getByRole('link', { name: /Bree Street Gantry/ })
  await expect(productLink).toBeVisible()
  await productLink.click()
  await expect(page).toHaveURL(`/inventory/products/${productId}`)
  await expect(page.getByText('Confirm availability before booking.')).toBeVisible({ timeout: 15_000 })
  await expect(page.getByRole('heading', { name: 'How this placement compares' })).toBeVisible()
  await expect(page.getByText('Strong Value')).toBeVisible()
  await expect(page.getByText('25% below median')).toBeVisible()
  await page.getByText('RATE CARD', { exact: true }).click()
  await expect(page.getByText(/File-integrity evidence: SHA-256 a{64}/)).toBeVisible()
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (request.method() === 'GET') return handleRead(route, state, path)
  assertMutation(route)
  if (path.endsWith('/inventory-imports')) {
    expect(request.headers()['content-type']).toContain('multipart/form-data')
    return json(route, importFixture(state), 201)
  }
  if (path.endsWith(`${importId}:execute`)) {
    state.importStatus = 'REVIEW_REQUIRED'; return json(route, importFixture(state))
  }
  if (path.endsWith(`${candidateId}:review`)) {
    expect(state.role).toBe('inventory_ops')
    state.candidateStatus = 'APPROVED'; return json(route, candidateFixture(state))
  }
  if (path.endsWith(`${importId}:publish`)) {
    state.importStatus = 'COMPLETED'; state.published = true
    return json(route, importFixture(state))
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleRead(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, [workspaceFixture(state.role)])
  if (path === '/api/v1/me') return json(route, userFixture(), 200, { ETag: '"1"' })
  if (path.endsWith(`/inventory-imports/${importId}`)) return json(route, importFixture(state))
  if (path.endsWith(`/inventory-products/${productId}/benchmark`)) return json(route, benchmarkFixture())
  if (path.endsWith(`/inventory-products/${productId}`)) return json(route, productFixture())
  if (path.endsWith('/inventory-products')) return json(route, {
    items: state.published ? [productSummary()] : [], nextCursor: null,
    maximumSourceBytes: 104857600,
  })
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function importFixture(state: State) {
  return {
    id: importId, supplierId, supplierName: 'City Media', fileName: 'city-sites.csv',
    declaredMediaType: 'text/csv', documentClass: 'CSV', status: state.importStatus,
    scanStatus: 'CLEAN', sourceHash: 'a'.repeat(64), sourceSize: 52, failureCode: null,
    steps: [{ stepType: 'UPLOAD_PROTECTION', status: 'COMPLETED',
      startedAtUtc: now, completedAtUtc: now }],
    candidates: state.importStatus === 'UPLOADED' ? [] : [candidateFixture(state)],
    candidateCounts: {
      total: state.importStatus === 'UPLOADED' ? 0 : 1,
      reviewRequired: state.candidateStatus === 'REVIEW_REQUIRED' &&
        state.importStatus !== 'UPLOADED' ? 1 : 0,
      approved: state.candidateStatus === 'APPROVED' ? 1 : 0,
      rejected: 0,
      blocking: 0,
    },
    nextCandidateCursor: null,
    version: state.importStatus === 'UPLOADED' ? 1 : state.importStatus === 'COMPLETED' ? 3 : 2,
    updatedAtUtc: now,
  }
}

function candidateFixture(state: State) {
  return {
    id: candidateId, importId, rowNumber: 1, status: state.candidateStatus,
    values: valuesFixture(), validation: [{ fieldName: 'availability', code: 'AVAILABILITY_UNKNOWN',
      message: 'Availability is not supplied and must be confirmed before booking.', isBlocking: false }],
    evidence: [{ fieldName: 'product_code', rawValue: 'OOH-001', normalizedValue: 'OOH-001',
      transformation: 'TRIM', sourceLocator: 'csv#row=2', sourceHash: 'a'.repeat(64) }],
    sourceLocator: 'csv#row=2', reviewedBy: state.candidateStatus === 'APPROVED' ? userId : null,
    version: state.candidateStatus === 'APPROVED' ? 2 : 1, updatedAtUtc: now,
  }
}

function valuesFixture() {
  return { productCode: 'OOH-001', name: 'Bree Street Gantry', channel: 'OOH',
    productType: 'OOH_SITE', geography: 'Johannesburg', address: 'Bree Street',
    latitude: -26.2041, longitude: 28.0473, rateType: 'MONTH_RATE', currency: 'ZAR',
    rateAmountMinor: 125000, availability: 'UNKNOWN', extension: {} }
}

function productSummary() {
  return { id: productId, supplierId, supplierName: 'City Media', productCode: 'OOH-001',
    name: 'Bree Street Gantry', channel: 'OOH', productType: 'OOH_SITE', geography: 'Johannesburg',
    verification: 'HUMAN_VERIFIED', version: 1, updatedAtUtc: now }
}

function productFixture() {
  return { product: productSummary(), address: 'Bree Street', latitude: -26.2041, longitude: 28.0473,
    extension: {}, rate: { rateType: 'MONTH_RATE', currency: 'ZAR', amountMinor: 125000,
      sourceLocator: 'csv#row=2' }, availability: { status: 'UNKNOWN', observedAtUtc: now,
      validUntilUtc: null, sourceLocator: 'csv#row=2' }, assets: [{ assetType: 'RATE_CARD',
        mediaType: 'text/csv', contentHash: 'a'.repeat(64), sourceReference: `inventory-import:${importId}` }],
    sourceImportId: importId, sourceCandidateId: candidateId, versionNumber: 1, publishedAtUtc: now }
}

function benchmarkFixture() {
  return {
    productId,
    productVersionId: 'd6100000-0000-0000-0000-000000000001',
    rateId: 'd6200000-0000-0000-0000-000000000001',
    rateType: 'MONTH_RATE', rateAmountMinor: 125000, currency: 'ZAR',
    policyVersion: 'OOH_LOCAL_PEER_V1', geographyBasis: 'RADIUS_5_KM', cohortSize: 4,
    medianMinor: 166667, lowerQuartileMinor: 145000, upperQuartileMinor: 185000,
    percentile: 25, differenceFromMedianMinor: -41667, differenceFromMedianPercent: -25,
    position: 'STRONG_VALUE', confidence: 0.7,
    comparables: [{ productId: 'd6300000-0000-0000-0000-000000000001',
      productVersionId: 'd6400000-0000-0000-0000-000000000001', name: 'Braamfontein Digital',
      geography: 'Johannesburg', rateAmountMinor: 150000, currency: 'ZAR', distanceKilometres: 2.4 }],
    exclusions: [],
  }
}

function assertMutation(route: Route) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-inventory')
  expect(headers['idempotency-key']).toBeTruthy()
}

function sessionFixture() { return { authenticated: true, antiforgeryToken: 'csrf-inventory', expiresAtUtc: '2026-08-29T22:00:00Z' } }
function workspaceFixture(role: State['role']) { return { membershipId: 'd7000000-0000-0000-0000-000000000001', tenantId, name: 'Media Workspace', slug: 'media-workspace', roleCode: role, version: 1 } }
function userFixture() { return { id: userId, email: 'operator@example.com', displayName: 'Inventory Operator', phone: null, mfaEnabled: true, version: 1 } }

async function json(route: Route, body: unknown, status = 200, headers?: Record<string, string>) {
  await route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(body) })
}
