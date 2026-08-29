import { expect, test, type Page, type Route } from '@playwright/test'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const userId = 'e2000000-0000-0000-0000-000000000001'
const now = '2026-08-29T16:00:00Z'

type FixtureState = {
  authenticated: boolean
  profileVersion: number
  failNextSave: boolean
  expireNextProfileRead: boolean
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem('advertified.workspace', '{"tenantId":"not-a-uuid"}')
  })
  await installApiFixture(page)
})

test('authenticated workspace and profile journey remains truthful', async ({ page }) => {
  await page.goto('/sign-in')
  await expect(page.getByRole('heading', { name: 'The calm centre of campaign delivery.' })).toBeVisible()
  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  await expect(page.getByRole('heading', { name: 'Where are you working today?' })).toBeVisible()

  const workspace = page.getByRole('button', { name: /Northstar Agency/ })
  await workspace.focus()
  await page.keyboard.press('Enter')
  await expect(page.getByRole('heading', { name: /Good to see you in Northstar Agency/ })).toBeVisible()
  await expect(page.getByText('Restricted')).toHaveCount(0)

  await page.getByRole('link', { name: 'Profile', exact: true }).click()
  await page.getByLabel('Display name').fill('A')
  await page.getByRole('button', { name: 'Save profile' }).click()
  await expect(page.getByText('Enter at least two characters.')).toBeVisible()

  await page.getByLabel('Display name').fill('Alex Planner')
  await page.getByLabel('Phone').fill('+27 11 555 0101')
  await page.getByRole('button', { name: 'Save profile' }).click()
  await expect(page.getByText('Your profile has been updated.')).toBeVisible()

  await page.evaluate(() => window.setProfileSaveFailure())
  await page.getByLabel('Display name').fill('Alex Updated')
  await page.getByRole('button', { name: 'Save profile' }).click()
  await expect(page.locator('.inline-alert')).toContainText('Something went wrong')
  await expect(page.getByText('sqlstate=private')).toHaveCount(0)

  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page.getByRole('heading', { name: 'Enter your Advertified workspace' })).toBeVisible()

  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  await page.getByRole('button', { name: /Northstar Agency/ }).click()
  await page.evaluate(() => window.expireProfileSession())
  await page.getByRole('link', { name: 'Profile', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Enter your Advertified workspace' })).toBeVisible()
})

test('malformed session response fails closed without exposing payload content', async ({ page }) => {
  await page.unrouteAll({ behavior: 'wait' })
  await page.route('**/api/v1/session', async (route) => {
    await json(route, 200, { authenticated: 'yes', privateMessage: 'provider-secret' })
  })
  await page.goto('/sign-in')
  await expect(page.getByRole('alert')).toContainText('Something went wrong')
  await expect(page.getByText('provider-secret')).toHaveCount(0)
})

async function installApiFixture(page: Page) {
  const state: FixtureState = {
    authenticated: false,
    profileVersion: 1,
    failNextSave: false,
    expireNextProfileRead: false,
  }
  await page.exposeFunction('setProfileSaveFailure', () => { state.failNextSave = true })
  await page.exposeFunction('expireProfileSession', () => {
    state.expireNextProfileRead = true
  })
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))
}

async function handleApi(route: Route, state: FixtureState) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (path === '/api/v1/session') return handleSession(route, state)
  if (path === '/api/v1/workspaces') return json(route, 200, [workspaceFixture()])
  if (path === '/api/v1/me') return handleProfileRead(route, state)
  if (path === `/api/v1/tenants/${tenantId}/me`) return handleProfileUpdate(route, state)
  if (path === `/api/v1/tenants/${tenantId}`) return json(route, 200, tenantFixture())
  if (path.endsWith('/client-accounts')) return json(route, 200, pageFixture(clientFixture()))
  if (path.endsWith('/agencies')) return json(route, 200, pageFixture(agencyFixture()))
  if (path.endsWith('/contacts')) return json(route, 200, pageFixture(contactFixture()))
  return json(route, 404, safeProblem('NOT_FOUND'))
}

async function handleProfileRead(route: Route, state: FixtureState) {
  if (state.expireNextProfileRead) {
    state.expireNextProfileRead = false
    state.authenticated = false
    return json(route, 401, safeProblem('AUTHENTICATION_REQUIRED'))
  }
  return json(route, 200, profileFixture(state.profileVersion), {
    ETag: `"${state.profileVersion}"`,
  })
}

async function handleSession(route: Route, state: FixtureState) {
  const method = route.request().method()
  if (method === 'POST') {
    expect(route.request().headers()['x-csrf-token']).toBe('csrf-local')
    state.authenticated = true
  }
  if (method === 'DELETE') {
    state.authenticated = false
    return route.fulfill({ status: 204 })
  }
  return json(route, 200, {
    authenticated: state.authenticated,
    antiforgeryToken: 'csrf-local',
    expiresAtUtc: state.authenticated ? '2026-08-29T20:00:00Z' : null,
  })
}

async function handleProfileUpdate(route: Route, state: FixtureState) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-local')
  expect(headers['if-match']).toBe(`"${state.profileVersion}"`)
  expect(headers['idempotency-key']).toBeTruthy()
  if (state.failNextSave) {
    state.failNextSave = false
    return json(route, 500, { ...safeProblem('PRIVATE_FAILURE'), detail: 'sqlstate=private' })
  }
  state.profileVersion += 1
  const body = route.request().postDataJSON() as { displayName: string; phone: string }
  return json(route, 200, { ...profileFixture(state.profileVersion), ...body })
}

function workspaceFixture() {
  return { membershipId: 'e3000000-0000-0000-0000-000000000001', tenantId, name: 'Northstar Agency', slug: 'northstar', roleCode: 'agency_admin', version: 1 }
}
function profileFixture(version: number) {
  return { id: userId, email: 'alex@example.com', displayName: 'Alex Morgan', phone: null, mfaEnabled: false, version }
}
function tenantFixture() {
  return { id: tenantId, typeCode: 'AGENCY', legalName: 'Northstar Agency (Pty) Ltd', tradingName: 'Northstar Agency', slug: 'northstar', statusCode: 'ACTIVE', timeZone: 'Africa/Johannesburg', currencyCode: 'ZAR', vatStatusCode: 'REGISTERED', vatNumber: null, settingsJson: '{}', version: 1, updatedAtUtc: now }
}
function clientFixture() {
  return { id: 'c1000000-0000-0000-0000-000000000001', tenantId, externalReference: 'client-1', legalName: 'Client One', tradingName: 'Client One', website: null, industry: null, billingProfileJson: '{}', primaryContactId: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function agencyFixture() {
  return { id: 'a1000000-0000-0000-0000-000000000001', tenantId, externalReference: 'agency-1', legalName: 'Northstar Agency', tradingName: 'Northstar Agency', website: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function contactFixture() {
  return { id: 'd1000000-0000-0000-0000-000000000001', tenantId, clientAccountId: 'c1000000-0000-0000-0000-000000000001', name: 'Casey Client', jobTitle: null, email: 'casey@example.com', phone: null, purposeCode: 'CAMPAIGN', consentBasis: 'Supplied', retainUntil: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function pageFixture(item: unknown) { return { items: [item], nextCursor: null } }
function safeProblem(code: string) {
  return { type: null, title: 'Request failed', status: 500, detail: null, instance: null, code, correlationId: 'e5000000-0000-0000-0000-000000000001', fieldErrors: null }
}
async function json(route: Route, status: number, body: unknown, headers?: Record<string, string>) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers })
}

declare global {
  interface Window {
    setProfileSaveFailure: () => Promise<void>
    expireProfileSession: () => Promise<void>
  }
}
