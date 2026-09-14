import { expect, test, type Page } from '@playwright/test'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

type Workspace = { tenantId: string; name: string }
type Brief = {
  id: string
  title: string
  status: string
  approvedVersionId: string | null
  updatedAtUtc: string
}
type Planning = {
  audience: { status: string } | null
  campaignMode: { mode: string } | null
}
type Strategy = { id: string; status: string; artifactJson: string }
type Usage = {
  agentCode: string
  status: string
  provider: string
  model: string
  recordedAtUtc: string
}
type AgentOperations = { recentUsage: Usage[] }
type HumanTaskPage = { items: Array<{ status: string; resourceId: string; briefId: string | null }>; nextCursor: string | null }
type CampaignState = {
  title: string
  briefId: string
  briefVersionId: string
  mode: string | undefined
  audienceStatus: string | undefined
  strategyId: string
  strategyStatus: string
}

const EXPECTED = [
  'Haiku 4.5 — Takealot Black Friday DOOH',
  'Haiku 4.5 — Mukuru remittance OOH',
  'Haiku 4.5 — Rayetsa Furniture growth',
  'Haiku 4.5 — Vaccination awareness 2026',
  'Haiku 4.5 — Church attendance decline',
]
const MODEL = 'global.anthropic.claude-haiku-4-5-20251001-v1:0'

test('retained Haiku certification state is exactly five approved live campaigns', async ({ page }) => {
  await signIn(page)
  const workspace = await loadWorkspace(page)
  const briefs = await loadBriefs(page, workspace.tenantId)
  const campaigns = await verifyCampaigns(page, workspace.tenantId, briefs)
  const relevantUsage = await verifyUsage(page, workspace.tenantId, briefs)
  const commercialState = await verifyCommercialState(page, workspace.tenantId, briefs)
  writeVerification(briefs, campaigns, relevantUsage, commercialState)
})

async function loadWorkspace(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const workspaces = await response.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace).toBeTruthy()
  return workspace
}

async function loadBriefs(page: Page, tenantId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/briefs`)
  expect(response.ok(), await response.text()).toBe(true)
  const briefs = await response.json() as Brief[]
  expect(briefs.length, 'Only the five certification Briefs should remain').toBe(5)
  expect(new Set(briefs.map(item => item.title))).toEqual(new Set(EXPECTED))
  return briefs
}

async function verifyCampaigns(page: Page, tenantId: string, briefs: Brief[]) {
  const state: CampaignState[] = []
  for (const title of EXPECTED) {
    const matches = briefs.filter(item => item.title === title)
    expect(matches.length, `${title}: exactly one retained Brief`).toBe(1)
    const brief = matches[0]
    expect(brief.status, `${title}: Brief status`).toBe('APPROVED')
    expect(brief.approvedVersionId, `${title}: approved Brief version`).toBeTruthy()
    const briefVersionId = brief.approvedVersionId!
    const planning = await loadPlanning(page, tenantId, briefVersionId, title)
    const strategy = await loadStrategy(page, tenantId, briefVersionId, title)
    state.push({
      title, briefId: brief.id, briefVersionId,
      mode: planning.campaignMode?.mode,
      audienceStatus: planning.audience?.status,
      strategyId: strategy.id, strategyStatus: strategy.status,
    })
  }
  return state
}

async function loadPlanning(page: Page, tenantId: string, briefVersionId: string, title: string) {
  const response = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`,
  )
  expect(response.ok(), await response.text()).toBe(true)
  const planning = await response.json() as Planning
  expect(planning.campaignMode, `${title}: campaign mode`).not.toBeNull()
  expect(planning.audience?.status, `${title}: Audience Intelligence`).toBe('APPROVED')
  return planning
}

async function loadStrategy(page: Page, tenantId: string, briefVersionId: string, title: string) {
  const response = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/intelligence/media-strategy`,
  )
  expect(response.ok(), await response.text()).toBe(true)
  const strategy = await response.json() as Strategy
  expect(strategy.status, `${title}: Media Strategy`).toBe('APPROVED')
  return strategy
}

async function verifyUsage(page: Page, tenantId: string, briefs: Brief[]) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/agent-operations`)
  expect(response.ok(), await response.text()).toBe(true)
  const operations = await response.json() as AgentOperations
  const start = Math.min(...briefs.map(item => new Date(item.updatedAtUtc).getTime())) - 10 * 60 * 1000
  const relevant = operations.recentUsage.filter(item =>
    ['brief_drafting', 'audience_intelligence', 'media_strategy'].includes(item.agentCode) &&
    new Date(item.recordedAtUtc).getTime() >= start,
  )
  expect(relevant.length, 'Live certification should retain provider usage receipts').toBeGreaterThanOrEqual(15)
  const wrongModel = relevant.filter(item => item.provider !== 'bedrock' || item.model !== MODEL)
  expect(wrongModel, 'No deterministic/Nova/other-model receipt is allowed in certification usage').toEqual([])
  return relevant
}

async function verifyCommercialState(page: Page, tenantId: string, briefs: Brief[]) {
  const proposalsResponse = await page.request.get(`/api/v1/tenants/${tenantId}/proposals`)
  expect(proposalsResponse.ok(), await proposalsResponse.text()).toBe(true)
  const proposals = await proposalsResponse.json() as unknown[]
  expect(proposals, 'Certification stops before proposal generation').toEqual([])

  const tasksResponse = await page.request.get(`/api/v1/tenants/${tenantId}/human-tasks?limit=100`)
  expect(tasksResponse.ok(), await tasksResponse.text()).toBe(true)
  const tasks = await tasksResponse.json() as HumanTaskPage
  const retainedIds = new Set(briefs.flatMap(item => [item.id, item.approvedVersionId].filter(Boolean)))
  const foreignTasks = tasks.items.filter(item =>
    (item.briefId !== null && !retainedIds.has(item.briefId)) ||
    (!retainedIds.has(item.resourceId) && item.briefId === null),
  )
  expect(foreignTasks, 'No task from cleaned/temporary Briefs should remain').toEqual([])
  return {
    proposalCount: proposals.length,
    taskCount: tasks.items.length,
    taskStatuses: [...new Set(tasks.items.map(item => item.status))],
  }
}

function writeVerification(
  briefs: Brief[], campaigns: CampaignState[], relevantUsage: Usage[],
  commercialState: { proposalCount: number; taskCount: number; taskStatuses: string[] },
) {
  const outputDirectory = resolve(process.cwd(), '..', 'artifacts', 'production-readiness', 'preview')
  mkdirSync(outputDirectory, { recursive: true })
  const outputPath = resolve(outputDirectory, 'haiku45-certification-verification.json')
  writeFileSync(outputPath, JSON.stringify({
    schemaVersion: 'advertified.haiku45-certification-verification.v1',
    generatedAtUtc: new Date().toISOString(),
    modelExpected: MODEL,
    retainedBriefCount: briefs.length,
    campaigns,
    relevantUsageCount: relevantUsage.length,
    providerModels: [...new Set(relevantUsage.map(item => `${item.provider}|${item.model}`))],
    commercialState,
  }, null, 2) + '\n', 'utf8')
  console.log(`Haiku certification verification: ${outputPath}`)
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
