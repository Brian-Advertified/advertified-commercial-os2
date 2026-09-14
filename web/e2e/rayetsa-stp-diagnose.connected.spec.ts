import { expect, test, type Page } from '@playwright/test'

const BRIEF_VERSION_ID = 'c4fcbe9d-77ca-4367-b2f8-f72f0d4a8c04'

test('diagnose Rayetsa STP research context', async ({ page }) => {
  await signIn(page)
  const workspaces = await (await page.request.get('/api/v1/workspaces')).json() as Array<{ tenantId: string; name: string }>
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  const response = await page.request.get(`/api/v1/tenants/${workspace.tenantId}/brief-versions/${BRIEF_VERSION_ID}/planning`)
  expect(response.ok(), await response.text()).toBe(true)
  const planning = await response.json() as {
    audience: { definitions: Array<{ name: string; lifeStage: string | null; lsmSem: string | null; lsmSemTaxonomy: string | null; lsmSemTaxonomyVersion: string | null; referenceObservationIds: string[]; evidenceItemIds: string[] }> } | null
    audienceResearch: { observations: Array<{ dimensions: Record<string, string>; metricCode: string; geographyName: string; sourceTitle: string; activationPolicy: string }> } | null
  }
  const observations = planning.audienceResearch?.observations ?? []
  const byGroup = observations.reduce<Record<string, number>>((acc, item) => {
    const key = item.dimensions.group ?? '(none)'
    acc[key] = (acc[key] ?? 0) + 1
    return acc
  }, {})
  console.log('GROUP_COUNTS=' + JSON.stringify(byGroup, null, 2))
  console.log('AGE_ROWS=' + JSON.stringify(observations.filter(item => ['AGE', 'AGE_GROUP'].includes(item.dimensions.group ?? '')).slice(0, 30), null, 2))
  console.log('LSM_ROWS=' + JSON.stringify(observations.filter(item => /LSM|SEM/i.test(JSON.stringify(item.dimensions))).slice(0, 30), null, 2))
  console.log('AUDIENCE_DEFINITIONS=' + JSON.stringify(planning.audience?.definitions ?? [], null, 2))
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
