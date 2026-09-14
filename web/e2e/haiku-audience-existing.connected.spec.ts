import { expect, test, type Page } from '@playwright/test'

type Workspace = { tenantId: string; name: string }
type BriefSummary = { id: string; title: string; approvedVersionId?: string | null; readyVersionId?: string | null }
type Planning = { audience: unknown | null }

test('fresh approved Brief can generate Audience Intelligence with Haiku 4.5', async ({ page }) => {
  await signIn(page)
  const workspace = await currentWorkspace(page)
  const briefsResponse = await page.request.get(`/api/v1/tenants/${workspace.tenantId}/briefs`)
  expect(briefsResponse.ok(), await briefsResponse.text()).toBe(true)
  const briefs = await briefsResponse.json() as BriefSummary[]
  const explicit = process.env.ADVERTIFIED_CONNECTED_BRIEF_VERSION_ID
  const target = explicit
    ? briefs.find(item => (item.approvedVersionId ?? item.readyVersionId) === explicit)
    : await newestBriefWithoutAudience(page, workspace.tenantId, briefs)
  expect(target, explicit
    ? `Could not find requested Brief version ${explicit}.`
    : 'A current approved Brief without Audience Intelligence is required.').toBeTruthy()
  const versionId = target!.approvedVersionId ?? target!.readyVersionId
  expect(versionId).toBeTruthy()
  console.log(`AUDIENCE_TARGET=${versionId} ${target!.title}`)

  const sessionResponse = await page.request.get('/api/v1/session')
  expect(sessionResponse.ok(), await sessionResponse.text()).toBe(true)
  const session = await sessionResponse.json() as { antiforgeryToken: string }
  const response = await page.request.post(
    `/api/v1/tenants/${workspace.tenantId}/brief-versions/${versionId}/audiences:generate`,
    {
      data: {},
      headers: {
        Origin: 'http://localhost:3017',
        'X-CSRF-TOKEN': session.antiforgeryToken,
        'Idempotency-Key': `haiku-audience-${Date.now()}`,
        'X-Correlation-ID': crypto.randomUUID(),
      },
    },
  )
  const body = await response.text()
  expect(response.status(), body).toBe(200)
  const audience = JSON.parse(body) as { definitions: unknown[]; status: string }
  expect(audience.definitions.length).toBeGreaterThan(0)
  expect(audience.status).toBe('DRAFT')
})

async function newestBriefWithoutAudience(page: Page, tenantId: string, briefs: BriefSummary[]) {
  for (const brief of briefs) {
    const versionId = brief.approvedVersionId ?? brief.readyVersionId
    if (!versionId) continue
    const response = await page.request.get(`/api/v1/tenants/${tenantId}/brief-versions/${versionId}/planning`)
    if (!response.ok()) continue
    const planning = await response.json() as Planning
    if (!planning.audience) return brief
  }
  return undefined
}

async function currentWorkspace(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const workspaces = await response.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace).toBeTruthy()
  return workspace!
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
