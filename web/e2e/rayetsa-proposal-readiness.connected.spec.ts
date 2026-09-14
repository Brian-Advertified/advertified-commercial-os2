import { expect, test, type Page } from '@playwright/test'

const BRIEF_ID = 'a314a1b2-7459-4878-b673-0fb145463991'
const BRIEF_VERSION_ID = 'c4fcbe9d-77ca-4367-b2f8-f72f0d4a8c04'

test('Rayetsa exposes planning readiness before proposal preparation', async ({ page }) => {
  await signIn(page)
  const tenantId = await localTenant(page)
  const planningResponse = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${BRIEF_VERSION_ID}/planning`,
  )
  expect(planningResponse.ok(), await planningResponse.text()).toBe(true)
  const planning = await planningResponse.json() as Planning
  expect(planning.mediaMix, 'Rayetsa should retain its strategy-derived media mix').not.toBeNull()

  const plansResponse = await page.request.get(`/api/v1/tenants/${tenantId}/briefs/${BRIEF_ID}/approved-plans`)
  expect(plansResponse.ok(), await plansResponse.text()).toBe(true)
  const plans = await plansResponse.json() as unknown[]
  console.log('RAYETSA_READINESS=' + JSON.stringify({
    mixStatus: planning.mediaMix?.status ?? null,
    shortlistStatus: planning.shortlist?.status ?? null,
    eligibleByChannel: eligibleByChannel(planning.shortlist),
    planStatus: planning.mediaPlan?.status ?? null,
    approvedPlanCount: plans.length,
  }))
})

type Planning = {
  mediaMix: { status: string } | null
  shortlist: { status: string; candidates: Array<{ channel: string; isEligible: boolean }> } | null
  mediaPlan: { status: string } | null
}

function eligibleByChannel(shortlist: Planning['shortlist']) {
  if (!shortlist) return {}
  return shortlist.candidates.filter(item => item.isEligible).reduce<Record<string, number>>((acc, item) => {
    acc[item.channel] = (acc[item.channel] ?? 0) + 1
    return acc
  }, {})
}

async function localTenant(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const workspaces = await response.json() as Array<{ tenantId: string; name: string }>
  return (workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]).tenantId
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
