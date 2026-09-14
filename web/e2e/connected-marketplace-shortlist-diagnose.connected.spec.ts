import { expect, test, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const briefVersionId = 'bb69be63-5454-4ec1-acd1-fd909746608f'

type Session = { antiforgeryToken: string }
type Candidate = {
  name: string
  channel: string
  geography: string
  inventoryTenantId: string
  marketplaceListingVersionId: string | null
  rateAmountMinor: number | null
  currency: string | null
  isEligible: boolean
  isSelected: boolean | null
  rejectionReason: string | null
  rejectionDetail: string | null
  commercialReadiness?: unknown
  commercialTerms?: unknown
  deliverable?: unknown
  spatial?: unknown
}

test('diagnose exact connected Marketplace planning state', async ({ page }) => {
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const response = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`,
  )
  expect(response.ok(), await response.text()).toBe(true)
  const planning = await response.json() as {
    mediaMix?: { id: string; status: string; totalBudgetMinor: number } | null
    shortlist?: { id: string; mixVersionId: string; status: string; candidates: Candidate[] } | null
    mediaPlan?: { id: string; mixVersionId: string; status: string; lines: unknown[] } | null
  }
  const candidates = planning.shortlist?.candidates ?? []
  const local = candidates.filter(candidate => /Local Demo/i.test(candidate.name))
  console.log(JSON.stringify({
    briefVersionId,
    mediaMix: planning.mediaMix,
    shortlist: planning.shortlist && {
      id: planning.shortlist.id,
      mixVersionId: planning.shortlist.mixVersionId,
      status: planning.shortlist.status,
      selectedCount: candidates.filter(item => item.isSelected).length,
      eligibleCount: candidates.filter(item => item.isEligible).length,
    },
    mediaPlan: planning.mediaPlan,
    local,
  }, null, 2))
})

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
  await page.evaluate((workspaceTenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: workspaceTenantId }))
  }, id)
}

function mutationHeaders(token: string) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}
