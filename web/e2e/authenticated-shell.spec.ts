import { expect, test, type Page, type Route } from '@playwright/test'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const userId = 'e2000000-0000-0000-0000-000000000001'
const briefId = 'e6000000-0000-0000-0000-000000000001'
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
  await signInAndChooseWorkspace(page)
  await expect(page.getByRole('heading', { name: 'Good morning, Alex 👋', exact: true })).toBeVisible()
  await expect(page.getByText('Restricted')).toHaveCount(0)

  const skipLink = page.getByRole('link', { name: 'Skip to main content' })
  await skipLink.focus()
  await page.keyboard.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()

  await page.getByRole('link', { name: /Alex Morgan/ }).click()
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

  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Northstar Agency/ }).click()
  await page.evaluate(() => window.expireProfileSession())
  await page.getByRole('link', { name: /Alex Morgan/ }).dispatchEvent('click')
  await expect(page.getByRole('heading', { name: 'Enter your Advertified workspace' })).toBeVisible()
})

test('sidebar contains only real top-level work areas and workflow progress is not a menu', async ({ page }) => {
  await signInAndChooseWorkspace(page)

  const destinations = [
    ['Home', '/home'],
    ['Opportunities', '/opportunities'],
    ['Briefs', '/briefs'],
    ['Inventory', '/inventory'],
    ['Marketplace', '/marketplace'],
    ['OOH Inbox', '/ooh-inbox'],
    ['Bookings', '/bookings'],
    ['Campaigns', '/campaigns'],
    ['Tasks', '/tasks'],
    ['Finance', '/funding'],
    ['Settings', '/admin/commercial'],
  ] as const

  for (const [label, path] of destinations) {
    await expect(page.getByRole('link', { name: label, exact: true })).toHaveAttribute('href', path)
  }

  for (const stage of ['Strategy & STP', 'Planning', 'Proposals', 'Approvals', 'Measurement', 'Reports']) {
    await expect(page.getByRole('link', { name: stage, exact: true })).toHaveCount(0)
  }

  await page.getByRole('link', { name: 'Briefs', exact: true }).click()
  await expect(page).toHaveURL(/\/briefs$/)
  await expect(page.getByRole('heading', { name: 'Briefs', exact: true })).toBeVisible()

  await page.getByRole('link', { name: '+ New Brief', exact: true }).click()
  await expect(page).toHaveURL(/\/briefs\/new$/)
  const flow = page.getByRole('region', { name: 'Campaign Flow' })
  await expect(flow).toBeVisible()
  await expect(flow).toHaveAttribute('data-campaign-mode', 'mode-unresolved')
  await expect(page.getByRole('region', { name: 'Full Campaign Flow' })).toHaveCount(0)
  await expect(flow.getByRole('link')).toHaveCount(0)
})

test('topbar search, messages and help open real destinations', async ({ page }) => {
  await signInAndChooseWorkspace(page)

  const search = page.getByRole('search')
  await search.getByRole('searchbox', { name: 'Search Advertified' }).fill('December')
  await search.getByRole('button', { name: 'Submit search' }).click()
  await expect(page).toHaveURL(/\/search\?q=December$/)
  await expect(page.getByRole('heading', { name: 'Search Advertified' })).toBeVisible()
  await expect(page.getByRole('link', { name: /December launch Brief/ }))
    .toHaveAttribute('href', `/briefs/${briefId}`)

  await page.getByRole('link', { name: 'Home', exact: true }).click()
  await expect(page).toHaveURL(/\/home$/)
  const shortcutSearch = page.getByRole('searchbox', { name: 'Search Advertified' })
  await expect(shortcutSearch).toBeVisible()
  await page.keyboard.press('Control+K')
  await expect(shortcutSearch).toBeFocused()
  await shortcutSearch.fill('Northstar')
  await shortcutSearch.press('Enter')
  await expect(page).toHaveURL(/\/search\?q=Northstar$/)

  await page.getByRole('link', { name: 'Home', exact: true }).click()
  await expect(page).toHaveURL(/\/home$/)
  if ((page.viewportSize()?.width ?? 0) <= 820) return
  const messages = page.getByRole('link', { name: 'Messages' })
  const help = page.getByRole('link', { name: 'Help' })
  await expect(messages).toHaveAttribute('href', '/ooh-inbox')
  await expect(help).toHaveAttribute('href', '/faq')

  await messages.click()
  await expect(page).toHaveURL(/\/ooh-inbox$/)

  await page.getByRole('link', { name: 'Home', exact: true }).click()
  await expect(page).toHaveURL(/\/home$/)
  await page.getByRole('link', { name: 'Help' }).click()
  await expect(page).toHaveURL(/\/faq$/)
  await expect(page.getByRole('heading', {
    name: 'The practical things you should know before starting.',
  })).toBeVisible({ timeout: 15_000 })
})

test('malformed session response fails closed without exposing payload content', async ({ page }) => {
  await page.unrouteAll({ behavior: 'wait' })
  await page.route('**/api/v1/session', async route => {
    await json(route, 200, { authenticated: 'yes', privateMessage: 'provider-secret' })
  })
  await page.goto('/sign-in')
  await expect(page.getByRole('alert')).toContainText('Advertified received an unexpected response')
  await expect(page.getByText('provider-secret')).toHaveCount(0)
})

async function signInAndChooseWorkspace(page: Page) {
  await page.goto('/sign-in')
  await expect(page.getByRole('heading', { name: 'The calm centre of campaign delivery.' })).toBeVisible()
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await expect(page.getByRole('heading', { name: 'Where are you working today?' })).toBeVisible()
  await page.getByRole('button', { name: /Northstar Agency/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}

async function installApiFixture(page: Page) {
  const state: FixtureState = {
    authenticated: false,
    profileVersion: 1,
    failNextSave: false,
    expireNextProfileRead: false,
  }
  await page.exposeFunction('setProfileSaveFailure', () => { state.failNextSave = true })
  await page.exposeFunction('expireProfileSession', () => { state.expireNextProfileRead = true })
  await page.route('**/api/v1/**', async route => handleApi(route, state))
}

async function handleApi(route: Route, state: FixtureState) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (path === '/api/v1/session') return handleSession(route, state)
  if (path === '/api/v1/workspaces') return json(route, 200, [workspaceFixture()])
  if (path === '/api/v1/me') return handleProfileRead(route, state)
  if (path === `/api/v1/tenants/${tenantId}/me`) return handleProfileUpdate(route, state)
  return handleTenantRead(route, path)
}

const emptyPageAreas = new Set(['opportunities', 'human-tasks'])
const emptyListAreas = new Set(['planning', 'proposals', 'campaigns', 'bookings'])
const tenantPageFixtures: Readonly<Record<string, () => unknown>> = {
  'client-accounts': clientFixture,
  agencies: agencyFixture,
  contacts: contactFixture,
}

async function handleTenantRead(route: Route, path: string) {
  if (path === `/api/v1/tenants/${tenantId}`) {
    return json(route, 200, tenantFixture())
  }
  const area = path.split('/').filter(Boolean).at(-1) ?? ''
  if (area === 'briefs') return json(route, 200, [briefSearchFixture()])
  if (emptyPageAreas.has(area)) return json(route, 200, emptyPageFixture())
  if (emptyListAreas.has(area)) return json(route, 200, [])
  if (path.includes('/inventory-products')) {
    return json(route, 200, { items: [], nextCursor: null, maximumSourceBytes: 67108864 })
  }
  const fixture = tenantPageFixtures[area]
  return fixture
    ? json(route, 200, pageFixture(fixture()))
    : json(route, 404, safeProblem('NOT_FOUND'))
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
    signInPath: null,
    signOutPath: null,
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
  return { membershipId: 'e3000000-0000-0000-0000-000000000001', tenantId,
    name: 'Northstar Agency', slug: 'northstar', roleCode: 'agency_admin', version: 1 }
}
function profileFixture(version: number) {
  return { id: userId, email: 'alex@example.com', displayName: 'Alex Morgan', phone: null,
    mfaEnabled: false, version }
}
function tenantFixture() {
  return { id: tenantId, typeCode: 'AGENCY', legalName: 'Northstar Agency (Pty) Ltd',
    tradingName: 'Northstar Agency', slug: 'northstar', statusCode: 'ACTIVE',
    timeZone: 'Africa/Johannesburg', currencyCode: 'ZAR', vatStatusCode: 'REGISTERED',
    vatNumber: null, settingsJson: '{}', version: 1, updatedAtUtc: now }
}
function clientFixture() {
  return { id: 'c1000000-0000-0000-0000-000000000001', tenantId,
    externalReference: 'client-1', legalName: 'Client One', tradingName: 'Client One',
    website: null, industry: null, billingProfileJson: '{}', primaryContactId: null,
    statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function agencyFixture() {
  return { id: 'a1000000-0000-0000-0000-000000000001', tenantId,
    externalReference: 'agency-1', legalName: 'Northstar Agency', tradingName: 'Northstar Agency',
    website: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function contactFixture() {
  return { id: 'd1000000-0000-0000-0000-000000000001', tenantId,
    clientAccountId: 'c1000000-0000-0000-0000-000000000001', name: 'Casey Client',
    jobTitle: null, email: 'casey@example.com', phone: null, purposeCode: 'CAMPAIGN',
    consentBasis: 'Supplied', retainUntil: null, statusCode: 'ACTIVE', version: 1,
    updatedAtUtc: now }
}
function briefSearchFixture() {
  return { id: briefId, tenantId, clientId: 'e7000000-0000-0000-0000-000000000001',
    clientName: 'Northstar Retail', opportunityId: null, title: 'December launch Brief',
    ownerUserId: userId, status: 'DRAFT',
    currentDraftVersionId: 'e8000000-0000-0000-0000-000000000001',
    readyVersionId: null, approvedVersionId: null, version: 1, updatedAtUtc: now }
}
function pageFixture(item: unknown) { return { items: [item], nextCursor: null } }
function emptyPageFixture() { return { items: [], nextCursor: null } }
function safeProblem(code: string) {
  return { type: null, title: 'Request failed', status: 500, detail: null, instance: null,
    code, correlationId: 'e5000000-0000-0000-0000-000000000001', fieldErrors: null }
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
