import { expect, test, type Route } from '@playwright/test'

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

type State = {
  audience: boolean
  audienceApproved: boolean
  mix: null | { status: 'DRAFT' | 'APPROVED'; version: number; periods: { start: string; end: string }[] }
  shortlist: null | { status: 'DRAFT' | 'APPROVED'; version: number; selected: boolean }
  plan: null | { status: 'IN_REVIEW' | 'APPROVED'; version: number; resolved: boolean }
}

test('planner edits allocation and timing before approving the plan', async ({ page }) => {
  const state: State = { audience: false, audienceApproved: false, mix: null, shortlist: null, plan: null }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto(`/planning/${briefVersionId}`)
  await expect(page.getByText('Planning Client · Campaign planning', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Build audience direction' }).click()
  await expect(page.getByText('Local business decision makers', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Create media mix' }).click()
  await expect(page.getByRole('heading', { name: 'Shape the investment and timing' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Out of Home' })).toBeVisible()
  await page.getByRole('button', { name: '+ Add period' }).click()
  await page.getByLabel('Start').fill('2026-09-01')
  await page.getByLabel('End').fill('2026-09-30')
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.locator('.timeline-segment[title*="2026-09-01 to 2026-09-30"]')).toBeVisible()
  await page.getByRole('button', { name: 'Confirm media mix' }).click()

  await page.getByRole('button', { name: 'Build inventory shortlist' }).click()
  await page.getByText('Market comparison').click()
  await expect(page.getByText('4 comparable sites')).toBeVisible()
  await page.getByLabel('Select Johannesburg OOH Site').check()
  await page.getByRole('button', { name: 'Confirm selected inventory' }).click()

  await page.getByRole('button', { name: 'Create media plan' }).click()
  await expect(page.locator('.plan-line-periods span').first()).toContainText('2026')
  await page.getByRole('button', { name: 'Review and accept' }).click()
  await page.getByRole('button', { name: 'Approve media plan' }).click()
  await expect(page.getByText('Media plan approved and ready for proposal preparation.')).toBeVisible()
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (request.method() === 'GET') return read(route, state, path)
  assertMutation(route, isVersioned(path))
  if (path.includes('audiences') || path.includes('media-mix')) return handleMixCommand(route, state, path)
  if (path.includes('shortlist')) return handleShortlistCommand(route, state, path)
  if (path.includes('media-plan')) return handlePlanCommand(route, state, path)
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleMixCommand(route: Route, state: State, path: string) {
  if (path.endsWith('/audiences:generate')) {
    state.audience = true
    state.audienceApproved = true
    return json(route, audience(state))
  }
  if (path.endsWith(`${audienceId}:approve`)) {
    state.audienceApproved = true
    return json(route, audience(state))
  }
  if (path.endsWith('/media-mixes:generate')) {
    state.mix = { status: 'DRAFT', version: 1, periods: [] }; state.shortlist = null; state.plan = null
    return json(route, mix(state))
  }
  if (path.endsWith(`${mixId}:update`)) {
    const body = route.request().postDataJSON() as { allocations: Array<{ runningPeriods: Array<{ start: string; end: string }> }> }
    state.mix = { status: 'DRAFT', version: 2, periods: body.allocations[0].runningPeriods }
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
    campaignMode: campaignMode(),
    audience: state.audience ? audience(state) : null,
    mediaMix: state.mix ? mix(state) : null,
    shortlist: state.shortlist ? shortlist(state) : null,
    mediaPlan: state.plan ? plan(state.plan) : null,
  }
}

function campaignMode() {
  return {
    id: 'c3500000-0000-0000-0000-000000000001', briefVersionId,
    mode: 'OOH_ONLY', allowedChannels: ['OOH', 'DOOH'], isLocked: true,
    decisionSource: 'AGENT', confidence: 0.95,
    reason: 'The supplied Brief requests only OOH.', selectedBy: userId, selectedAtUtc: now,
  }
}

function audience(state: State) {
  return { id: audienceId, briefVersionId, versionNumber: 1,
    targetAudienceIds: ['cf000000-0000-0000-0000-000000000001'],
    targetingRationale: 'Prioritise local business decision makers in Johannesburg.',
    positioningStatement: 'Present the advertiser as the practical local growth partner.',
    inputHash: 'a'.repeat(64), status: state.audienceApproved ? 'APPROVED' : 'DRAFT',
    definitions: [{ id: 'cf000000-0000-0000-0000-000000000001', name: 'Local business decision makers',
      description: 'Businesses seeking local customer demand.', needState: 'Growth', buyingContext: 'Local purchase',
      geographies: ['Johannesburg'], language: null, lifeStage: null, lsmSem: null,
      classification: 'INFERENCE', exclusions: [], evidenceItemIds: [], confidence: 0.7, status: 'APPROVED' }],
    createdAtUtc: now }
}

function mix(state: State) {
  return { id: mixId, briefVersionId, audienceSetId: audienceId, versionNumber: 1,
    totalBudgetMinor: 1_000_000, currency: 'ZAR', allocations: [{ channel: 'OOH', budgetMinor: 1_000_000,
      role: 'Primary local visibility', runningPeriods: state.mix?.periods ?? [] }], assumptions: [],
    inputHash: 'b'.repeat(64), status: state.mix?.status ?? 'DRAFT', createdBy: userId,
    approvedBy: state.mix?.status === 'APPROVED' ? userId : null, version: state.mix?.version ?? 1, createdAtUtc: now }
}

function shortlist(state: State) {
  return { id: shortlistId, briefVersionId, mixVersionId: mixId, versionNumber: 1,
    inputHash: 'c'.repeat(64), status: state.shortlist?.status ?? 'DRAFT', assumptions: [], version: state.shortlist?.version ?? 1,
    createdAtUtc: now, candidates: [{ id: candidateId, inventoryTenantId: tenantId,
      marketplaceListingVersionId: null, inventoryProductId: productId, productVersionId,
      rateId, availabilityId, name: 'Johannesburg OOH Site', channel: 'OOH', geography: 'Johannesburg',
      rateAmountMinor: 100_000, currency: 'ZAR', isEligible: true, rejectionReason: null, rejectionDetail: null,
      score: 88, rationale: 'Eligible after governed hard constraints and local peer review.',
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

function session() { return { authenticated: true, antiforgeryToken: 'csrf-planning', expiresAtUtc: '2026-08-29T23:00:00Z' } }
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
