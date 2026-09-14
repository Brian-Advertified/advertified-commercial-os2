import { expect, test, type Locator, type Page } from '@playwright/test'

type Workspace = { tenantId: string; name: string }
type BriefSummary = { approvedVersionId?: string | null; readyVersionId?: string | null }
type PlanningSummary = {
  campaignMode?: unknown | null
  audience?: { definitions?: unknown[]; status?: string } | null
}

test('approved Audience & STP composition stays aligned to the visual reference', async ({ page }) => {
  await signIn(page)
  const briefVersionId = requestedAudienceBriefVersion() ?? await currentAudienceBriefVersion(page)
  if (process.env.ADVERTIFIED_AUDIENCE_REGENERATE === '1') await regenerateAudience(page, briefVersionId)
  await page.goto(`/stp/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Audience & STP', exact: true })).toBeVisible()
  await expect(page.locator('.connected-audience-page')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Save & Exit', exact: true })).toBeVisible()
  await expect(page.locator('.approved-flow-step-copy')).toHaveCount(7)
  await expect(page.locator('.approved-flow-step-copy').first()).toBeVisible()

  await ensureAudienceWorkspace(page)

  const top = page.locator('.connected-audience-top-grid')
  const priority = page.locator('.connected-audience-priority .connected-audience-summary-card')
  const geography = page.locator('.connected-geography-card')
  const evidence = page.locator('.connected-audience-main-panel')
  const insights = page.locator('.connected-audience-insights')
  const metrics = page.locator('.connected-audience-metric-card')
  const relevance = page.locator('.connected-audience-relevance')
  const strategyBasis = page.locator('.connected-strategy-basis > summary')

  await expect(top).toBeVisible()
  await expect(priority.first()).toBeVisible()
  await expect(geography).toBeVisible()
  await expect(evidence).toBeVisible()
  await expect(insights).toBeVisible()
  await expect(metrics).toHaveCount(6)
  await expect(relevance).toBeVisible()
  await expect(strategyBasis).toBeVisible()

  await expectSameRow(priority.first(), geography, 6)
  if (await priority.count() > 1) await expectSameRow(priority.nth(0), priority.nth(1), 6)
  await expectSameRow(evidence, insights, 6)

  const canvas = await page.locator('.connected-audience-page').boundingBox()
  const topBox = await top.boundingBox()
  const evidenceBox = await evidence.boundingBox()
  const insightsBox = await insights.boundingBox()
  expect(canvas).not.toBeNull()
  expect(topBox).not.toBeNull()
  expect(evidenceBox).not.toBeNull()
  expect(insightsBox).not.toBeNull()
  expect(topBox!.width / canvas!.width).toBeGreaterThan(.96)
  expect(evidenceBox!.width).toBeGreaterThan(insightsBox!.width * 1.8)

  await expect(page.locator('a[href*="#stp-"]')).toHaveCount(0)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  await verifyGovernedResearchContext(page)

  const rendered = {
    cards: (await page.locator('.connected-audience-summary-card, .audience-strategy-card').allTextContents()).map(value => value.trim()),
    enrichment: await optionalText(page.locator('.audience-enrichment-banner')),
    footer: await optionalText(page.locator('.connected-audience-approved-footer')),
    rationale: await audienceDirectionValue(page, 0),
    positioning: await audienceDirectionValue(page, 1),
  }
  const brief = await inspectBriefVersion(page, briefVersionId)
  console.log('Audience route inspection:', JSON.stringify({ briefVersionId, brief, ...rendered }))
})

async function verifyGovernedResearchContext(page: Page) {
  const provenance = page.locator('.connected-research-provenance')
  await expect(provenance).toContainText(/governed market-context observations available/i)
  await expect(provenance).toContainText(/Gauteng/)
  await expect(provenance).toContainText(/Western Cape/)
  await expect(provenance).toContainText(/KwaZulu-Natal/)
  await expect(page.locator('.connected-gender-card')).toContainText('SA market context')
  await expect(page.locator('.connected-gender-card')).toContainText(/51\.5%|48\.5%/)
  await expect(page.locator('.connected-channel-affinity')).toContainText('Digital Access Context')
  await expect(page.locator('.connected-channel-affinity')).toContainText('%')
  await page.getByRole('tab', { name: 'Channel Affinity' }).click()
  await expect(page.locator('.connected-audience-context-research')).toBeVisible()
  await expect(page.locator('.connected-audience-context-research')).toContainText(/Gauteng/)
  await page.getByRole('tab', { name: 'Demographics' }).click()
}

function requestedAudienceBriefVersion() {
  const value = process.env.ADVERTIFIED_AUDIENCE_BRIEF_VERSION?.trim()
  if (!value) return null
  expect(value).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)
  return value
}

async function regenerateAudience(page: Page, briefVersionId: string) {
  const workspacesResponse = await page.request.get('/api/v1/workspaces')
  expect(workspacesResponse.ok()).toBe(true)
  const workspaces = await workspacesResponse.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace, 'A current workspace is required for Audience Intelligence regeneration.').toBeTruthy()
  const sessionResponse = await page.request.get('/api/v1/session')
  expect(sessionResponse.ok()).toBe(true)
  const session = await sessionResponse.json() as { antiforgeryToken: string }
  const response = await page.request.post(
    `/api/v1/tenants/${workspace!.tenantId}/brief-versions/${briefVersionId}/audiences:generate`,
    {
      data: {},
      headers: {
        Origin: 'http://localhost:3017',
        'X-CSRF-TOKEN': session.antiforgeryToken,
        'Idempotency-Key': `audience-regenerate-${briefVersionId}-${Date.now()}`,
        'X-Correlation-ID': crypto.randomUUID(),
      },
    },
  )
  const body = await response.text()
  expect(response.status(), body).toBe(200)
  console.log('Audience regeneration completed for', briefVersionId)
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
}

async function ensureAudienceWorkspace(page: Page) {
  const start = page.getByRole('button', { name: /Research & build audience strategy/i })
  if (await firstVisible(start)) {
    await Promise.all([
      page.waitForResponse(response => response.url().includes('/audiences:generate') && response.status() === 200),
      start.first().click(),
    ])
  }
  const rebuild = page.getByRole('button', { name: /Research & rebuild strategy/i })
  if (await firstVisible(rebuild)) {
    await Promise.all([
      page.waitForResponse(response => response.url().includes('/audiences:generate') && response.status() === 200),
      rebuild.first().click(),
    ])
  }
  await expect(page.locator('.connected-audience-top-grid')).toBeVisible()
}

async function currentAudienceBriefVersion(page: Page) {
  const workspacesResponse = await page.request.get('/api/v1/workspaces')
  expect(workspacesResponse.ok()).toBe(true)
  const workspaces = await workspacesResponse.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace, 'A current workspace is required for the connected Audience & STP check.').toBeTruthy()

  const briefsResponse = await page.request.get(`/api/v1/tenants/${workspace.tenantId}/briefs`)
  expect(briefsResponse.ok()).toBe(true)
  const briefs = await briefsResponse.json() as BriefSummary[]
  const candidates = briefs.map(item => item.approvedVersionId ?? item.readyVersionId ?? null)
    .filter((value): value is string => Boolean(value))
  let fallback: string | null = null
  for (const versionId of candidates) {
    const response = await page.request.get(`/api/v1/tenants/${workspace.tenantId}/brief-versions/${versionId}/planning`)
    if (!response.ok()) continue
    const planning = await response.json() as PlanningSummary
    if (!planning.campaignMode) continue
    fallback ??= versionId
    if ((planning.audience?.definitions?.length ?? 0) >= 2) return versionId
  }
  expect(fallback, 'A campaign-mode-bound Brief is required for Audience & STP.').toBeTruthy()
  return fallback!
}

async function expectSameRow(left: Locator, right: Locator, tolerance: number) {
  const first = await left.boundingBox()
  const second = await right.boundingBox()
  expect(first).not.toBeNull()
  expect(second).not.toBeNull()
  expect(Math.abs(first!.y - second!.y)).toBeLessThanOrEqual(tolerance)
}

async function inspectBriefVersion(page: Page, briefVersionId: string) {
  const workspacesResponse = await page.request.get('/api/v1/workspaces')
  expect(workspacesResponse.ok()).toBe(true)
  const workspaces = await workspacesResponse.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace).toBeTruthy()
  const planningResponse = await page.request.get(
    `/api/v1/tenants/${workspace!.tenantId}/brief-versions/${briefVersionId}/planning`,
  )
  expect(planningResponse.ok()).toBe(true)
  const planning = await planningResponse.json() as { briefId: string }
  const briefResponse = await page.request.get(`/api/v1/tenants/${workspace!.tenantId}/briefs/${planning.briefId}`)
  expect(briefResponse.ok()).toBe(true)
  const brief = await briefResponse.json() as {
    versions: Array<{
      id: string
      businessProblem: string
      objective: string
      audiences: string[]
      geographies: string[]
      mediaRequirements: string[]
      audienceResearch?: unknown[] | null
    }>
  }
  const version = brief.versions.find(item => item.id === briefVersionId)
  expect(version, 'The requested Brief version must exist in the canonical Brief.').toBeTruthy()
  return {
    businessProblem: version!.businessProblem,
    objective: version!.objective,
    audiences: version!.audiences,
    geographies: version!.geographies,
    mediaRequirements: version!.mediaRequirements,
    audienceResearchCount: version!.audienceResearch?.length ?? 0,
  }
}

async function optionalText(locator: Locator) {
  return await locator.count() ? (await locator.first().textContent())?.trim() ?? null : null
}

async function audienceDirectionValue(page: Page, index: number) {
  const textarea = page.locator('.connected-strategy-basis textarea').nth(index)
  if (await textarea.count()) return (await textarea.inputValue()).trim() || null
  const item = page.locator('.audience-strategy-direction.is-readonly > article').nth(index)
  if (!await item.count()) return null
  return await optionalText(item.locator('p'))
}

async function firstVisible(locator: Locator) {
  for (let index = 0; index < await locator.count(); index += 1) {
    if (await locator.nth(index).isVisible()) return true
  }
  return false
}
