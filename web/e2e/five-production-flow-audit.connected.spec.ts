import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const reportPath = resolve(process.cwd(), '..', 'artifacts', 'production-readiness', 'preview', 'haiku45-five-briefs.json')

type CertifiedCase = {
  key: string
  title: string
  briefId: string
  briefVersionId: string
}
type HaikuReport = { cases: CertifiedCase[] }

test('audit the five retained Haiku cases for complete production-flow readiness', async ({ page }) => {
  await signIn(page)
  const session = await getJson<{ antiforgeryToken: string }>(page, '/api/v1/session')
  expect(session.antiforgeryToken).toBeTruthy()
  const workspaces = await getJson<Array<{ tenantId: string; name: string }>>(page, '/api/v1/workspaces')
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace).toBeTruthy()

  const certified = JSON.parse(readFileSync(reportPath, 'utf8')) as HaikuReport
  expect(certified.cases).toHaveLength(5)
  const summaries = []
  for (const item of certified.cases) {
    const tenantId = workspace.tenantId
    const brief = await getJson<Record<string, unknown>>(page, `/api/v1/tenants/${tenantId}/briefs/${item.briefId}`)
    const planning = await getJson<Record<string, unknown>>(page, `/api/v1/tenants/${tenantId}/brief-versions/${item.briefVersionId}/planning`)
    const strategy = await getMaybeJson<Record<string, unknown>>(page,
      `/api/v1/tenants/${tenantId}/brief-versions/${item.briefVersionId}/intelligence/media-strategy`)
    const proposals = await getJson<unknown[]>(page, `/api/v1/tenants/${tenantId}/proposals`)
    const relevantProposals = proposals.filter(value => JSON.stringify(value).includes(item.briefId))
    summaries.push({
      key: item.key,
      briefId: item.briefId,
      briefVersionId: item.briefVersionId,
      brief: briefSummary(brief),
      strategy: strategySummary(strategy),
      planning: planningSummary(planning),
      proposalCount: relevantProposals.length,
    })
  }
  console.log('FIVE_FLOW_AUDIT=' + JSON.stringify(summaries, null, 2))
})

function briefSummary(value: Record<string, unknown>) {
  const versions = value.versions as Array<Record<string, unknown>> | undefined
  const current = versions?.at(-1)
  return current ? {
    status: current.status,
    timing: current.timing,
    budgetMinor: current.budgetMinor,
    budgetUnknown: current.budgetUnknown,
    currency: current.currency,
    geographies: current.geographies,
    mediaRequirements: current.mediaRequirements,
    measurement: current.measurement,
  } : null
}

function strategySummary(value: Record<string, unknown> | null) {
  if (!value) return null
  const artifactJson = value.artifactJson
  if (typeof artifactJson !== 'string') return { status: value.status }
  const artifact = JSON.parse(artifactJson) as Record<string, unknown>
  return {
    status: value.status,
    channels: (artifact.channelRecommendations as Array<Record<string, unknown>> | undefined)?.map(item => ({
      channel: item.channel,
      budgetGuidancePercent: item.budgetGuidancePercent,
      role: item.role,
    })) ?? [],
  }
}

function planningSummary(value: Record<string, unknown>) {
  return {
    campaignMode: record(value.campaignMode)?.mode ?? null,
    audienceStatus: record(value.audience)?.status ?? null,
    mix: mixSummary(record(value.mediaMix)),
    shortlist: shortlistSummary(record(value.shortlist)),
    plan: planSummary(record(value.mediaPlan)),
  }
}

function record(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null
}

function mixSummary(mix: Record<string, unknown> | null) {
  return mix ? { id: mix.id, status: mix.status, version: mix.version, allocations: mix.allocations } : null
}

function shortlistSummary(shortlist: Record<string, unknown> | null) {
  if (!shortlist) return null
  const rows = shortlist.candidates as Array<Record<string, unknown>> | undefined ?? []
  return { id: shortlist.id, status: shortlist.status, candidates: rows.length,
    selected: rows.filter(item => item.isSelected === true).length,
    eligibleByChannel: eligibleByChannel(shortlist) }
}

function planSummary(plan: Record<string, unknown> | null) {
  if (!plan) return null
  return { id: plan.id, status: plan.status, version: plan.version, totalMinor: plan.totalMinor,
    lines: (plan.lines as unknown[] | undefined)?.length ?? 0, objections: plan.objections }
}

function eligibleByChannel(shortlist: Record<string, unknown>) {
  const rows = shortlist.candidates as Array<Record<string, unknown>> | undefined ?? []
  return rows.filter(item => item.isEligible === true).reduce<Record<string, number>>((acc, item) => {
    const key = String(item.channel ?? 'UNKNOWN')
    acc[key] = (acc[key] ?? 0) + 1
    return acc
  }, {})
}

async function getJson<T>(page: Page, path: string): Promise<T> {
  const response = await page.request.get(path)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as T
}

async function getMaybeJson<T>(page: Page, path: string): Promise<T | null> {
  const response = await page.request.get(path)
  if (response.status() === 404) return null
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as T
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
