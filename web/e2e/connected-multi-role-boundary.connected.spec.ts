import { expect, test, type Page } from '@playwright/test'

const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const clientUserId = '10000000-0000-0000-0000-000000000004'
const supplierUserId = '10000000-0000-0000-0000-000000000041'
const financeUserId = '10000000-0000-0000-0000-000000000042'

type Session = { antiforgeryToken: string }
type Workspace = { tenantId: string; name: string; roleCode: string }

test('connected browser crosses buyer, supplier, client and finance role boundaries', async ({ page }) => {
  test.setTimeout(60_000)
  page.setDefaultTimeout(7_500)

  console.log('role-boundary: sign in')
  await signIn(page)
  await bootstrap(page)

  console.log('role-boundary: buyer')
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)
  await page.goto('/marketplace')
  await expect(page.getByRole('heading', { name: 'Inventory marketplace' })).toBeVisible()
  await expect(page.getByText('Buyer', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: /Requests/ })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Publish supply' })).toHaveCount(0)

  console.log('role-boundary: supplier')
  await switchIdentity(page, supplierUserId)
  await chooseWorkspace(page, supplierTenantId)
  await page.goto('/marketplace')
  await expect(page.getByRole('heading', { name: 'Inventory marketplace' })).toBeVisible()
  await expect(page.locator('.connected-marketplace-proofbar').getByText('Supplier', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Publish supply' })).toBeVisible()

  console.log('role-boundary: client')
  await switchIdentity(page, clientUserId)
  const clientWorkspaces = await workspaces(page)
  expect(clientWorkspaces).toEqual(expect.arrayContaining([
    expect.objectContaining({ tenantId: buyerTenantId, roleCode: 'advertiser_approver' }),
  ]))
  await chooseWorkspace(page, buyerTenantId)
  await page.goto('/marketplace')
  await expect(page.getByText('View only', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: /Requests/ })).toHaveCount(0)

  console.log('role-boundary: finance')
  await switchIdentity(page, financeUserId)
  const financeWorkspaces = await workspaces(page)
  expect(financeWorkspaces).toEqual(expect.arrayContaining([
    expect.objectContaining({ tenantId: buyerTenantId, roleCode: 'platform_admin' }),
  ]))
  await chooseWorkspace(page, buyerTenantId)
  await page.goto('/funding')
  await expect(page.getByRole('heading', { name: 'Finance', exact: true })).toBeVisible()
  console.log('role-boundary: complete')
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}

async function bootstrap(page: Page) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/bootstrap', {
    data: {},
    headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
  expect(await response.json()).toMatchObject({ buyerTenantId, supplierTenantId })
}

async function switchIdentity(page: Page, userId: string) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId },
    headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
  const next = await response.json() as Session
  expect(next.antiforgeryToken).toBeTruthy()
}

async function sessionFor(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as Session
}

async function workspaces(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as Workspace[]
}

async function chooseWorkspace(page: Page, tenantId: string) {
  const rows = await workspaces(page)
  expect(rows.some(item => item.tenantId === tenantId)).toBe(true)
  await page.evaluate((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
}

function mutationHeaders(token: string) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}
