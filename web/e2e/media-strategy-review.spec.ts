import { expect, test, type Route } from '@playwright/test'
import { planningWorkspaceSchema } from '../src/api/planning-schemas'
import { mediaStrategyPayloadSchema } from '../src/api/media-strategy-client'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const userId = 'e2000000-0000-0000-0000-000000000001'
const briefId = 'e3000000-0000-0000-0000-000000000001'
const briefVersionId = 'e4000000-0000-0000-0000-000000000001'
const audienceId = 'e5000000-0000-0000-0000-000000000001'
const draftId = 'e6000000-0000-0000-0000-000000000001'
const approvedId = 'e6000000-0000-0000-0000-000000000002'
const now = '2026-09-13T00:00:00Z'
type Scenario = 'complete' | 'empty' | 'budgetless'
type State = {
  scenario: Scenario; strategy: 'DRAFT' | 'APPROVED' | null; allocated: boolean; mutations: string[]
  planReview?: boolean; planApproved?: boolean; reviewReason?: string
  flightPeriods?: { start: string; end: string }[]; multipleFlights?: boolean
  missingGeography?: boolean
  geographyAllocations?: { geography: string; budgetMinor: number }[]
  impactEstimate?: {
    estimatedReach: number | null; averageFrequency: number | null; estimatedRoiPercent: number | null
    source: string; measurementPeriod: string | null; methodology: string
  } | null
}

for (const scenario of ['complete', 'empty', 'budgetless'] as const) {
  test(`strategy review respects the ${scenario} recommendation boundary`, async ({ page }) => {
    const state: State = { scenario, strategy: null, allocated: false, mutations: [] }
    planningWorkspaceSchema.parse(planning(state))
    mediaStrategyPayloadSchema.parse(payload(scenario))
    await page.addInitScript(id => sessionStorage.setItem('advertified.workspace',
      JSON.stringify({ tenantId: id })), tenantId)
    await page.route('**/api/v1/**', route => handleApi(route, state))
    await page.goto(`/planning/${briefVersionId}#strategy`)
    await expect(page.getByRole('heading', { name: 'Strategy recommendations', exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Generate strategy recommendations', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Review channel recommendations' })).toBeVisible()
    expect(state.allocated).toBe(false)
    expect(state.mutations).toEqual(['analyse'])
    if (scenario === 'empty') {
      await expect(page.getByText('No channel recommendation is available yet.', { exact: false }).first()).toBeVisible()
      await expect(page.getByRole('button', { name: 'Approve channel recommendations' })).toHaveCount(0)
      await expect(page.getByRole('button', { name: 'Build media allocation' })).toHaveCount(0)
      return
    }
    await page.getByRole('button', { name: 'Approve channel recommendations', exact: true }).click()
    expect(state.mutations).toEqual(['analyse', 'approve'])
    if (scenario === 'budgetless') {
      await expect(page.getByRole('link', { name: 'Review budget before allocation' })).toBeVisible()
      await expect(page.getByRole('button', { name: 'Build media allocation' })).toHaveCount(0)
      await page.getByRole('button', { name: 'Create revised strategy recommendations', exact: true }).click()
      await expect(page.getByRole('button', { name: 'Approve channel recommendations', exact: true })).toBeVisible()
      expect(state.mutations).toEqual(['analyse', 'approve', 'analyse'])
      expect(state.allocated).toBe(false)
      return
    }
    await page.getByRole('button', { name: 'Build media allocation', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Recommended media mix', exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Campaign flighting timeline', exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Geographic allocation', exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Why this strategy?', exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Expected impact', exact: true })).toBeVisible()
    await expect(page.getByText('Set estimate', { exact: true })).toHaveCount(2)
    await expect(page.getByText('Optional', { exact: true })).toHaveCount(1)
    await expect(page.getByRole('button', { name: 'Add planning estimate', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Percentage', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Rand value', exact: true })).toBeVisible()
    expect(state.mutations).toEqual(['analyse', 'approve', 'allocate'])
  })
}

test('strategy persists geography budgets and a sourced pre-buy impact estimate', async ({ page }) => {
  const state: State = { scenario: 'complete', strategy: 'APPROVED', allocated: true,
    mutations: [], missingGeography: true, impactEstimate: null }
  await page.addInitScript(id => sessionStorage.setItem('advertified.workspace',
    JSON.stringify({ tenantId: id })), tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))
  await page.goto(`/planning/${briefVersionId}#strategy`)
  const approve = page.getByRole('button', { name: 'Approve strategy & continue', exact: true })
  await expect(approve).toBeDisabled()
  await page.getByRole('button', { name: 'Edit geography allocation for Outdoor advertising' }).click()
  const geography = page.getByRole('dialog', { name: 'Geography allocation for Outdoor advertising' })
  await geography.getByRole('button', { name: 'Split evenly', exact: true }).click()
  await geography.getByRole('button', { name: 'Save geography allocation', exact: true }).click()
  await expect(geography).toHaveCount(0)
  await expect(approve).toBeEnabled()
  await page.getByRole('button', { name: 'Add planning estimate', exact: true }).click()
  const impact = page.getByRole('region', { name: 'Planning impact estimate' })
  await impact.getByLabel('Estimated reach').fill('2400000')
  await impact.getByLabel('Average frequency').fill('3.2')
  await impact.getByLabel('Source').fill('Planner benchmark model')
  await impact.getByLabel('Methodology').fill('Pre-buy planning benchmark; not verified delivery.')
  await impact.getByRole('button', { name: 'Save planning estimate', exact: true }).click()
  await expect(page.getByText('2.4M', { exact: true })).toBeVisible()
  await expect(page.getByText('3.2x', { exact: true })).toBeVisible()
  expect(state.mutations).toEqual(['save-geography', 'save-impact'])
})

test('the media plan needs an explicit review reason and approval before Proposal', async ({ page }) => {
  const state: State = { scenario: 'complete', strategy: 'APPROVED', allocated: true,
    mutations: [], planReview: true, planApproved: false }
  planningWorkspaceSchema.parse(planning(state))
  await page.addInitScript(id => sessionStorage.setItem('advertified.workspace',
    JSON.stringify({ tenantId: id })), tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))
  await page.goto(`/planning/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Reconciled plan', exact: true })).toBeVisible()
  await expect(page.getByRole('link', { name: /Next: Proposal/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Approve media plan', exact: true })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Review and accept', exact: true })).toBeDisabled()
  const reason = 'I reviewed the dated supplier evidence; no delivery forecast is being promised.'
  await page.getByRole('textbox', { name: 'Review reason for supply unconfirmed' }).fill(reason)
  await page.getByRole('button', { name: 'Review and accept', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Approve media plan', exact: true })).toBeEnabled()
  expect(state.reviewReason).toBe(reason)
  await expect(page.getByRole('link', { name: /Next: Proposal/ })).toHaveCount(0)
  await page.getByRole('button', { name: 'Approve media plan', exact: true }).click()
  await expect(page.getByRole('link', { name: /Next: Proposal/ }))
    .toHaveAttribute('href', `/briefs/${briefId}/proposals/new`)
  expect(state.mutations).toEqual(['resolve-plan', 'approve-plan'])
})

test('strategy requires persisted flight dates before approval', async ({ page }) => {
  const state: State = { scenario: 'complete', strategy: 'APPROVED', allocated: true,
    mutations: [], flightPeriods: [], multipleFlights: true }
  await page.addInitScript(id => sessionStorage.setItem('advertified.workspace',
    JSON.stringify({ tenantId: id })), tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))
  await page.goto(`/planning/${briefVersionId}#strategy`)
  const approve = page.getByRole('button', { name: 'Approve strategy & continue', exact: true })
  await expect(approve).toBeDisabled()
  await page.getByRole('button', { name: 'Edit flight dates for Outdoor advertising', exact: true }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: '+ Add flight period', exact: true }).click()
  await dialog.getByLabel('Start date', { exact: true }).fill('2026-11-04')
  await dialog.getByLabel('End date', { exact: true }).fill('2026-11-03')
  await expect(dialog.getByRole('button', { name: /Apply new flight/ })).toBeDisabled()
  await dialog.getByLabel('End date', { exact: true }).fill('2026-11-28')
  await expect(dialog.getByRole('button', { name: /Apply new flight/ })).toBeEnabled()
  await dialog.getByRole('radio', { name: /Apply to this channel and all channels without flight dates/ }).check()
  await dialog.getByRole('button', { name: /Apply new flight/ }).click()
  await expect(dialog).toHaveCount(0)
  expect(state.flightPeriods).toEqual([{ start: '2026-11-04', end: '2026-11-28' }])
  await expect(approve).toBeEnabled()
  expect(state.mutations).toEqual(['save-flights'])
})

async function handleApi(route: Route, state: State) {
  const path = new URL(route.request().url()).pathname.replace(/\/$/, '')
  if (route.request().method() === 'GET') return readApi(route, state, path)
  expect(route.request().headers()['x-csrf-token']).toBe('csrf-strategy')
  if (path.includes('/media-plan-versions/')) return handlePlanDecision(route, state, path)
  if (path.includes('/media-mix-versions/') && path.endsWith(':update')) {
    return handleMixUpdate(route, state)
  }
  if (path.endsWith('/intelligence/media-strategy')) {
    state.strategy = 'DRAFT'; state.mutations.push('analyse')
    return json(route, record(state))
  }
  if (path.endsWith(`/media-strategy/${draftId}/approve`)) {
    expect(route.request().postDataJSON()).toEqual({ expectedVersion: 1 })
    expect(state.strategy).toBe('DRAFT')
    state.strategy = 'APPROVED'; state.mutations.push('approve')
    return json(route, record(state))
  }
  if (path.endsWith('/media-mixes:generate')) {
    expect(state.strategy).toBe('APPROVED')
    expect(state.scenario).toBe('complete')
    state.allocated = true; state.mutations.push('allocate')
    return json(route, mix())
  }
  throw new Error(`Unexpected strategy mutation: ${path}`)
}

async function handleMixUpdate(route: Route, state: State) {
  expect(route.request().headers()['if-match']).toBe('"1"')
  const body = route.request().postDataJSON()
  const allocations = body.allocations
  state.flightPeriods = allocations[0].runningPeriods
  if (state.multipleFlights) {
    expect(allocations).toHaveLength(2)
    expect(allocations[1].runningPeriods).toEqual(state.flightPeriods)
  }
  const nextGeography = allocations[0].geographyAllocations ?? []
  if (state.missingGeography && nextGeography.length) {
    state.missingGeography = false
    state.geographyAllocations = nextGeography
    state.mutations.push('save-geography')
  } else if (JSON.stringify(body.impactEstimate ?? null) !== JSON.stringify(state.impactEstimate ?? null)) {
    state.impactEstimate = body.impactEstimate ?? null
    state.mutations.push('save-impact')
  } else {
    state.mutations.push('save-flights')
  }
  return json(route, mix(state))
}

async function readApi(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, {
    authenticated: true, antiforgeryToken: 'csrf-strategy', expiresAtUtc: '2099-01-01T00:00:00Z',
    signInPath: null, signOutPath: null,
  })
  if (path === '/api/v1/workspaces') return json(route, [{ membershipId: userId, tenantId,
    name: 'Strategy test workspace', slug: 'strategy-test', roleCode: 'agency_admin', version: 1 }])
  if (path === '/api/v1/me') return json(route, { id: userId, email: 'strategy@example.test',
    displayName: 'Strategy reviewer', phone: null, mfaEnabled: true, version: 1 })
  if (path.endsWith('/human-tasks')) return json(route, [])
  if (path.endsWith(`/brief-versions/${briefVersionId}/planning`)) return json(route, planning(state))
  if (path.endsWith('/intelligence/media-strategy')) return state.strategy
    ? json(route, record(state)) : json(route, null, 404)
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function planning(state: State) {
  return {
    briefId, briefVersionId, clientName: 'Synthetic strategy client',
    campaignMode: { id: briefId, briefVersionId, mode: 'OOH_ONLY', allowedChannels: ['OOH', 'DOOH'],
      isLocked: true, decisionSource: 'HUMAN_CLARIFICATION', confidence: 1,
      reason: 'Supplied OOH requirement.', selectedBy: userId, selectedAtUtc: now },
    audience: { id: audienceId, briefVersionId, versionNumber: 2, targetAudienceIds: [audienceId],
      targetingRationale: 'Review the supplied audience.', positioningStatement: null,
      inputHash: 'a'.repeat(64), status: 'APPROVED', createdBy: userId, approvedBy: userId,
      version: 2, approvedAtUtc: now, createdAtUtc: now,
      definitions: [{ id: audienceId, name: 'Supplied shoppers', description: 'Audience supplied by the client.',
        needState: null, buyingContext: null, geographies: ['Johannesburg'], language: null, lifeStage: null,
        lsmSem: null, lsmSemTaxonomy: null, lsmSemTaxonomyVersion: null, classification: 'CLIENT_REQUIREMENT',
        exclusions: [], evidenceItemIds: [], referenceObservationIds: [], confidence: null,
        status: 'APPROVED', lsmSemMandatory: false }] },
    mediaMix: state.allocated ? mix(state) : null, shortlist: null, mediaPlan: state.planReview ? plan(state) : null,
  }
}

function payload(scenario: Scenario) {
  return { summary: 'Synthetic upstream strategy recommendation for this UI regression.',
    channelRecommendations: scenario === 'empty' ? [] : [{ channel: 'OOH', role: 'Serve the supplied awareness objective.',
      rationale: 'Recommendation only; no claimed measured delivery.', objectiveContribution: 'Support the supplied objective.',
      geographyRole: null, classification: 'AI_RECOMMENDATION', budgetGuidancePercent: scenario === 'budgetless' ? null : 100,
      tradeOffs: ['Delivery evidence still needs verification.'], evidenceGaps: [] }],
    strategicPrinciples: ['Do not infer measured performance.'], excludedChannels: [],
    evidenceGaps: scenario === 'empty' ? ['No channel recommendation is available.'] : [],
  }
}

function record(state: State) {
  return { id: state.strategy === 'APPROVED' ? approvedId : draftId, subjectId: briefVersionId,
    subjectVersion: 3, serviceCode: 'media_strategy', artifactJson: JSON.stringify(payload(state.scenario)),
    status: state.strategy, version: state.strategy === 'APPROVED' ? 2 : 1, unknowns: [], assumptions: [] }
}

function mix(state?: State) {
  const allocations = mixChannels(state).map(channel => mixAllocation(channel, state))
  return { id: 'e7000000-0000-0000-0000-000000000001', briefVersionId, audienceArtifactId: audienceId,
    mediaStrategyArtifactId: approvedId, versionNumber: 1, totalBudgetMinor: 32_000_000, currency: 'ZAR',
    allocations, assumptions: [], inputHash: 'b'.repeat(64), status: 'DRAFT', createdBy: userId, approvedBy: null,
    version: 1, createdAtUtc: now, impactEstimate: state?.impactEstimate ?? null }
}

function mixChannels(state?: State) {
  return state?.multipleFlights ? ['OOH', 'DOOH'] : ['OOH']
}

function mixAllocation(channel: string, state?: State) {
  const budgetMinor = state?.multipleFlights ? 16_000_000 : 32_000_000
  return {
    channel,
    budgetMinor,
    role: 'Serve the supplied awareness objective.',
    runningPeriods: state?.flightPeriods ?? [{ start: '2026-11-04', end: '2026-11-28' }],
    geographyAllocations: state?.missingGeography
      ? []
      : state?.geographyAllocations ?? [{ geography: 'Johannesburg', budgetMinor }],
  }
}

function plan(state: State) {
  return {
    id: 'e8000000-0000-0000-0000-000000000001', briefVersionId, mixVersionId: mix().id,
    shortlistVersionId: 'e9000000-0000-0000-0000-000000000001', versionNumber: 1,
    feesMinor: 0, vatMinor: 0, totalMinor: 32_000_000, currency: 'ZAR',
    supplyConfidence: 'UNCONFIRMED', inputHash: 'c'.repeat(64),
    status: state.planApproved ? 'APPROVED' : 'IN_REVIEW', assumptions: [],
    lines: [{ id: draftId, inventoryTenantId: tenantId, marketplaceListingVersionId: null,
      inventoryProductId: briefId, productVersionId: briefVersionId, rateId: audienceId, availabilityId: null,
      name: 'Synthetic OOH review placement', channel: 'OOH', geography: 'Johannesburg',
      runningPeriods: [{ start: '2026-11-04', end: '2026-11-28' }], quantity: 1,
      clientPriceMinor: 32_000_000, feesMinor: 0, vatMinor: 0, availability: 'AVAILABLE',
      rateFreshness: 'CURRENT', supplySource: 'PUBLISHED_INVENTORY', lastConfirmedAtUtc: null,
      supplyConfidence: 'UNCONFIRMED' }],
    objections: [{ code: 'SUPPLY_UNCONFIRMED', severity: 'MATERIAL', affectedField: 'supply',
      evidenceGap: 'Supplier confirmation needs review.', recommendedResolution: 'Review the retained evidence.',
      resolution: state.reviewReason ? 'ACCEPTED_WITH_REASON' : null,
      resolutionReason: state.reviewReason ?? null, resolvedBy: state.reviewReason ? userId : null }],
    createdBy: userId, approvedBy: state.planApproved ? userId : null,
    version: state.planApproved ? 3 : state.reviewReason ? 2 : 1, createdAtUtc: now,
  }
}

async function handlePlanDecision(route: Route, state: State, path: string) {
  expect(route.request().headers()['idempotency-key']).toBeTruthy()
  if (path.endsWith('/objections/SUPPLY_UNCONFIRMED:resolve')) {
    expect(route.request().headers()['if-match']).toBe('"1"')
    const body = route.request().postDataJSON()
    expect(body.resolution).toBe('ACCEPTED_WITH_REASON')
    state.reviewReason = body.reason
    state.mutations.push('resolve-plan')
  } else {
    expect(path).toMatch(/:approve$/)
    expect(route.request().headers()['if-match']).toBe('"2"')
    expect(state.reviewReason).toBeTruthy()
    state.planApproved = true
    state.mutations.push('approve-plan')
  }
  return json(route, plan(state))
}

async function json(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}
