import { expect, test, type Page } from '@playwright/test'

type Workspace = { tenantId: string; name: string }
type BriefSummary = { approvedVersionId?: string | null; readyVersionId?: string | null }

test('current Audience Strategy is actionable and has no dead section links', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()

  const briefVersionId = await currentPlanningBriefVersion(page)
  await page.goto(`/stp/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Audience Strategy' })).toBeVisible()
  await expect(page.locator('.approved-stp-page')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Segmentation', exact: true })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Targeting', exact: true })).toHaveCount(0)
  await expect(page.locator('a[href*="#stp-"]')).toHaveCount(0)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})

async function currentPlanningBriefVersion(page: Page) {
  const workspacesResponse = await page.request.get('/api/v1/workspaces')
  expect(workspacesResponse.ok()).toBe(true)
  const workspaces = await workspacesResponse.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace, 'A current workspace is required for the connected Audience Strategy check.').toBeTruthy()

  const briefsResponse = await page.request.get(`/api/v1/tenants/${workspace.tenantId}/briefs`)
  expect(briefsResponse.ok()).toBe(true)
  const briefs = await briefsResponse.json() as BriefSummary[]
  const versionId = briefs
    .map(item => item.approvedVersionId ?? item.readyVersionId ?? null)
    .find((value): value is string => Boolean(value))
  expect(versionId, 'A current approved or ready Brief is required for Audience Strategy.').toBeTruthy()
  return versionId!
}
