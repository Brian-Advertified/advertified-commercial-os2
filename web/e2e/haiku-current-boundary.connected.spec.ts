import { expect, test, type Page } from '@playwright/test'

type Workspace = { tenantId: string; name: string }
type Session = { antiforgeryToken: string }
type Brief = { title: string; approvedVersionId: string | null }
type Audience = {
  id: string
  version: number
  status: string
  targetAudienceIds: string[]
  targetingRationale: string | null
  positioningStatement: string | null
}
type Strategy = { id: string; version: number; status: string; artifactJson: string }

const TITLE = 'Haiku 4.5 — Church attendance decline'

test('current compact Haiku boundaries re-certify the complex Church case', async ({ page }) => {
  await signIn(page)
  const workspace = await workspaceFor(page)
  const session = await sessionFor(page)
  const brief = await approvedBrief(page, workspace.tenantId)
  const versionId = brief.approvedVersionId!

  const audience = await post<Audience>(page,
    `/api/v1/tenants/${workspace.tenantId}/brief-versions/${versionId}/audiences:generate`, {}, session.antiforgeryToken)
  expect(audience.status).toBe('DRAFT')
  expect(audience.targetAudienceIds.length).toBeGreaterThan(0)
  const approvedAudience = await post<Audience>(page,
    `/api/v1/tenants/${workspace.tenantId}/audience-strategies/${audience.id}:approve`, {
      targetAudienceIds: audience.targetAudienceIds,
      targetingRationale: audience.targetingRationale ?? 'Retain the Brief-required audiences within governed evidence boundaries.',
      positioningStatement: audience.positioningStatement ?? 'Use only approved audience context in downstream planning.',
      reason: 'Current Haiku 4.5 provider-boundary certification.',
    }, session.antiforgeryToken, audience.version)
  expect(approvedAudience.status).toBe('APPROVED')

  const strategy = await post<Strategy>(page,
    `/api/v1/tenants/${workspace.tenantId}/brief-versions/${versionId}/intelligence/media-strategy`, {}, session.antiforgeryToken)
  expect(strategy.status).toBe('DRAFT')
  const payload = JSON.parse(strategy.artifactJson) as { channelRecommendations: unknown[] }
  expect(payload.channelRecommendations.length).toBeGreaterThan(0)
  const approvedStrategy = await post<Strategy>(page,
    `/api/v1/tenants/${workspace.tenantId}/brief-versions/${versionId}/intelligence/media-strategy/${strategy.id}/approve`,
    { expectedVersion: strategy.version }, session.antiforgeryToken)
  expect(approvedStrategy.status).toBe('APPROVED')
})

async function workspaceFor(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Workspace[]
  return rows.find(item => /Advertified Local/i.test(item.name)) ?? rows[0]
}

async function sessionFor(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Session
}

async function approvedBrief(page: Page, tenantId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/briefs`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Brief[]
  const brief = rows.find(item => item.title === TITLE)
  expect(brief?.approvedVersionId).toBeTruthy()
  return brief!
}

async function post<T>(page: Page, path: string, data: unknown, token: string, version?: number) {
  const headers: Record<string, string> = {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
  if (version !== undefined) headers['If-Match'] = `"${version}"`
  const response = await page.request.post(path, { data, headers })
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as T
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
