import { expect, test, type Route } from '@playwright/test'

const tenantId = 'b1000000-0000-0000-0000-000000000001'
const userId = 'b2000000-0000-0000-0000-000000000001'
const clientId = 'b3000000-0000-0000-0000-000000000001'
const briefId = 'b4000000-0000-0000-0000-000000000001'
const sourceId = 'b5000000-0000-0000-0000-000000000001'
const versionId = 'b6000000-0000-0000-0000-000000000001'
const now = '2026-08-29T16:00:00Z'

type State = {
  status: 'DRAFT' | 'IN_REVIEW' | 'READY' | 'APPROVED'
  version: number
  source: string
  campaignMode: 'OOH_ONLY' | 'FULL_CAMPAIGN'
}

test('a reviewed supplied Brief proceeds to Strategy and STP', async ({ page }) => {
  const state: State = {
    status: 'DRAFT', version: 1, source: '', campaignMode: 'OOH_ONLY',
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto('/briefs/new')
  await page.getByLabel('Campaign or Brief name').fill('December enquiry Brief')
  await page.getByLabel('Original Brief').fill(
    'Client One needs qualified Gauteng enquiries by December with a R100,000 media budget. The media type is unclear.')
  await page.getByRole('button', { name: 'Understand this Brief' }).click()

  await expect(page.getByRole('heading', {
    name: 'Confirm only what could not be established',
  })).toBeVisible()
  await page.getByRole('radio', { name: /OOH and DOOH only/ }).check()
  await page.getByRole('button', { name: 'Review the completed Brief' }).click()

  await expect(page.getByRole('heading', {
    name: 'Confirm what Advertified understood before planning begins.',
  })).toBeVisible()
  await page.getByRole('button', { name: 'Approve Brief and start planning' }).click()

  await expect(page).toHaveURL(new RegExp(`/stp/${versionId}$`))
  await expect(page.getByRole('heading', { name: 'Strategy & STP' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'OOH_ONLY')
  await expect(page.getByRole('button', { name: 'Generate Strategy & STP' })).toBeVisible()
})

test('Brief sections show progress and provide a governed continuation', async ({ page }) => {
  const state: State = {
    status: 'READY',
    version: 3,
    source: 'The supplied client wording remains visible throughout review.',
    campaignMode: 'OOH_ONLY',
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto(`/briefs/${briefId}#brief-objectives`)
  await expect(page.getByRole('heading', { name: 'Review Campaign Brief' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'OOH_ONLY')
  await expect(page.getByText(
    'Client One · December enquiry Brief · Version 1', { exact: true },
  )).toBeVisible()
  await expect(page.getByRole('link', { name: /Objectives Complete/ }))
    .toHaveAttribute('aria-current', 'step')
  await expect(page.getByRole('link', { name: /Measurement Needs attention/ })).toBeVisible()
  const objectivesStep = page.getByRole('link', { name: /Objectives Complete/ })
  const stepCopy = objectivesStep.locator('.approved-brief-step-copy')
  const stepState = objectivesStep.locator('.approved-brief-step-state')
  const [copyBox, stateBox] = await Promise.all([
    stepCopy.boundingBox(),
    stepState.boundingBox(),
  ])
  expect(copyBox?.width).toBeGreaterThan(40)
  expect((copyBox?.x ?? 0) + (copyBox?.width ?? 0)).toBeLessThanOrEqual(stateBox?.x ?? 0)

  await page.getByRole('button', { name: 'Continue to Audience →' }).click()
  await expect(page).toHaveURL(new RegExp('#brief-audience$'))
  await expect(page.getByRole('heading', { name: 'Audience' })).toBeVisible()

  await page.getByRole('link', { name: /Attachments Complete/ }).click()
  await page.getByText('View original source', { exact: true }).click()
  await expect(page.getByText(state.source, { exact: true })).toBeVisible()

  await page.getByRole('link', { name: /Review & Submit Needs attention/ }).click()
  await expect(page.getByRole('heading', { name: 'Review & Submit' })).toBeVisible()
  await page.getByRole('button', { name: 'Approve Brief and continue' }).click()
  await expect(page.getByRole('link', { name: 'Next: Strategy & STP →' })).toBeVisible()
  await page.getByRole('link', { name: 'Next: Strategy & STP →' }).click()
  await expect(page).toHaveURL(new RegExp(`/stp/${versionId}$`))
})

test('a Full Campaign Brief keeps its persisted mode on the same lifecycle rail', async ({ page }) => {
  const state: State = {
    status: 'READY', version: 2, source: 'Integrated media request.',
    campaignMode: 'FULL_CAMPAIGN',
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async route => handleApi(route, state))

  await page.goto(`/briefs/${briefId}`)
  const rail = page.getByRole('region', { name: 'Full Campaign Flow' })
  await expect(rail).toBeVisible()
  await expect(rail).toHaveAttribute('data-campaign-mode', 'FULL_CAMPAIGN')
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' })).toHaveCount(0)
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  return request.method() === 'GET'
    ? handleReadApi(route, state, path)
    : handleWriteApi(route, state, path)
}

async function handleReadApi(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, [workspaceFixture()])
  if (path === '/api/v1/me') return json(route, userFixture(), 200, { ETag: '"1"' })
  if (path.endsWith('/human-tasks')) return json(route, { items: [], nextCursor: null })
  if (path.endsWith('/client-accounts')) {
    return json(route, { items: [clientFixture()], nextCursor: null })
  }
  if (path.endsWith(`/briefs/${briefId}`)) return json(route, briefFixture(state))
  if (path.endsWith(`/brief-versions/${versionId}/planning`)) {
    return json(route, planningFixture(state.campaignMode))
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleWriteApi(route: Route, state: State, path: string) {
  const request = route.request()
  if (path.endsWith('/briefs:understand')) {
    expect(request.headers()['x-csrf-token']).toBe('csrf-brief')
    const body = request.postDataJSON() as {
      sourceTitle: string
      sourceContent: string
      clarifications: Array<{ fieldPath: string; value: string }>
    }
    expect(body.sourceTitle).toBe('December enquiry Brief')
    state.source = body.sourceContent
    const mode = body.clarifications.find(item => item.fieldPath === 'campaignMode')?.value
    return json(route, understandingFixture(mode ?? null))
  }
  if (path.endsWith('/briefs')) {
    assertMutation(route, false)
    const body = request.postDataJSON() as { clientId: string | null; clientName: string }
    expect(body.clientId).toBeNull()
    expect(body.clientName).toBe('Client One')
    return json(route, briefSummary('CREATED', 1), 201)
  }
  if (path.endsWith(`/briefs/${briefId}/versions`)) {
    assertMutation(route, false)
    return json(route, versionFixture(state), 201)
  }
  if (path.endsWith(`${versionId}:submit`)) {
    assertMutation(route, true)
    expect(request.postDataJSON()).toEqual({ confirmerUserId: null, comment: null })
    state.status = 'IN_REVIEW'
    state.version += 1
    return json(route, versionFixture(state))
  }
  if (path.endsWith(`${versionId}:approve`)) {
    assertMutation(route, true)
    expect(request.postDataJSON()).toEqual({ reason: 'Confirmed for planning.' })
    state.status = 'APPROVED'
    state.version += 1
    return json(route, versionFixture(state))
  }
  if (path.endsWith(`/brief-versions/${versionId}/campaign-mode:select`)) {
    assertMutation(route, false)
    const mode = (request.postDataJSON() as { mode: State['campaignMode'] }).mode
    expect(mode).toBe('OOH_ONLY')
    state.campaignMode = mode
    return json(route, campaignModeFixture(mode))
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function understandingFixture(mode: string | null) {
  const needsChoice = mode === null
  return {
    clientName: 'Client One', title: 'December enquiry Brief', campaignMode: mode,
    campaignModeConfidence: needsChoice ? 0 : 1,
    requiresHumanClarification: needsChoice,
    campaignModeRationale: needsChoice
      ? 'The supplied Brief does not identify the required media.'
      : 'The user selected out-of-home only.',
    draft: {
      businessProblem: 'Qualified demand is not established.',
      objective: 'Generate qualified enquiries.', audiences: ['Workspace furniture buyers'],
      geographies: ['Gauteng'], timing: 'By December 2026', budgetMinor: 10000000,
      budgetUnknown: false, currency: 'ZAR', vatStatus: null, feesMinor: null,
      mediaRequirements: mode ? ['OOH'] : [], constraints: [], measurement: [],
      facts: ['The supplied Brief names Gauteng, December and the budget.'], unknowns: [],
      assumptions: [], conflicts: [],
    },
    questions: needsChoice ? [{
      fieldPath: 'campaignMode',
      question: 'Should this use only out-of-home media or a full campaign?',
      isBlocking: true, options: ['OOH_ONLY', 'FULL_CAMPAIGN'],
    }] : [],
    evidence: [{
      fieldPath: 'geographies', kind: 'SUPPLIED_FACT', excerpt: 'Gauteng', confidence: 1,
      sourceLocator: 'supplied:web:test',
    }],
    usage: {
      provider: 'deterministic-local', model: 'fixture', promptVersion: '1.0.0',
      researchStatus: 'NOT_RUN', toolCalls: 0, incrementalCostMinor: 0,
    },
  }
}

function planningFixture(mode: State['campaignMode']) {
  return {
    briefId,
    briefVersionId: versionId,
    clientName: 'Client One',
    campaignMode: campaignModeFixture(mode),
    audience: null,
    mediaMix: null,
    shortlist: null,
    mediaPlan: null,
  }
}

function campaignModeFixture(mode: State['campaignMode']) {
  return {
    id: 'b6500000-0000-0000-0000-000000000001', briefVersionId: versionId,
    mode, allowedChannels: mode === 'OOH_ONLY' ? ['OOH', 'DOOH'] : [
      'OOH', 'DOOH', 'RADIO', 'TV', 'PRINT', 'DIGITAL',
    ], isLocked: true,
    decisionSource: 'HUMAN_CLARIFICATION', confidence: 1,
    reason: 'The user resolved the unclear media requirement.', selectedBy: userId,
    selectedAtUtc: now,
  }
}

function briefFixture(state: State) {
  return {
    brief: briefSummary(state.status, state.version),
    sources: [{
      id: sourceId, sourceType: 'SUPPLIED_TEXT', locator: 'supplied:web:1',
      title: 'December enquiry Brief supplied source', content: state.source,
      contentHash: 'a'.repeat(64), createdBy: userId, createdAtUtc: now,
    }],
    versions: [versionFixture(state)],
  }
}

function briefSummary(status: string, version: number) {
  return {
    id: briefId, tenantId, clientId, clientName: 'Client One', opportunityId: null,
    title: 'December enquiry Brief',
    ownerUserId: userId, status, currentDraftVersionId: status === 'CREATED' ? null : versionId,
    readyVersionId: status === 'READY' || status === 'APPROVED' ? versionId : null,
    approvedVersionId: status === 'APPROVED' ? versionId : null,
    version, updatedAtUtc: now,
  }
}

function versionFixture(state: State) {
  return {
    id: versionId, briefId, baseVersionId: null, sourceId, versionNumber: 1,
    businessProblem: 'Qualified demand is not established.', objective: 'Generate qualified enquiries.',
    audiences: ['Workspace furniture buyers'], geographies: ['Gauteng'], timing: 'By December 2026',
    budgetMinor: 10000000, budgetUnknown: false, currency: 'ZAR',
    vatStatus: null, feesMinor: null,
    constraints: [], measurement: [], facts: [], unknowns: [], assumptions: [], conflicts: [],
    evidenceItemIds: [], status: state.status, createdBy: userId,
    submittedBy: state.status === 'IN_REVIEW' || state.status === 'APPROVED' ? userId : null,
    approvedBy: state.status === 'APPROVED' ? userId : null,
    approvalMode: state.status === 'APPROVED' ? 'SELF' : null,
    rejectedBy: null, rejectionReason: null, requestedChanges: null,
    version: state.version, createdAtUtc: now,
  }
}

function sessionFixture() {
  return {
    authenticated: true,
    antiforgeryToken: 'csrf-brief',
    expiresAtUtc: '2026-08-29T20:00:00Z',
    signInPath: null,
    signOutPath: null,
  }
}

function workspaceFixture() {
  return {
    membershipId: 'b7000000-0000-0000-0000-000000000001',
    tenantId, name: 'Solo Agency', slug: 'solo-agency',
    roleCode: 'agency_admin', version: 1,
  }
}

function userFixture() {
  return {
    id: userId, email: 'solo@example.com', displayName: 'Solo Operator',
    phone: null, mfaEnabled: true, version: 1,
  }
}

function clientFixture() {
  return {
    id: clientId, tenantId, externalReference: 'solo-client',
    legalName: 'Client One', tradingName: 'Client One', website: null,
    industry: null, billingProfileJson: '{}', primaryContactId: null,
    statusCode: 'ACTIVE', version: 1, updatedAtUtc: now,
  }
}

function assertMutation(route: Route, versioned: boolean) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-brief')
  expect(headers['idempotency-key']).toBeTruthy()
  if (versioned) expect(headers['if-match']).toBeTruthy()
}

async function json(
  route: Route,
  body: unknown,
  status = 200,
  headers?: Record<string, string>,
) {
  await route.fulfill({
    status, headers, contentType: 'application/json', body: JSON.stringify(body),
  })
}
