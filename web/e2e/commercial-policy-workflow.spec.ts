import { expect, test, type Page, type Route } from '@playwright/test'

const tenantId = 'c7100000-0000-0000-0000-000000000001'
const userId = 'c7200000-0000-0000-0000-000000000001'
const policyId = 'c7300000-0000-0000-0000-000000000001'
const now = '2026-08-30T12:00:00Z'

type State = { version: number; markupBasisPoints: number }

test('administrator creates and versions exact commercial settings', async ({ page }) => {
  const state: State = { version: 0, markupBasisPoints: 0 }
  await page.addInitScript((selectedTenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({
      tenantId: selectedTenantId,
    }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/admin/commercial')
  await expect(page.getByRole('heading', { name: 'Commercial policy' })).toBeVisible()
  await expect(page.getByText('No policy exists yet.')).toBeVisible()
  await fillPolicy(page, '15.00')
  await page.getByRole('button', { name: 'Save policy version' }).click()
  await expect(page.getByText('Commercial policy version 1 saved.')).toBeVisible()
  await expect(page.getByText('Version 1', { exact: true })).toBeVisible()

  await page.getByLabel('Markup (%)').fill('17.50')
  await page.getByRole('button', { name: 'Save policy version' }).click()
  await expect(page.getByText('Commercial policy version 2 saved.')).toBeVisible()
  await expect(page.getByText('Version 2', { exact: true })).toBeVisible()
})

async function fillPolicy(page: Page, markup: string) {
  await page.getByLabel('Markup (%)').fill(markup)
  await page.getByLabel('Management fee (%)').fill('5.00')
  await page.getByLabel('Agency commission (%)').fill('10.00')
  await page.getByLabel('VAT treatment').selectOption('REGISTERED')
  await page.getByLabel('VAT rate (%)').fill('15.00')
  await page.getByLabel('Policy currency').selectOption('ZAR')
  await page.getByLabel(/Booking approval threshold/).fill('50000.00')
}

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (path === '/api/v1/session') return json(route, 200, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, 200, [workspaceFixture()])
  if (path === `/api/v1/tenants/${tenantId}/commercial-policy`) {
    if (request.method() === 'GET') {
      return state.version === 0
        ? json(route, 404, safeProblem('COMMERCIAL_POLICY_NOT_CONFIGURED'))
        : json(route, 200, policyFixture(state), { ETag: `"${state.version}"` })
    }
    if (request.method() === 'PUT') return savePolicy(route, state)
  }
  return json(route, 404, safeProblem('NOT_FOUND'))
}

async function savePolicy(route: Route, state: State) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-commercial-policy')
  expect(headers['idempotency-key']).toBeTruthy()
  expect(headers['if-match']).toBe(`"${state.version}"`)
  const body = route.request().postDataJSON() as Record<string, unknown>
  expect(body).toMatchObject({
    managementFeeBasisPoints: 500,
    commissionBasisPoints: 1_000,
    vatStatus: 'REGISTERED',
    vatRateBasisPoints: 1_500,
    pricesIncludeVat: false,
    currency: 'ZAR',
    bookingApprovalThresholdMinor: 5_000_000,
  })
  state.version += 1
  state.markupBasisPoints = body.markupBasisPoints as number
  return json(route, 200, policyFixture(state), { ETag: `"${state.version}"` })
}

function policyFixture(state: State) {
  return {
    id: `c7400000-0000-0000-0000-${state.version.toString().padStart(12, '0')}`,
    policyId,
    versionNumber: state.version,
    markupBasisPoints: state.markupBasisPoints,
    managementFeeBasisPoints: 500,
    commissionBasisPoints: 1_000,
    vatStatus: 'REGISTERED',
    vatRateBasisPoints: 1_500,
    pricesIncludeVat: false,
    currency: 'ZAR',
    bookingApprovalThresholdMinor: 5_000_000,
    createdBy: userId,
    createdAtUtc: now,
    version: state.version,
  }
}

function workspaceFixture() {
  return {
    membershipId: 'c7500000-0000-0000-0000-000000000001',
    tenantId,
    name: 'Advertified Local Development',
    slug: 'advertified-local-development',
    roleCode: 'agency_admin',
    version: 1,
  }
}

function sessionFixture() {
  return {
    authenticated: true,
    antiforgeryToken: 'csrf-commercial-policy',
    expiresAtUtc: '2026-08-30T18:00:00Z',
  }
}

function safeProblem(code: string) {
  return { status: code === 'COMMERCIAL_POLICY_NOT_CONFIGURED' ? 404 : 500,
    title: 'Request failed', code, correlationId: userId }
}

async function json(
  route: Route,
  status: number,
  body: unknown,
  headers?: Record<string, string>,
) {
  await route.fulfill({ status, contentType: 'application/json',
    body: JSON.stringify(body), headers })
}
