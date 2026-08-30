import { expect, test, type Route } from '@playwright/test'

const tenantId = 'b1000000-0000-0000-0000-000000000001'
const userId = 'b2000000-0000-0000-0000-000000000001'
const clientId = 'b3000000-0000-0000-0000-000000000001'
const briefId = 'b4000000-0000-0000-0000-000000000001'
const sourceId = 'b5000000-0000-0000-0000-000000000001'
const versionId = 'b6000000-0000-0000-0000-000000000001'
const now = '2026-08-29T16:00:00Z'

type State = {
  status: 'DRAFT' | 'IN_REVIEW' | 'APPROVED'
  version: number
  source: string
}

test('one agency operator takes a supplied Brief through confirmation', async ({ page }) => {
  const state: State = { status: 'DRAFT', version: 1, source: '' }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/briefs/new')
  await page.getByLabel('Campaign or Brief name').fill('December enquiry Brief')
  await page.getByLabel('Original Brief').fill(
    'Client One needs qualified Gauteng enquiries by December. The media type is unclear.')
  await page.getByRole('button', { name: 'Create campaign from Brief' }).click()

  await expect(page.getByRole('heading', {
    name: 'Answer only what could not be confirmed',
  })).toBeVisible()
  await page.getByLabel('Should this use only out-of-home media or a full campaign?')
    .selectOption('OOH_ONLY')
  await page.getByRole('button', { name: 'Continue to planning' }).click()
  await expect(page).toHaveURL(new RegExp(`/planning/${versionId}$`))
})

test('advertiser can read a Brief without being asked to confirm it', async ({ page }) => {
  const state: State = {
    status: 'DRAFT', version: 1,
    source: 'The supplied client wording remains visible without an approval request.',
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) =>
    handleApi(route, state, 'advertiser_approver'))

  await page.goto(`/briefs/${briefId}`)
  await expect(page.getByRole('heading', { name: 'December enquiry Brief' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Confirm this Brief' })).toHaveCount(0)
})

async function handleApi(route: Route, state: State, role = 'agency_admin') {
  const request = route.request()
  const path = new URL(request.url()).pathname
  return request.method() === 'GET'
    ? handleReadApi(route, state, path, role)
    : handleWriteApi(route, state, path)
}

async function handleReadApi(route: Route, state: State, path: string, role: string) {
  if (path === '/api/v1/session') return json(route, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, [workspaceFixture(role)])
  if (path === '/api/v1/me') return json(route, userFixture(), 200, { ETag: '"1"' })
  if (path.endsWith('/client-accounts')) {
    return json(route, { items: [clientFixture()], nextCursor: null })
  }
  if (path.endsWith(`/briefs/${briefId}`)) return json(route, briefFixture(state))
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
    return json(route, briefSummary('CREATED'), 201)
  }
  if (path.endsWith(`/briefs/${briefId}/versions`)) {
    assertMutation(route, false); return json(route, versionFixture(state), 201)
  }
  if (path.endsWith(`${versionId}:submit`)) {
    assertMutation(route, true)
    const body = request.postDataJSON() as Record<string, unknown>
    expect(body.confirmerUserId).toBeNull()
    expect(body.approverUserId).toBeUndefined()
    state.status = 'IN_REVIEW'; state.version = 2
    return json(route, versionFixture(state))
  }
  if (path.endsWith(`${versionId}:approve`)) {
    assertMutation(route, true); state.status = 'APPROVED'; state.version = 3
    return json(route, versionFixture(state))
  }
  if (path.endsWith(`/brief-versions/${versionId}/campaign-mode:select`)) {
    assertMutation(route, false)
    expect((request.postDataJSON() as { mode: string }).mode).toBe('OOH_ONLY')
    return json(route, {
      id: 'b6500000-0000-0000-0000-000000000001', briefVersionId: versionId,
      mode: 'OOH_ONLY', allowedChannels: ['OOH', 'DOOH'], isLocked: true,
      decisionSource: 'HUMAN_CLARIFICATION', confidence: 1,
      reason: 'The user resolved the unclear media requirement.', selectedBy: userId,
      selectedAtUtc: now,
    })
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
      geographies: ['Gauteng'], timing: 'By December 2026', budgetMinor: null,
      budgetUnknown: true, currency: null, vatStatus: null, feesMinor: null,
      mediaRequirements: mode ? ['OOH'] : [], constraints: [], measurement: [],
      facts: ['The supplied Brief names Gauteng and December.'], unknowns: [],
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

function briefFixture(state: State) {
  return {
    brief: briefSummary(state.status),
    sources: [{
      id: sourceId, sourceType: 'SUPPLIED_TEXT', locator: 'supplied:web:1',
      title: 'December enquiry Brief supplied source', content: state.source,
      contentHash: 'a'.repeat(64), createdBy: userId, createdAtUtc: now,
    }],
    versions: [versionFixture(state)],
  }
}

function briefSummary(status: string) {
  return {
    id: briefId, tenantId, clientId, opportunityId: null, title: 'December enquiry Brief',
    ownerUserId: userId, status, currentDraftVersionId: status === 'CREATED' ? null : versionId,
    approvedVersionId: status === 'APPROVED' ? versionId : null,
    version: status === 'CREATED' ? 1 : stateVersion(status), updatedAtUtc: now,
  }
}

function versionFixture(state: State) {
  return {
    id: versionId, briefId, baseVersionId: null, sourceId, versionNumber: 1,
    businessProblem: 'Qualified demand is not established.', objective: 'Generate qualified enquiries.',
    audiences: ['Workspace furniture buyers'], geographies: ['Gauteng'], timing: 'By December 2026',
    budgetMinor: null, budgetUnknown: true, currency: null, vatStatus: null, feesMinor: null,
    constraints: [], measurement: [], facts: [], unknowns: [{
      fieldPath: 'budget', question: 'What budget is available?', isBlocking: false,
    }], assumptions: [], conflicts: [], evidenceItemIds: [], status: state.status,
    createdBy: userId, submittedBy: state.status === 'DRAFT' ? null : userId,
    approvedBy: state.status === 'APPROVED' ? userId : null, rejectedBy: null,
    rejectionReason: null, requestedChanges: null, version: state.version, createdAtUtc: now,
  }
}

function stateVersion(status: string) { return status === 'APPROVED' ? 4 : 2 }
function sessionFixture() { return { authenticated: true, antiforgeryToken: 'csrf-brief', expiresAtUtc: '2026-08-29T20:00:00Z' } }
function workspaceFixture(role: string) { return { membershipId: 'b7000000-0000-0000-0000-000000000001', tenantId, name: 'Solo Agency', slug: 'solo-agency', roleCode: role, version: 1 } }
function userFixture() { return { id: userId, email: 'solo@example.com', displayName: 'Solo Operator', phone: null, mfaEnabled: true, version: 1 } }
function clientFixture() { return { id: clientId, tenantId, externalReference: 'solo-client', legalName: 'Client One', tradingName: 'Client One', website: null, industry: null, billingProfileJson: '{}', primaryContactId: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now } }

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
  await route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(body) })
}
