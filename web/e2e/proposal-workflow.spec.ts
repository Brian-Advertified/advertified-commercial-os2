import { expect, test, type Route } from '@playwright/test'

const tenantId = '91000000-0000-0000-0000-000000000001'
const agencyUserId = '92000000-0000-0000-0000-000000000001'
const clientUserId = '92000000-0000-0000-0000-000000000002'
const briefId = '93000000-0000-0000-0000-000000000001'
const briefVersionId = '93000000-0000-0000-0000-000000000002'
const proposalId = '94000000-0000-0000-0000-000000000001'
const documentId = '94000000-0000-0000-0000-000000000002'
const now = '2026-08-29T20:00:00Z'

const planIds = [
  '95000000-0000-0000-0000-000000000001',
  '95000000-0000-0000-0000-000000000002',
  '95000000-0000-0000-0000-000000000003',
] as const

const optionIds = [
  '96000000-0000-0000-0000-000000000001',
  '96000000-0000-0000-0000-000000000002',
  '96000000-0000-0000-0000-000000000003',
] as const

type Role = 'agency_admin' | 'advertiser_approver'
type ProposalStatus = 'DRAFT' | 'APPROVED' | 'SENT' | 'SELECTED'
type OptionState = { planVersionId: string; label: string; outcome: string }
type State = {
  role: Role
  userId: string
  status: ProposalStatus
  version: number
  title: string
  summary: string
  terms: string
  expiryAtUtc: string
  document: boolean
  recipientUserId: string | null
  selectedOptionId: string | null
  externalDecision: null | {
    optionId: string
    evidenceReference: string
    reason: string | null
  }
  options: OptionState[]
  campaignMode: 'OOH_ONLY' | 'FULL_CAMPAIGN'
}

test('agency prepares three approved-plan choices and client selects one', async ({ page }) => {
  const state = initialState()
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))

  await page.goto(`/briefs/${briefId}/proposals/new`)
  await expect(page.getByRole('heading', { name: 'Build clear choices from approved plans' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Full Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'FULL_CAMPAIGN')
  await page.getByText('Plan 1', { exact: true }).click()
  await page.getByText('Plan 2', { exact: true }).click()
  await page.getByText('Plan 3', { exact: true }).click()
  await page.getByLabel('Proposal title').fill('Three routes to qualified demand')
  await page.getByRole('button', { name: 'Create proposal' }).click()

  await expect(page.getByRole('heading', { name: 'Three routes to qualified demand' })).toBeVisible()
  await page.getByLabel('Executive summary').fill(
    'Choose the route that best balances visibility, trust and measurable response.')
  await page.getByRole('button', { name: 'Save wording' }).click()
  await page.getByRole('button', { name: 'Approve now' }).click()
  await page.getByRole('button', { name: 'Create branded PDF' }).click()
  await expect(page.getByRole('link', { name: 'Open proposal PDF' })).toBeVisible()
  await page.getByLabel('Client recipient').selectOption(clientUserId)
  await page.getByRole('button', { name: 'Share with client' }).click()
  await expect(page.getByText('Waiting for the client decision')).toBeVisible()

  state.role = 'advertiser_approver'
  state.userId = clientUserId
  await page.reload()
  await expect(page.getByText('Decision required')).toBeVisible()
  const digitalChoice = page.locator('article').filter({
    has: page.getByRole('heading', { name: 'Digital route' }),
  })
  await digitalChoice.getByRole('button', { name: 'Select this option' }).click()
  await expect(page.getByText('Your selected route has been recorded.')).toBeVisible()
  await expect(digitalChoice.getByText('Selected by the client')).toBeVisible()
})

test('Rapid OOH operator records the verified external client reply', async ({ page }) => {
  const state = initialState()
  state.status = 'SENT'
  state.version = 6
  state.document = true
  state.options = [{
    planVersionId: planIds[0],
    label: 'OOH launch route',
    outcome: 'Reach the priority commuting audience.',
  }]
  state.campaignMode = 'OOH_ONLY'
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', route => handleApi(route, state))

  await page.goto(`/proposals/${proposalId}`)
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'OOH_ONLY')
  await expect(page.getByRole('heading', { name: 'Record the verified client reply' }))
    .toBeVisible()
  await page.getByLabel('Evidence reference').fill('mailhog-message-42')
  await page.getByLabel('Decision note').fill('Client selected the OOH launch route by reply email.')
  await page.getByRole('button', { name: 'Record verified client decision' }).click()

  await expect(page.getByRole('heading', {
    name: 'Continue with purchase order and funding',
  })).toBeVisible()
  await expect(page.getByText(
    'Verified external reply from rapid-client@example.com · Evidence: mailhog-message-42',
  )).toBeVisible()
  await expect(page.getByRole('link', { name: /Open funding/ })).toBeVisible()
})

function initialState(): State {
  return {
    role: 'agency_admin', userId: agencyUserId, status: 'DRAFT', version: 1,
    title: 'Media proposal', summary: 'Three approved media-plan routes are ready for review.',
    terms: 'Rates and availability remain bound to approved plan evidence.',
    expiryAtUtc: '2026-09-28T21:59:59.000Z', document: false,
    recipientUserId: null, selectedOptionId: null, externalDecision: null, options: [],
    campaignMode: 'FULL_CAMPAIGN',
  }
}

async function handleApi(route: Route, state: State) {
  const path = new URL(route.request().url()).pathname
  if (route.request().method() === 'GET') return handleRead(route, state, path)
  assertMutation(route, requiresVersion(path))
  return handleWrite(route, state, path)
}

async function handleRead(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, [workspaceFixture(state.role)])
  if (path === '/api/v1/me') return json(route, userFixture(state), 200, { ETag: '"1"' })
  if (path.endsWith(`/briefs/${briefId}/approved-plans`)) return json(route, planFixtures())
  if (path.endsWith(`/brief-versions/${briefVersionId}/planning`)) {
    return json(route, planningFixture(state.campaignMode))
  }
  if (path.endsWith('/proposal-recipients')) return json(route, recipientFixtures())
  if (path.endsWith('/proposal-approvers')) return json(route, approverFixtures())
  if (path.endsWith(`/proposals/${proposalId}`)) return json(route, proposalFixture(state))
  if (path.endsWith(`/proposal-documents/${documentId}`)) {
    return route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.4\n%%EOF' })
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleWrite(route: Route, state: State, path: string) {
  if (path.endsWith('/proposals:generate')) return generate(route, state)
  if (path.endsWith(`${proposalId}:update`)) return update(route, state)
  if (path.endsWith(`${proposalId}:approve`)) return transition(route, state, 'APPROVED')
  if (path.endsWith(`${proposalId}:render`)) {
    state.document = true; state.version += 1
    return json(route, proposalFixture(state))
  }
  if (path.endsWith(`${proposalId}:share`)) return share(route, state)
  if (path.endsWith(`${proposalId}:record-external-decision`)) {
    return recordExternalDecision(route, state)
  }
  if (path.endsWith(`${proposalId}:select-option`)) return selectOption(route, state)
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function generate(route: Route, state: State) {
  const body = route.request().postDataJSON() as {
    title: string; terms: string; expiryAtUtc: string; options: OptionState[]
  }
  state.title = body.title; state.terms = body.terms; state.expiryAtUtc = body.expiryAtUtc
  state.options = body.options; state.version = 1
  return json(route, proposalFixture(state))
}

async function update(route: Route, state: State) {
  const body = route.request().postDataJSON() as {
    title: string; executiveSummary: string; terms: string; expiryAtUtc: string
    options: Array<{ optionId: string; label: string; outcome: string }>
  }
  state.title = body.title; state.summary = body.executiveSummary
  state.terms = body.terms; state.expiryAtUtc = body.expiryAtUtc
  state.options = body.options.map((item, index) => ({
    planVersionId: state.options[index].planVersionId,
    label: item.label,
    outcome: item.outcome,
  }))
  state.version += 1
  return json(route, proposalFixture(state))
}

async function transition(route: Route, state: State, status: ProposalStatus) {
  state.status = status; state.version += 1
  return json(route, proposalFixture(state))
}

async function share(route: Route, state: State) {
  const body = route.request().postDataJSON() as { recipientUserId: string }
  state.recipientUserId = body.recipientUserId
  return transition(route, state, 'SENT')
}

async function recordExternalDecision(route: Route, state: State) {
  const body = route.request().postDataJSON() as {
    optionId: string
    declined: boolean
    evidenceReference: string
    reason: string | null
  }
  expect(body).toEqual({
    optionId: optionIds[0],
    declined: false,
    evidenceReference: 'mailhog-message-42',
    reason: 'Client selected the OOH launch route by reply email.',
  })
  state.selectedOptionId = body.optionId
  state.externalDecision = {
    optionId: body.optionId,
    evidenceReference: body.evidenceReference,
    reason: body.reason,
  }
  return transition(route, state, 'SELECTED')
}

async function selectOption(route: Route, state: State) {
  const body = route.request().postDataJSON() as { optionId: string }
  state.selectedOptionId = body.optionId
  return transition(route, state, 'SELECTED')
}

function proposalFixture(state: State) {
  return {
    id: proposalId, briefId, briefVersionId, versionNumber: 1,
    title: state.title, executiveSummary: state.summary, terms: state.terms,
    expiryAtUtc: state.expiryAtUtc, status: state.status,
    options: state.options.map((item, index) => optionFixture(item, index)),
    document: state.document ? {
      id: documentId, mediaType: 'application/pdf', contentHash: 'a'.repeat(64),
      sizeBytes: 2048, createdAtUtc: now,
    } : null,
    recipientUserId: state.recipientUserId,
    decision: decisionFixture(state),
    createdBy: agencyUserId,
    approvedBy: state.status === 'DRAFT' ? null : agencyUserId,
    approvalMode: state.status === 'DRAFT' ? null : 'SELF',
    approvalAssigneeUserId: null,
    approvalRequestedBy: null,
    approvalRequestedAtUtc: null,
    approvalRejectedBy: null,
    approvalRejectionReason: null,
    approvalRejectedAtUtc: null,
    version: state.version, createdAtUtc: now,
  }
}

function planningFixture(mode: State['campaignMode']) {
  return {
    briefId,
    briefVersionId,
    clientName: 'Proposal Client',
    campaignMode: {
      id: '93000000-0000-0000-0000-000000000003',
      briefVersionId,
      mode,
      allowedChannels: mode === 'OOH_ONLY' ? ['OOH', 'DOOH'] : [
        'OOH', 'DOOH', 'RADIO', 'DIGITAL',
      ],
      isLocked: true,
      decisionSource: 'SUPPLIED_BRIEF_EVIDENCE',
      confidence: 1,
      reason: 'The supplied media requirement establishes the campaign mode.',
      selectedBy: agencyUserId,
      selectedAtUtc: now,
    },
    audience: null,
    mediaMix: null,
    shortlist: null,
    mediaPlan: null,
  }
}

function decisionFixture(state: State) {
  if (!state.selectedOptionId) return null
  const external = state.externalDecision
  return {
    decision: 'SELECTED',
    optionId: state.selectedOptionId,
    reason: external?.reason ?? 'Client selected this proposal route.',
    decidedBy: external ? agencyUserId : clientUserId,
    decidedAtUtc: now,
    recordedForExternalParty: Boolean(external),
    externalPartyEmail: external ? 'rapid-client@example.com' : null,
    evidenceReference: external?.evidenceReference ?? null,
  }
}

function optionFixture(item: OptionState, index: number) {
  const plan = planFixtures()[planIds.indexOf(item.planVersionId as typeof planIds[number])]
  return {
    id: optionIds[index], label: item.label, outcome: item.outcome,
    planVersionId: item.planVersionId, planVersionNumber: plan.versionNumber,
    budgetMinor: plan.totalMinor, currency: plan.currency, displayOrder: index + 1,
    channels: plan.channels, runningPeriods: plan.runningPeriods,
    inventoryNames: [`${plan.channels[0]} approved placement`],
    inventory: [],
  }
}

function planFixtures() {
  return [
    planFixture(planIds[0], 1, 10_000_000, ['OOH']),
    planFixture(planIds[1], 2, 20_000_000, ['RADIO']),
    planFixture(planIds[2], 3, 35_000_000, ['DIGITAL']),
  ]
}

function planFixture(id: string, versionNumber: number, totalMinor: number, channels: string[]) {
  return {
    id, briefVersionId, versionNumber, totalMinor, currency: 'ZAR', channels,
    runningPeriods: channels.map(channel => ({
      channel, start: '2026-09-01', end: '2026-09-30',
    })),
    createdAtUtc: now,
  }
}

function recipientFixtures() {
  return [{ userId: clientUserId, displayName: 'Client Approver',
    email: 'client@example.com', role: 'advertiser_approver' }]
}

function approverFixtures() {
  return [{ userId: agencyUserId, displayName: 'Agency Operator',
    email: 'agency@example.com', role: 'agency_admin' }]
}

function requiresVersion(path: string) {
  return !path.endsWith('/proposals:generate')
}

function assertMutation(route: Route, versioned: boolean) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-proposal')
  expect(headers['idempotency-key']).toBeTruthy()
  if (versioned) expect(headers['if-match']).toBeTruthy()
}

function sessionFixture() {
  return { authenticated: true, antiforgeryToken: 'csrf-proposal', expiresAtUtc: '2026-08-30T02:00:00Z', signInPath: null, signOutPath: null }
}

function workspaceFixture(role: Role) {
  return { membershipId: '97000000-0000-0000-0000-000000000001', tenantId,
    name: 'Proposal Workspace', slug: 'proposal-workspace', roleCode: role, version: 1 }
}

function userFixture(state: State) {
  return { id: state.userId, email: state.role === 'agency_admin' ? 'agency@example.com' : 'client@example.com',
    displayName: state.role === 'agency_admin' ? 'Agency Operator' : 'Client Approver',
    phone: null, mfaEnabled: true, version: 1 }
}

async function json(
  route: Route,
  body: unknown,
  status = 200,
  headers?: Record<string, string>,
) {
  await route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(body) })
}
