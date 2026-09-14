import { expect, test, type Page } from '@playwright/test'

const TENANT = '10000000-0000-0000-0000-000000000002'
const VERSION = '3817c516-085c-47ff-8639-bf042e23c8e6'
const MARKETS = ['Pretoria', 'Midrand', 'Broader Gauteng', 'Mpumalanga', 'Limpopo']

test('manual Rayetsa strategy is review-ready using Brief timing and neutral market defaults', async ({ page }) => {
  await signIn(page)
  const session = await (await page.request.get('/api/v1/session')).json() as { antiforgeryToken: string }
  const planningResponse = await page.request.get(`/api/v1/tenants/${TENANT}/brief-versions/${VERSION}/planning`)
  expect(planningResponse.ok(), await planningResponse.text()).toBe(true)
  const planning = await planningResponse.json() as { mediaMix: any }
  expect(planning.mediaMix).toBeTruthy()
  let mix = planning.mediaMix

  const needsDefaults = mix.allocations.some((item: any) =>
    item.runningPeriods.length === 0 || item.geographyAllocations.length === 0)
  if (needsDefaults) {
    const allocations = mix.allocations.map((item: any) => ({
      ...item,
      runningPeriods: [{ start: '2026-10-01', end: '2026-12-31' }],
      geographyAllocations: splitBudget(item.budgetMinor),
    }))
    const response = await page.request.post(
      `/api/v1/tenants/${TENANT}/media-mix-versions/${mix.id}:update`,
      {
        data: {
          allocations,
          impactEstimate: mix.impactEstimate,
          reason: 'Seed approved Brief flight dates and neutral equal market allocations so the planner can review instead of re-entering supplied campaign facts.',
        },
        headers: commandHeaders(session.antiforgeryToken, mix.version),
      },
    )
    expect(response.status(), await response.text()).toBe(200)
    mix = await response.json()
  }

  expect(mix.allocations.every((item: any) => item.runningPeriods.length > 0)).toBe(true)
  expect(mix.allocations.every((item: any) => item.geographyAllocations.length === MARKETS.length)).toBe(true)

  await page.goto(`/planning/${VERSION}#strategy`)
  const approve = page.getByRole('button', { name: 'Approve strategy & continue' })
  await expect(approve).toBeVisible()
  await expect(approve).toBeEnabled()
})

function splitBudget(total: number) {
  const base = Math.floor(total / MARKETS.length)
  let assigned = 0
  return MARKETS.map((geography, index) => {
    const budgetMinor = index === MARKETS.length - 1 ? total - assigned : base
    assigned += budgetMinor
    return { geography, budgetMinor }
  })
}

function commandHeaders(token: string, version: number) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'If-Match': `"${version}"`,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
