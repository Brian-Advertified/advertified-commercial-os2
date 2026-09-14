import { expect, test, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const briefVersionId = '2b06dd9c-31de-4f74-a281-3ea070158570'

type Session = { antiforgeryToken: string }
type Candidate = {
  id: string
  isSelected: boolean | null
  name: string
  channel: string
  rateAmountMinor: number | null
  commercialReadiness: null | { supplierVatStatus: string | null; vatTreatment: string | null; evidenceGaps: string[]; rateType: string | null }
  supplierCommercial: unknown
  commercialTerms: unknown
}

test('diagnose media-plan generation from a confirmed Marketplace shortlist', async ({ page }) => {
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const before = await workspace(page)
  console.log('before', JSON.stringify(summarize(before), null, 2))
  expect(before.shortlist?.status).toBe('APPROVED')
  expect(before.shortlist?.candidates.filter(item => item.isSelected).length).toBeGreaterThan(0)

  await page.goto(`/planning/${briefVersionId}#shortlist-workbench`)
  const build = page.getByRole('button', { name: 'Build client-ready media plan', exact: true })
  await expect(build).toBeVisible()

  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    /\/brief-versions\/[^/]+\/media-plans:generate$/.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await build.click()
  const response = await responsePromise
  console.log('generate-status', response.status(), await response.text())

  const after = await workspace(page)
  console.log('after', JSON.stringify(summarize(after), null, 2))
  expect(response.status()).toBe(200)
  expect(after.shortlist?.status).toBe('APPROVED')
  expect(after.shortlist?.candidates.filter(item => item.isSelected).length).toBeGreaterThan(0)
  expect(after.mediaPlan).not.toBeNull()
})

async function workspace(page: Page) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as {
    mediaMix: null | { id: string; status: string; totalBudgetMinor: number; allocations: Array<{ channel: string; budgetMinor: number }> }
    shortlist: null | { id: string; mixVersionId: string; status: string; candidates: Candidate[] }
    mediaPlan: null | { id: string; mixVersionId: string; status: string; lines: unknown[] }
  }
}

function summarize(value: Awaited<ReturnType<typeof workspace>>) {
  return {
    mediaMix: value.mediaMix,
    shortlist: value.shortlist && {
      id: value.shortlist.id,
      mixVersionId: value.shortlist.mixVersionId,
      status: value.shortlist.status,
      selected: value.shortlist.candidates.filter(item => item.isSelected).map(item => ({
        name: item.name,
        channel: item.channel,
        rateAmountMinor: item.rateAmountMinor,
        commercialReadiness: item.commercialReadiness,
        supplierCommercial: item.supplierCommercial,
        commercialTerms: item.commercialTerms,
      })),
    },
    mediaPlan: value.mediaPlan && {
      id: value.mediaPlan.id,
      mixVersionId: value.mediaPlan.mixVersionId,
      status: value.mediaPlan.status,
      lineCount: value.mediaPlan.lines.length,
    },
  }
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
