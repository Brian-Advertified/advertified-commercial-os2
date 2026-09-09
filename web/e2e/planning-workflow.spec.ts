import { expect, test, type Route } from '@playwright/test'
import { planningWorkspaceSchema } from '../src/api/planning-schemas'
import { buyAssessmentFixture, combinationFixture } from './buy-assessment-fixture'
import { decisionReportFixture } from './inventory-decision-fixture'

const tenantId = 'c1000000-0000-0000-0000-000000000001'
const userId = 'c2000000-0000-0000-0000-000000000001'
const briefId = 'c3000000-0000-0000-0000-000000000000'
const briefVersionId = 'c3000000-0000-0000-0000-000000000001'
const audienceId = 'c4000000-0000-0000-0000-000000000001'
const mixId = 'c5000000-0000-0000-0000-000000000001'
const shortlistId = 'c6000000-0000-0000-0000-000000000001'
const candidateId = 'c7000000-0000-0000-0000-000000000001'
const productId = 'c8000000-0000-0000-0000-000000000001'
const productVersionId = 'c9000000-0000-0000-0000-000000000001'
const rateId = 'ca000000-0000-0000-0000-000000000001'
const availabilityId = 'cb000000-0000-0000-0000-000000000001'
const planId = 'cc000000-0000-0000-0000-000000000001'
const lineId = 'cd000000-0000-0000-0000-000000000001'
const benchmarkId = 'ce000000-0000-0000-0000-000000000001'
const now = '2026-08-29T19:00:00Z'

type Allocation = {
  channel: string
  budgetMinor: number
  role: string
  runningPeriods: { start: string; end: string }[]
  purchases?: { inventoryTenantId: string; inventoryProductId: string; productVersionId: string;
    rateId: string; rateType: string; quantity: number }[]
}

type State = {
  buyingRateType?: string
  fullCampaign: boolean
  audience: boolean
  audienceApproved: boolean
  mix: null | {
    status: 'DRAFT' | 'APPROVED'
    version: number
    allocations: Allocation[]
  }
  shortlist: null | { status: 'DRAFT' | 'APPROVED'; version: number; selected: boolean }
  plan: null | { status: 'IN_REVIEW' | 'APPROVED'; version: number; resolved: boolean }
}

test('planner edits allocation and timing before approving the plan', async ({ page }) => {
  const state: State = {
    fullCampaign: false, audience: true, audienceApproved: true,
    mix: null, shortlist: null, plan: null,
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto(`/planning/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Media Planning Overview' }))
    .toBeVisible({ timeout: 15_000 })

  await page.getByRole('button', { name: 'Create media mix' }).click()
  await expect(page.getByRole('heading', { name: 'Shape the investment and timing' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Outdoor advertising', exact: true })).toBeVisible()
  await page.getByRole('button', { name: '+ Add period' }).click()
  await page.getByLabel('Start').fill('2026-09-01')
  await page.getByLabel('End').fill('2026-09-30')
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.locator('.timeline-segment[title*="2026-09-01 to 2026-09-30"]')).toBeVisible()
  await page.getByRole('button', { name: 'Confirm media mix' }).click()

  await page.getByRole('button', { name: 'Build inventory shortlist' }).click()
  await page.getByText('Buying evidence and gaps', { exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Digital screen exposure' })).toBeVisible()
  await expect(page.getByText('Supplied audience measurement — not a campaign forecast')).toBeVisible()
  await expect(page.getByRole('region', { name: 'Planner rationale and buying questions' })).toBeVisible()
  await expect(page.getByText('Required places matched: 1 / 1', { exact: true })).toBeVisible()
  await expect(page.getByText(/The creative is longer than the supplied slot/)).toBeVisible()
  await expect(page.getByText(/Proximity alone does not establish audience/)).toBeVisible()
  await page.getByText('Compare coverage combinations', { exact: true }).click()
  await expect(page.getByText('Difference from combination 1', { exact: true })).toBeVisible()
  await expect(page.getByText('Same supplier cost', { exact: true })).toBeVisible()
  await expect(page.getByText('Placements with measured target baselines', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Review this combination' }).click()
  await expect(page.getByLabel('Select Johannesburg OOH Site')).toBeChecked()
  await expect(page.getByText('Cost per relevant audience reached: Needs evidence.', { exact: true })).toBeVisible()
  await expect(page.getByText('Additional campaign reach: Needs evidence.', { exact: true })).toBeVisible()
  await expect(page.getByText('Profile matches are not people reached.', { exact: false })).toBeVisible()
  await page.getByText('Market comparison').click()
  await expect(page.getByText('4 comparable sites')).toBeVisible()
  await page.getByLabel('Select Johannesburg OOH Site').check()
  await expect(page.getByRole('button', { name: 'Confirm selected inventory' })).toBeDisabled()
  await page.getByLabel('Why are you carrying these placements forward?').fill('Retain the local anchor; audience reach still needs research.')
  await page.getByRole('button', { name: 'Confirm selected inventory' }).click()

  await page.getByRole('button', { name: 'Create media plan' }).click()
  await expect(page.locator('.plan-line-periods span').first()).toContainText('2026')
  await page.getByRole('button', { name: 'Review and accept' }).click()
  await page.getByRole('button', { name: 'Approve media plan' }).click()
  await expect(page.getByText('Media plan approved and ready for proposal preparation.')).toBeVisible()
  await page.getByRole('button', { name: 'Open decision history' }).click()
  await expect(page.getByText('Removed from this selection', { exact: true })).toBeVisible()
  await expect(page.getByText('Changed the anchor to improve verified local coverage.')).toBeVisible()
  await expect(page.getByText('Agent interpretation is not evidence that AI selected the placement.', { exact: false })).toBeVisible()
})

test('buying quantity is bound to placement and saved before mix confirmation', async ({ page }) => {
  const state: State = {
    fullCampaign: false, audience: true, audienceApproved: true, buyingRateType: 'CPM',
    mix: { status: 'DRAFT', version: 1, allocations: [{ channel: 'OOH', budgetMinor: 1_000_000,
      role: 'Synthetic placement', runningPeriods: [{ start: '2026-09-01', end: '2026-09-30' }] }] },
    shortlist: { status: 'DRAFT', version: 1, selected: false }, plan: null,
  }
  planningWorkspaceSchema.parse(planning(state))
  await page.addInitScript(id => sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id })), tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))
  await page.goto(`/planning/${briefVersionId}`)
  await page.getByRole('combobox', { name: 'Placement', exact: true }).selectOption(candidateId)
  await page.getByLabel('Committed quantity').fill('100000')
  await page.getByRole('button', { name: 'Add buying quantity' }).click()
  await expect(page.getByRole('button', { name: 'Confirm media mix' })).toBeDisabled()
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByRole('button', { name: 'Confirm media mix' })).toBeEnabled()
  expect(state.mix!.allocations[0].purchases).toEqual([{ inventoryTenantId: tenantId,
    inventoryProductId: productId, productVersionId, rateId, rateType: 'CPM', quantity: 100000 }])
  await page.getByRole('button', { name: 'Confirm media mix' }).click()
  await expect(page.getByText('Media mix confirmed.', { exact: true })).toBeVisible()
})

test('full campaign planner can add, rebalance and remove permitted channels', async ({ page }) => {
  const state: State = {
    fullCampaign: true, audience: true, audienceApproved: true,
    mix: null, shortlist: null, plan: null,
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto(`/planning/${briefVersionId}`)
  await page.getByRole('button', { name: 'Create media mix' }).click()
  await page.getByRole('button', { name: '+ Add period' }).click()
  await page.getByLabel('Start').fill('2026-10-01')
  await page.getByLabel('End').fill('2026-10-31')

  await page.getByLabel('Add media type').selectOption('RADIO')
  await page.getByRole('button', { name: 'Add media type' }).click()
  const ooh = page.locator('.media-allocation-card')
    .filter({ has: page.getByRole('heading', { name: 'Outdoor advertising' }) })
  const radio = page.locator('.media-allocation-card')
    .filter({ has: page.getByRole('heading', { name: 'Radio' }) })
  await ooh.getByRole('spinbutton').fill('7000')
  await radio.getByRole('spinbutton').fill('3000')
  await expect(page.getByText('Budget balanced')).toBeVisible()
  await page.getByRole('button', { name: 'Save changes' }).click()
  expect(state.mix?.allocations.map(item => item.channel)).toEqual(['OOH', 'RADIO'])

  await page.getByRole('button', { name: 'Remove Radio from media mix' }).click()
  await ooh.getByRole('spinbutton').fill('10000')
  await page.getByRole('button', { name: 'Save changes' }).click()
  expect(state.mix?.allocations.map(item => item.channel)).toEqual(['OOH'])
})

test('audience strategy is reviewed and approved with one action', async ({ page }) => {
  const state: State = {
    fullCampaign: false, audience: true, audienceApproved: false,
    mix: null, shortlist: null, plan: null,
  }
  await page.addInitScript(id => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))

  await page.goto(`/stp/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Audience Strategy' })).toBeVisible()
  await expect(page.getByText('Human review required')).toBeVisible()
  await expect(page.getByRole('combobox', { name: 'Planning role' })).toHaveCount(2)
  await page.getByRole('combobox', { name: 'Planning role' }).nth(1).selectOption('secondary')
  await page.getByRole('button', { name: 'Approve audience strategy & continue' }).click()

  await expect(page).toHaveURL(`/planning/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Media Planning Overview' })).toBeVisible()
  await expect(page.getByRole('combobox', { name: 'Planning role' })).toHaveCount(0)
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (path.endsWith('/reporting/inventory-decisions')) return json(route,
    decisionReportFixture(productId, productVersionId, briefVersionId, shortlistId))
  if (request.method() === 'GET') return read(route, state, path)
  assertMutation(route, isVersioned(path))
  if (path.includes('audience') || path.includes('media-mix')) return handleMixCommand(route, state, path)
  if (path.includes('shortlist')) return handleShortlistCommand(route, state, path)
  if (path.includes('media-plan')) return handlePlanCommand(route, state, path)
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleMixCommand(route: Route, state: State, path: string) {
  if (path.endsWith('/audiences:generate')) {
    state.audience = true
    state.audienceApproved = false
    return json(route, audience(state))
  }
  if (path.endsWith(`${audienceId}:approve`)) {
    state.audienceApproved = true
    return json(route, audience(state))
  }
  if (path.endsWith('/media-mixes:generate')) {
    state.mix = {
      status: 'DRAFT',
      version: 1,
      allocations: [{
        channel: 'OOH', budgetMinor: 1_000_000,
        role: 'Primary local visibility', runningPeriods: [],
      }],
    }
    state.shortlist = null
    state.plan = null
    return json(route, mix(state))
  }
  if (path.endsWith(`${mixId}:update`)) {
    const body = route.request().postDataJSON() as { allocations: Allocation[] }
    state.mix = {
      status: 'DRAFT',
      version: state.mix!.version + 1,
      allocations: body.allocations,
    }
    return json(route, mix(state))
  }
  state.mix = { ...state.mix!, status: 'APPROVED', version: state.mix!.version + 1 }
  return json(route, mix(state))
}

async function handleShortlistCommand(route: Route, state: State, path: string) {
  if (path.endsWith('/shortlists:generate')) {
    state.shortlist = { status: 'DRAFT', version: 1, selected: false }
  } else {
    state.shortlist = { status: 'APPROVED', version: 2, selected: true }
  }
  return json(route, shortlist(state))
}

async function handlePlanCommand(route: Route, state: State, path: string) {
  if (path.endsWith('/media-plans:generate')) {
    state.plan = { status: 'IN_REVIEW', version: 1, resolved: false }
  } else if (path.includes('/objections/')) {
    state.plan = { status: 'IN_REVIEW', version: 2, resolved: true }
  } else {
    state.plan = { status: 'APPROVED', version: 3, resolved: true }
  }
  return json(route, plan(state.plan))
}

function isVersioned(path: string) {
  return path.includes(':update') || path.includes(':approve') ||
    path.includes(':select') || path.includes(':resolve')
}

async function read(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, session())
  if (path === '/api/v1/workspaces') return json(route, [workspace()])
  if (path === '/api/v1/me') return json(route, user())
  if (path.endsWith(`/brief-versions/${briefVersionId}/planning`)) return json(route, planning(state))
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function planning(state: State) {
  return {
    briefId,
    briefVersionId,
    clientName: 'Planning Client',
    campaignMode: campaignMode(state),
    audience: state.audience ? audience(state) : null,
    mediaMix: state.mix ? mix(state) : null,
    shortlist: state.shortlist ? shortlist(state) : null,
    mediaPlan: state.plan ? plan(state.plan) : null,
  }
}

function campaignMode(state: State) {
  return {
    id: 'c3500000-0000-0000-0000-000000000001', briefVersionId,
    mode: state.fullCampaign ? 'FULL_CAMPAIGN' : 'OOH_ONLY',
    allowedChannels: state.fullCampaign
      ? ['OOH', 'DOOH', 'RADIO', 'TV', 'PRINT', 'DIGITAL', 'SOCIAL']
      : ['OOH', 'DOOH'],
    isLocked: true,
    decisionSource: 'AGENT', confidence: 0.95,
    reason: 'The supplied Brief requests only OOH.', selectedBy: userId, selectedAtUtc: now,
  }
}

function audience(state: State) {
  const status = state.audienceApproved ? 'APPROVED' : 'DRAFT'
  return { id: audienceId, briefVersionId, versionNumber: 1,
    targetAudienceIds: ['cf000000-0000-0000-0000-000000000001'],
    targetingRationale: 'Prioritise local business decision makers in Johannesburg.',
    positioningStatement: 'Present the advertiser as the practical local growth partner.',
    inputHash: 'a'.repeat(64), status,
    definitions: [
      { id: 'cf000000-0000-0000-0000-000000000001', name: 'Local business decision makers',
        description: 'Businesses actively seeking local customer demand.', needState: 'Growth',
        buyingContext: 'Evaluating a local purchase', geographies: ['Johannesburg'],
        language: null, lifeStage: null, lsmSem: null, lsmSemTaxonomy: null,
        lsmSemTaxonomyVersion: null, lsmSemMandatory: false, classification: 'INFERENCE',
        exclusions: ['Do not infer individual business ownership.'], evidenceItemIds: [],
        confidence: 0.7, status },
      { id: 'cf000000-0000-0000-0000-000000000002', name: 'Purchase influencers',
        description: 'People who may influence the final business purchasing decision.',
        needState: 'Confidence in the recommendation', buyingContext: 'Advising the buyer',
        geographies: ['Johannesburg'], language: null, lifeStage: null, lsmSem: null,
        lsmSemTaxonomy: null, lsmSemTaxonomyVersion: null, lsmSemMandatory: false,
        classification: 'HYPOTHESIS', exclusions: ['Do not assume authority to purchase.'],
        evidenceItemIds: [], confidence: 0.45, status },
    ],
    createdBy: userId, approvedBy: state.audienceApproved ? userId : null,
    version: state.audienceApproved ? 2 : 1,
    approvedAtUtc: state.audienceApproved ? now : null, createdAtUtc: now }
}

function mix(state: State) {
  return { id: mixId, briefVersionId, audienceSetId: audienceId, versionNumber: 1,
    totalBudgetMinor: 1_000_000, currency: 'ZAR',
    allocations: state.mix?.allocations ?? [], assumptions: [],
    inputHash: 'b'.repeat(64), status: state.mix?.status ?? 'DRAFT', createdBy: userId,
    approvedBy: state.mix?.status === 'APPROVED' ? userId : null,
    version: state.mix?.version ?? 1, createdAtUtc: now }
}

function shortlist(state: State) {
  return { id: shortlistId, briefVersionId, mixVersionId: mixId, versionNumber: 1,
    campaignCombinations: combinationFixture(candidateId),
    inputHash: 'c'.repeat(64), status: state.shortlist?.status ?? 'DRAFT', assumptions: [], version: state.shortlist?.version ?? 1,
    createdAtUtc: now, candidates: [{ id: candidateId, inventoryTenantId: tenantId,
      marketplaceListingVersionId: null, inventoryProductId: productId, productVersionId,
      rateId, availabilityId, name: 'Johannesburg OOH Site', channel: 'OOH', geography: 'Johannesburg',
      rateAmountMinor: 100_000, currency: 'ZAR', isEligible: true, rejectionReason: null, rejectionDetail: null,
      score: 88, rationale: 'Eligible after governed hard constraints and local peer review.',
      suitability: { buyAssessment: buyAssessmentFixture, policyVersion: 'OOH_LOCAL_PEER_V1', geography: 0, audienceContext: 0,
        objectiveFormat: 0, budgetEfficiency: 0, evidenceQualityFreshness: 1,
        portfolioCoverageDiversity: 0, total: 0.1, evidenceGaps: [
          'suitability.objectiveFormatEvidence', 'suitability.comparableTargetExposureCost',
          'suitability.incrementalReachEvidence'] },
      commercialReadiness: { supplierVatStatus: 'REGISTERED', vatTreatment: 'INCLUSIVE',
        supplierVatNumber: '4000000000', evidenceGaps: [], rateType: state.buyingRateType },
      audienceFit: {
        languageScore: null, lifeStageScore: null, lsmSemScore: null, evidenceGaps: [],
        measurementSource: null, measurementPeriod: null, methodology: null,
        taxonomyName: null, taxonomyVersion: null,
      },
      isSelected: state.shortlist?.selected ?? false, benchmark: { id: benchmarkId, policyVersion: 'OOH_LOCAL_PEER_V1',
        geographyBasis: 'RADIUS_5_KM', cohortSize: 4, medianMinor: 140_000, lowerQuartileMinor: 120_000,
        upperQuartileMinor: 160_000, percentile: 25, position: 'STRONG_VALUE', confidence: 0.4, exclusions: [] } }] }
}

function plan(current: NonNullable<State['plan']>) {
  const resolution = current.resolved ? 'ACCEPTED_WITH_REASON' : null
  return { id: planId, briefVersionId, mixVersionId: mixId, shortlistVersionId: shortlistId, versionNumber: 1,
    feesMinor: 5_000, vatMinor: 15_750, totalMinor: 120_750, currency: 'ZAR',
    supplyConfidence: 'UNKNOWN', inputHash: 'd'.repeat(64), status: current.status, assumptions: [],
    lines: [{ id: lineId, inventoryTenantId: tenantId, marketplaceListingVersionId: null,
      inventoryProductId: productId, productVersionId, rateId, availabilityId,
      name: 'Johannesburg OOH Site', channel: 'OOH', geography: 'Johannesburg',
      runningPeriods: [{ start: '2026-09-01', end: '2026-09-30' }], quantity: 1,
      clientPriceMinor: 120_750, feesMinor: 5_000, vatMinor: 15_750,
      availability: 'UNKNOWN', rateFreshness: 'CURRENT', supplySource: 'PUBLISHED_INVENTORY',
      lastConfirmedAtUtc: null, supplyConfidence: 'UNKNOWN' }],
    objections: [{ code: 'SUPPLY_UNCONFIRMED', severity: 'MATERIAL', affectedField: 'supply',
      evidenceGap: 'Supply is not yet confirmed.', recommendedResolution: 'Review the uncertainty before approval.',
      resolution, resolutionReason: current.resolved ? 'Reviewed.' : null,
      resolvedBy: current.resolved ? userId : null }], createdBy: userId,
    approvedBy: current.status === 'APPROVED' ? userId : null, version: current.version, createdAtUtc: now }
}

function session() { return { authenticated: true, antiforgeryToken: 'csrf-planning', expiresAtUtc: '2026-08-29T23:00:00Z', signInPath: null, signOutPath: null } }
function workspace() { return { membershipId: 'd1000000-0000-0000-0000-000000000001', tenantId, name: 'Planning Agency', slug: 'planning-agency', roleCode: 'agency_admin', version: 1 } }
function user() { return { id: userId, email: 'planner@example.com', displayName: 'Planner', phone: null, mfaEnabled: true, version: 1 } }

function assertMutation(route: Route, versioned: boolean) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-planning')
  expect(headers['idempotency-key']).toBeTruthy()
  if (versioned) expect(headers['if-match']).toBeTruthy()
}

async function json(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}
