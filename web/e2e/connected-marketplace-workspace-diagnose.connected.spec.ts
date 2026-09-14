import { expect, test, type Page } from '@playwright/test'
import { shortlistSchema } from '../src/api/planning-schemas'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'

type Session = { antiforgeryToken: string }

test('diagnose latest connected Marketplace shortlist browser contract', async ({ page }) => {
  await signIn(page)
  await switchIdentity(page, buyerUserId)
  const briefsResponse = await page.request.get(`/api/v1/tenants/${tenantId}/briefs`)
  expect(briefsResponse.ok(), await briefsResponse.text()).toBe(true)
  const briefs = await briefsResponse.json() as Array<{
    title: string; approvedVersionId: string | null; createdAtUtc?: string
  }>
  const brief = briefs
    .filter(item => item.title.startsWith('Connected Marketplace Campaign ') && item.approvedVersionId)
    .sort((left, right) => left.title.localeCompare(right.title))
    .at(-1)
  expect(brief?.approvedVersionId).toBeTruthy()
  const response = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${brief!.approvedVersionId}/planning`,
  )
  expect(response.ok(), await response.text()).toBe(true)
  const workspace = await response.json() as {
    shortlist: unknown
    mediaMix?: { id: string; status: string; allocations?: unknown[] } | null
    mediaPlan?: unknown
  }
  const parsed = workspace.shortlist ? shortlistSchema.safeParse(workspace.shortlist) : null
  const shortlist = workspace.shortlist as null | { candidates?: Array<Record<string, unknown>> }
  const candidates = shortlist?.candidates ?? []
  const local = candidates.filter(item =>
    item.inventoryProductId === '10000000-0000-0000-0000-000000000110' ||
    item.inventoryProductId === '10000000-0000-0000-0000-000000000111')
  console.log(JSON.stringify({
    title: brief!.title,
    approvedVersionId: brief!.approvedVersionId,
    mediaMix: workspace.mediaMix,
    mediaPlan: workspace.mediaPlan,
    shortlistPresent: Boolean(workspace.shortlist),
    valid: parsed?.success ?? null,
    issues: parsed && !parsed.success ? parsed.error.issues : [],
    candidateCount: candidates.length,
    eligibleCount: candidates.filter(item => item.isEligible === true).length,
    local,
  }, null, 2))
  if (parsed) expect(parsed.success).toBe(true)
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}

async function switchIdentity(page: Page, userId: string) {
  const sessionResponse = await page.request.get('/api/v1/session')
  expect(sessionResponse.ok(), await sessionResponse.text()).toBe(true)
  const session = await sessionResponse.json() as Session
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId },
    headers: {
      Origin: 'http://localhost:3017',
      'X-CSRF-TOKEN': session.antiforgeryToken,
      'Idempotency-Key': crypto.randomUUID(),
      'X-Correlation-ID': crypto.randomUUID(),
    },
  })
  expect(response.ok(), await response.text()).toBe(true)
}
