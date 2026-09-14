import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'

type Session = { antiforgeryToken: string }
type Campaign = { id: string; title: string; status: string }

test('LIVE Marketplace campaign completes after the certified delivery window and requests proof', async ({ page }) => {
  test.setTimeout(60_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const campaign = await targetCampaign(page)
  if (campaign.status === 'COMPLETED') return
  expect(campaign.status).toBe('LIVE')

  await page.goto(`/campaigns/${campaign.id}#live-stage`)
  await expect(page.getByRole('button', { name: 'Complete and request proof', exact: true })).toBeVisible()
  await page.getByLabel('Completion reason').fill('The certified booked October delivery window has closed.')
  await page.getByLabel('Supplier proof request').fill('Submit retained delivery evidence for each exact supplier-confirmed booking and booked flight.')
  await mutate(page, page.getByRole('button', { name: 'Complete and request proof', exact: true }), /\/campaigns\/[^/]+:complete$/, [200])

  const next = await getCampaign(page, campaign.id)
  expect(next.status).toBe('COMPLETED')
})

async function targetCampaign(page: Page): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Campaign[]
  const candidate = rows.find(item => /Connected Marketplace OOH Proposal/i.test(item.title))
  expect(candidate).toBeTruthy()
  return candidate!
}

async function getCampaign(page: Page, id: string): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns/${id}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Campaign
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
