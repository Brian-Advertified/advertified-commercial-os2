import { expect, test, type Page } from '@playwright/test'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

test.setTimeout(12 * 60 * 1000)

type Workspace = { tenantId: string; name: string }
type Session = { antiforgeryToken: string }
type BriefSummary = {
  id: string
  title: string
  approvedVersionId: string | null
}
type Audience = {
  id: string
  version: number
  status: string
  targetAudienceIds: string[]
  targetingRationale: string | null
  positioningStatement: string | null
  definitions: Array<{
    id: string
    name: string
    description: string
    needState: string | null
    buyingContext: string | null
    geographies: string[]
    classification: string
    confidence: number | null
  }>
}
type MediaStrategyRecord = {
  id: string
  version: number
  artifactJson: string
  status: string
}
type MediaStrategyPayload = {
  summary: string
  channelRecommendations: Array<{
    channel: string
    role: string
    rationale: string
    objectiveContribution: string
    geographyRole: string | null
    classification: string
    budgetGuidancePercent: number | null
    tradeOffs: string[]
    evidenceGaps: string[]
  }>
  strategicPrinciples: string[]
  excludedChannels: string[]
  evidenceGaps: string[]
}
type PlanningWorkspace = {
  briefId: string
  briefVersionId: string
  campaignMode: { mode: string; allowedChannels: string[] } | null
  audience: Audience | null
}

type Case = {
  key: string
  title: string
  expectedMode: 'OOH_ONLY' | 'FULL_CAMPAIGN'
  source: string
  expectedGeographies: string[]
}

type CompletedCase = {
  planning: PlanningWorkspace
  approvedAudience: Audience
  approvedStrategy: MediaStrategyRecord
  payload: MediaStrategyPayload
}

const cases: Case[] = [
  {
    key: 'takealot-black-friday',
    title: 'Haiku 4.5 — Takealot Black Friday DOOH',
    expectedMode: 'OOH_ONLY',
    expectedGeographies: ['Johannesburg', 'Cape Town', 'Durban'],
    source: [
      'Client: Takealot',
      'Campaign: Black Friday 2026',
      'Business problem: Increase Black Friday awareness and drive online purchases during the promotional period.',
      'Objective: Reach online shoppers and convert demand to ecommerce purchases.',
      'Audience: Online shoppers aged 18-54, families and deal seekers.',
      'Geography: Johannesburg, Cape Town and Durban.',
      'Timing: 4 November 2026 to 28 November 2026.',
      'Budget: ZAR 320,000, with flexibility up to ZAR 400,000 if justified.',
      'Media: Digital out-of-home only.',
      'Priority locations: Mall of Africa, Sandton City, Gateway, Cavendish Square and Menlyn.',
      'Measurement: Reach, frequency, site delivery and online sales response where attributable.',
      'Constraint: Do not expand this into a full campaign; the requirement is digital OOH only.',
    ].join('\n'),
  },
  {
    key: 'mukuru-remittance-ooh',
    title: 'Haiku 4.5 — Mukuru remittance OOH',
    expectedMode: 'OOH_ONLY',
    expectedGeographies: ['South Africa'],
    source: [
      'Client: Mukuru',
      'Business problem: Reach customers who send money to Bangladesh, India and Pakistan.',
      'Objective: Identify effective out-of-home opportunities across South Africa that can support remittance awareness and consideration.',
      'Audience: Customers who send money to Bangladesh, India and Pakistan.',
      'Geography: South Africa, with focus on areas and communities with high concentrations of Indian, Pakistani and Bangladeshi residents and proximity to mosques and temples.',
      'Timing: October 2026 to December 2026.',
      'Budget: No confirmed budget. Recommend a range of investment levels without inventing a client budget.',
      'Media: Billboards, wall murals and other relevant OOH formats only.',
      'Constraint: Treat community concentration, country of origin and religious-site proximity as sensitive contextual planning evidence. Do not infer sensitive individual attributes.',
      'Deliverable: Recommendations should be suitable for client review and clearly identify evidence gaps.',
    ].join('\n'),
  },
  {
    key: 'rayetsa-furniture-growth',
    title: 'Haiku 4.5 — Rayetsa Furniture growth',
    expectedMode: 'FULL_CAMPAIGN',
    expectedGeographies: ['Pretoria', 'Midrand', 'Gauteng', 'Mpumalanga', 'Limpopo'],
    source: [
      'Client: Rayetsa Furniture',
      'Business problem: Grow store and product awareness and create measurable demand for furniture business-combo offers.',
      'Objective: Drive enquiries and store visits during the October to December 2026 trading period.',
      'Audience: Working households, value-seeking furniture buyers, entrepreneurs and small-business owners interested in business combos.',
      'Geography: Pretoria, Midrand, broader Gauteng, Mpumalanga and Limpopo.',
      'Timing: 1 October 2026 to 31 December 2026.',
      'Budget: ZAR 200,000 including VAT.',
      'Media: Use an integrated mix of OOH, radio and digital/social where justified by the audience and budget.',
      'Measurement: Enquiries, store visits, qualified leads and attributed sales where available.',
      'Constraint: Keep recommendations realistic for the stated budget and do not fabricate audience delivery figures.',
    ].join('\n'),
  },
  {
    key: 'health-vaccination-awareness',
    title: 'Haiku 4.5 — Vaccination awareness 2026',
    expectedMode: 'FULL_CAMPAIGN',
    expectedGeographies: ['South Africa'],
    source: [
      'Client: Department of Health',
      'Campaign: Vaccination Awareness 2026',
      'Business problem: Improve awareness and action around childhood and adolescent vaccination.',
      'Objective: Increase awareness of vaccination schedules and encourage eligible parents and caregivers to take children for vaccination.',
      'Audience: Parents and caregivers of children under 5, and parents/caregivers of girls aged 9-14.',
      'Geography: South Africa, with local adaptation by province where evidence supports it.',
      'Timing: 2026 campaign period; rotate polio, measles and HPV messages according to the approved campaign schedule.',
      'Budget: Treat all stated investment figures as inclusive of VAT. No agency commission is allowed.',
      'Media: Integrated radio, OOH, digital and community-relevant channels. Recommend channels only where the evidence supports the role.',
      'Measurement: Awareness, reach/frequency where measured, engagement and campaign response indicators.',
      'Constraint: Do not infer medical status or other sensitive individual health attributes.',
    ].join('\n'),
  },
  {
    key: 'church-decline-multi-audience',
    title: 'Haiku 4.5 — Church attendance decline',
    expectedMode: 'FULL_CAMPAIGN',
    expectedGeographies: ['South Africa'],
    source: [
      'Client: National church network',
      'Business problem: Attendance and participation are declining across multiple congregations.',
      'Objective: Rebuild awareness, relevance and participation while supporting local congregation growth.',
      'Audiences: Existing members who have disengaged; young adults 18-30; parents with school-age children; community members seeking support and belonging; and faith-curious adults.',
      'Geography: South Africa, with priority metros and township communities to be refined from approved evidence.',
      'Languages: English, isiZulu, isiXhosa, Sesotho, Setswana and Sepedi.',
      'Timing: 12-week campaign.',
      'Budget: ZAR 4,200,000 total; ZAR 3,400,000 is the operative media and activation budget. Preserve the distinction.',
      'Media: Integrated OOH, radio, digital/social, influencer/community voices and experiential activity where justified.',
      'Measurement: Reach, engagement, event participation, enquiries and congregation attendance trends.',
      'Constraint: Do not infer religion for individuals. Treat faith affiliation as campaign context, not an individual-level targeting fact.',
    ].join('\n'),
  },
]

test('five fresh Briefs use Haiku 4.5 through interpretation, audience and media strategy', async ({ page }) => {
  await signIn(page)
  const workspace = await currentWorkspace(page)
  const session = await currentSession(page)
  const results: unknown[] = []

  for (const scenario of cases) {
    const completed = await completeScenario(page, workspace.tenantId, session.antiforgeryToken, scenario)
    const { planning, approvedAudience, approvedStrategy, payload } = completed
    expect(planning.campaignMode?.mode, `${scenario.key}: campaign mode`).toBe(scenario.expectedMode)
    expect(approvedAudience.targetAudienceIds.length, `${scenario.key}: approved targets`).toBeGreaterThan(0)
    expect(payload.channelRecommendations.length, `${scenario.key}: media recommendations`).toBeGreaterThan(0)
    assertRecommendationsStayInMode(scenario, planning, payload)
    assertBudgetGuidance(payload)

    const brief = await getBrief(page, workspace.tenantId, planning.briefId)
    results.push({
      key: scenario.key,
      title: scenario.title,
      briefId: planning.briefId,
      briefVersionId: planning.briefVersionId,
      expectedMode: scenario.expectedMode,
      campaignMode: planning.campaignMode,
      brief,
      audience: {
        id: approvedAudience.id,
        targets: approvedAudience.targetAudienceIds,
        targetingRationale: approvedAudience.targetingRationale,
        positioningStatement: approvedAudience.positioningStatement,
        definitions: approvedAudience.definitions,
      },
      mediaStrategy: {
        id: approvedStrategy.id,
        summary: payload.summary,
        channelRecommendations: payload.channelRecommendations,
        strategicPrinciples: payload.strategicPrinciples,
        evidenceGaps: payload.evidenceGaps,
      },
    })
  }

  const outputDirectory = resolve(process.cwd(), '..', 'artifacts', 'production-readiness', 'preview')
  mkdirSync(outputDirectory, { recursive: true })
  const outputPath = resolve(outputDirectory, 'haiku45-five-briefs.json')
  writeFileSync(outputPath, JSON.stringify({
    schemaVersion: 'advertified.haiku45-five-briefs.v1',
    generatedAtUtc: new Date().toISOString(),
    modelExpected: 'global.anthropic.claude-haiku-4-5-20251001-v1:0',
    cases: results,
  }, null, 2) + '\n', 'utf8')
  console.log(`Haiku 4.5 five-Brief report: ${outputPath}`)
})

async function completeScenario(page: Page, tenantId: string, token: string, scenario: Case): Promise<CompletedCase> {
  const existing = await existingBriefVersion(page, tenantId, scenario.title)
  const briefVersionId = existing ?? (await createAndApproveBrief(page, scenario)).briefVersionId
  await page.goto('/home')
  let planning = await getPlanning(page, tenantId, briefVersionId)
  const audience = planning.audience ?? await generateAudience(page, tenantId, briefVersionId, token)
  expect(audience.definitions.length, `${scenario.key}: audience definitions`).toBeGreaterThan(0)
  const approvedAudience = audience.status === 'APPROVED'
    ? audience
    : await approveAudience(page, tenantId, audience, token)
  planning = await getPlanning(page, tenantId, briefVersionId)
  const currentStrategy = await getMediaStrategy(page, tenantId, briefVersionId)
  const strategy = currentStrategy ?? await analyseMediaStrategy(page, tenantId, briefVersionId, token)
  const payload = JSON.parse(strategy.artifactJson) as MediaStrategyPayload
  assertRecommendationsStayInMode(scenario, planning, payload)
  assertBudgetGuidance(payload)
  const approvedStrategy = strategy.status === 'APPROVED'
    ? strategy
    : await approveMediaStrategy(page, tenantId, briefVersionId, strategy, token)
  return { planning, approvedAudience, approvedStrategy, payload }
}

async function existingBriefVersion(page: Page, tenantId: string, title: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/briefs`)
  expect(response.ok(), await response.text()).toBe(true)
  const briefs = await response.json() as BriefSummary[]
  return briefs.find(item => item.title === title && item.approvedVersionId)?.approvedVersionId ?? null
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await expect(page.getByRole('heading', { name: 'The calm centre of campaign delivery.' })).toBeVisible()
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}

async function currentWorkspace(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const workspaces = await response.json() as Workspace[]
  const workspace = workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]
  expect(workspace).toBeTruthy()
  return workspace!
}

async function currentSession(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Session
}

async function createAndApproveBrief(page: Page, scenario: Case) {
  await page.goto('/briefs/new')
  await page.getByLabel('Campaign or Brief name').fill(scenario.title)
  await page.getByLabel('Client requirement').fill(scenario.source)
  const interpretationResponse = page.waitForResponse(response =>
    response.url().includes('/briefs:understand') && response.request().method() === 'POST',
    { timeout: 120_000 })
  await page.getByRole('button', { name: /Next: AI Interpretation/ }).click()
  const understood = await interpretationResponse
  expect(understood.status(), `${scenario.key}: ${await understood.text()}`).toBe(200)
  await expect(page.getByRole('heading', { name: 'AI brief interpretation' })).toBeVisible({ timeout: 30_000 })
  const approveResponse = page.waitForResponse(response =>
    /\/brief-versions\/[0-9a-f-]+:approve$/.test(new URL(response.url()).pathname) &&
    response.request().method() === 'POST', { timeout: 60_000 })
  await page.getByRole('button', { name: /Approve and continue/ }).click()
  const approved = await approveResponse
  expect(approved.status(), `${scenario.key}: ${await approved.text()}`).toBe(200)
  await expect(page).toHaveURL(/\/stp\/[0-9a-f-]{36}$/, { timeout: 30_000 })
  const match = page.url().match(/\/stp\/([0-9a-f-]{36})$/i)
  expect(match, `${scenario.key}: Brief version route`).toBeTruthy()
  return { briefVersionId: match![1] }
}

async function getPlanning(page: Page, tenantId: string, briefVersionId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as PlanningWorkspace
}

async function getBrief(page: Page, tenantId: string, briefId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/briefs/${briefId}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json()
}

async function generateAudience(page: Page, tenantId: string, briefVersionId: string, token: string) {
  const response = await page.request.post(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/audiences:generate`,
    { data: {}, headers: commandHeaders(token) },
  )
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as Audience
}

async function approveAudience(page: Page, tenantId: string, audience: Audience, token: string) {
  const response = await page.request.post(
    `/api/v1/tenants/${tenantId}/audience-strategies/${audience.id}:approve`,
    {
      data: {
        targetAudienceIds: audience.targetAudienceIds,
        targetingRationale: audience.targetingRationale ?? 'Approve the provider-proposed target audience with retained evidence boundaries.',
        positioningStatement: audience.positioningStatement ?? 'Use only the approved audience and evidence in downstream planning.',
        reason: 'Haiku 4.5 production-behaviour certification.',
      },
      headers: { ...commandHeaders(token), 'If-Match': `"${audience.version}"` },
    },
  )
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as Audience
}

async function getMediaStrategy(page: Page, tenantId: string, briefVersionId: string) {
  const response = await page.request.get(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/intelligence/media-strategy`,
  )
  if (response.status() === 404) return null
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as MediaStrategyRecord
}

async function analyseMediaStrategy(page: Page, tenantId: string, briefVersionId: string, token: string) {
  const response = await page.request.post(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/intelligence/media-strategy`,
    { data: {}, headers: commandHeaders(token) },
  )
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as MediaStrategyRecord
}

async function approveMediaStrategy(
  page: Page,
  tenantId: string,
  briefVersionId: string,
  strategy: MediaStrategyRecord,
  token: string,
) {
  const response = await page.request.post(
    `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/intelligence/media-strategy/${strategy.id}/approve`,
    { data: { expectedVersion: strategy.version }, headers: commandHeaders(token) },
  )
  expect(response.status(), await response.text()).toBe(200)
  return await response.json() as MediaStrategyRecord
}

function commandHeaders(token: string) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}

function assertRecommendationsStayInMode(scenario: Case, planning: PlanningWorkspace, payload: MediaStrategyPayload) {
  const allowed = new Set(planning.campaignMode?.allowedChannels ?? [])
  for (const recommendation of payload.channelRecommendations) {
    expect(allowed.has(recommendation.channel),
      `${scenario.key}: ${recommendation.channel} must remain inside ${[...allowed].join(', ')}`).toBe(true)
  }
}

function assertBudgetGuidance(payload: MediaStrategyPayload) {
  const values = payload.channelRecommendations.map(item => item.budgetGuidancePercent)
  if (values.some(value => value === null)) return
  const total = values.reduce((sum, value) => sum + (value ?? 0), 0)
  expect(total).toBeCloseTo(100, 4)
}
