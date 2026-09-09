import { expect, test, type Page, type Route } from '@playwright/test'

type RoleCase = {
  roleCode: string
  heading: string
  expectedNavigation: string[]
  hiddenNavigation: string[]
  dashboardAction: string
}

const tenantId = 'eb000000-0000-0000-0000-000000000001'
const userId = 'eb000000-0000-0000-0000-000000000002'
const membershipId = 'eb000000-0000-0000-0000-000000000003'
const now = '2026-09-09T03:00:00Z'

const cases: RoleCase[] = [
  {
    roleCode: 'supplier_user', heading: 'Supplier workspace',
    expectedNavigation: ['Home', 'Inventory', 'Marketplace', 'Bookings', 'Delivery', 'Tasks'],
    hiddenNavigation: ['Opportunities', 'Briefs', 'Media inbox', 'Campaigns', 'Reporting', 'Finance'],
    dashboardAction: 'Creative & delivery queue',
  },
  {
    roleCode: 'influencer_rep', heading: 'Creator workspace',
    expectedNavigation: ['Home', 'Inventory', 'Marketplace', 'Bookings', 'Delivery', 'Tasks'],
    hiddenNavigation: ['Opportunities', 'Briefs', 'Media inbox', 'Campaigns', 'Reporting', 'Finance'],
    dashboardAction: 'Creative & delivery queue',
  },
  {
    roleCode: 'advertiser_admin', heading: 'Advertiser review workspace',
    expectedNavigation: ['Home', 'Briefs', 'Campaigns', 'Reporting', 'Tasks'],
    hiddenNavigation: ['Opportunities', 'Inventory', 'Marketplace', 'Media inbox', 'Bookings', 'Delivery', 'Finance'],
    dashboardAction: 'Campaign progress',
  },
]

for (const role of cases) {
  test(`${role.roleCode} sees a role-specific navigation and dashboard`, async ({ page }) => {
    await installFixture(page, role.roleCode)
    await page.goto('/home')
    await expect(page.getByRole('heading', { name: role.heading, exact: true })).toBeVisible()
    const navigation = page.getByRole('navigation', { name: 'Workspace navigation' })
    for (const label of role.expectedNavigation) {
      await expect(navigation.getByRole('link', { name: label, exact: true })).toBeVisible()
    }
    for (const label of role.hiddenNavigation) {
      await expect(navigation.getByRole('link', { name: label, exact: true })).toHaveCount(0)
    }
    await expect(page.getByRole('link', { name: role.dashboardAction })).toBeVisible()
    await expect(page.getByRole('link', { name: /^New/ })).toHaveCount(0)
  })
}

async function installFixture(page: Page, roleCode: string) {
  await page.addInitScript(id => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, roleCode))
}

async function handleApi(route: Route, roleCode: string) {
  const path = new URL(route.request().url()).pathname
  if (path === '/api/v1/session') return json(route, {
    authenticated: true, antiforgeryToken: 'csrf-role-home',
    expiresAtUtc: '2099-09-09T05:00:00Z', signInPath: null, signOutPath: null,
  })
  if (path === '/api/v1/workspaces') return json(route, [{
    membershipId, tenantId, name: 'Role Workspace', slug: 'role-workspace', roleCode, version: 1,
  }])
  if (path === '/api/v1/me') return json(route, {
    id: userId, email: 'role@example.test', displayName: 'Role User', phone: null,
    mfaEnabled: false, version: 1,
  }, { ETag: '"1"' })
  if (path === `/api/v1/tenants/${tenantId}`) return json(route, {
    id: tenantId, typeCode: tenantType(roleCode), legalName: 'Role Workspace',
    tradingName: 'Role Workspace', slug: 'role-workspace', statusCode: 'ACTIVE',
    timeZone: 'Africa/Johannesburg', currencyCode: 'ZAR', vatStatusCode: 'UNKNOWN',
    vatNumber: null, settingsJson: '{}', version: 1, updatedAtUtc: now,
  })
  if (path.endsWith('/human-tasks')) return json(route, { items: [], nextCursor: null })
  if (path.endsWith('/inventory-products')) {
    return json(route, { items: [], nextCursor: null, maximumSourceBytes: 67108864 })
  }
  if (path.endsWith('/campaigns') || path.endsWith('/bookings')) return json(route, [])
  return json(route, { code: 'NOT_FOUND', status: 404 }, {}, 404)
}

function tenantType(roleCode: string) {
  if (roleCode === 'influencer_rep') return 'CREATOR'
  if (roleCode === 'supplier_user') return 'SUPPLIER'
  return 'ADVERTISER'
}

async function json(route: Route, body: unknown, headers: Record<string, string> = {}, status = 200) {
  await route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(body) })
}
