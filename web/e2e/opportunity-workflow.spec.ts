import { expect, test, type Route } from '@playwright/test'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const opportunityId = '91000000-0000-0000-0000-000000000001'
const ownerId = '92000000-0000-0000-0000-000000000001'
const reviewerId = '92000000-0000-0000-0000-000000000002'
const approverId = '92000000-0000-0000-0000-000000000003'
const evidenceSetId = '93000000-0000-0000-0000-000000000001'
const interpretationId = '94000000-0000-0000-0000-000000000001'
const angleId = '95000000-0000-0000-0000-000000000001'
const secondAngleId = '95000000-0000-0000-0000-000000000002'
const strategyId = '96000000-0000-0000-0000-000000000001'
const objectionId = '97000000-0000-0000-0000-000000000001'
const now = '2026-08-29T16:00:00Z'

type Phase = 'evidence' | 'interpret' | 'confirm' | 'angleReady' | 'angles' | 'selected' | 'critic' | 'resolved' | 'review' | 'complete'
type Role = 'owner' | 'reviewer' | 'approver'
type State = { phase: Phase; role: Role }

test('three human roles carry evidence-bound strategy to Brief ready', async ({ page }) => {
  const state: State = { phase: 'evidence', role: 'reviewer' }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.exposeFunction('switchOpportunityRole', (role: Role) => { state.role = role })
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto(`/opportunities/${opportunityId}`)
  await expect(page.getByRole('heading', { name: 'Evidence-led workspace growth' })).toBeVisible()
  await page.getByRole('button', { name: 'Approve assigned evidence set' }).click()
  await page.evaluate(() => window.switchOpportunityRole('owner'))
  await page.getByRole('button', { name: 'Interpret approved evidence' }).click()
  await expect(page.getByRole('button', { name: 'Confirm interpretation' })).toBeVisible()
  await page.getByRole('button', { name: 'Confirm interpretation' }).click()
  await page.getByRole('button', { name: 'Generate opportunity angles' }).click()
  await expect(page.getByRole('button', { name: 'Select angle 1' })).toBeVisible()
  await page.getByRole('button', { name: 'Select angle 1' }).click()
  await page.getByLabel('Strategy approver user ID').fill(approverId)
  await page.getByRole('button', { name: 'Generate strategy and critic' }).click()
  await expect(page.getByRole('button', { name: 'Resolve material objection' })).toBeVisible()
  await page.getByRole('button', { name: 'Resolve material objection' }).click()
  await page.getByRole('button', { name: 'Submit strategy' }).click()

  await page.getByRole('button', { name: 'Approve assigned strategy' }).click()
  await expect(page.getByRole('alert')).toContainText('assigned operator or reviewer')
  await page.evaluate(() => window.switchOpportunityRole('approver'))
  await page.getByRole('button', { name: 'Approve assigned strategy' }).click()
  await expect(page.getByText('Draft the campaign brief.')).toBeVisible()
  await expect(page.getByText('brief ready', { exact: true })).toBeVisible()
})

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (path === '/api/v1/session') return json(route, 200, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, 200, [workspaceFixture()])
  if (path === `/api/v1/tenants/${tenantId}/opportunities/${opportunityId}` && request.method() === 'GET') {
    return json(route, 200, detailFixture(state.phase))
  }
  if (request.method() !== 'POST') return json(route, 404, safeProblem('NOT_FOUND'))
  assertCommandHeaders(route)
  return handleCommand(route, state, commandAction(path))
}

const commandRoutes = [
  { suffix: `${evidenceSetId}:approve`, action: 'evidence' },
  { suffix: '/interpret', action: 'interpret' },
  { suffix: `${interpretationId}:confirm`, action: 'confirm' },
  { suffix: '/angles:generate', action: 'angles' },
  { suffix: `${angleId}:select`, action: 'select' },
  { suffix: '/strategies:generate', action: 'strategy' },
  { suffix: `${objectionId}:resolve`, action: 'resolve' },
  { suffix: `${strategyId}:submit`, action: 'submit' },
  { suffix: `${strategyId}:approve`, action: 'approve' },
] as const

function commandAction(path: string): string {
  return commandRoutes.find((item) => path.endsWith(item.suffix))?.action ?? 'unknown'
}

async function handleCommand(route: Route, state: State, action: string) {
  switch (action) {
    case 'evidence': return approveEvidence(route, state)
    case 'interpret': state.phase = 'confirm'; return json(route, 202, runFixture('INTERPRETATION'))
    case 'confirm': state.phase = 'angleReady'; return json(route, 200, interpretationFixture('APPROVED', 2))
    case 'angles': state.phase = 'angles'; return json(route, 202, runFixture('ANGLES'))
    case 'select': state.phase = 'selected'; return json(route, 200, angleFixture(angleId, 1, 'SELECTED'))
    case 'strategy': state.phase = 'critic'; return json(route, 202, runFixture('STRATEGY_CRITIC'))
    case 'resolve': state.phase = 'resolved'; return json(route, 200, objectionFixture(true))
    case 'submit': state.phase = 'review'; return json(route, 200, strategyFixture('IN_REVIEW', 2, true))
    case 'approve': return approveStrategy(route, state)
    default: return json(route, 404, safeProblem('NOT_FOUND'))
  }
}

async function approveEvidence(route: Route, state: State) {
  if (state.role !== 'reviewer') return json(route, 403, safeProblem('APPROVAL_REQUIRED'))
  state.phase = 'interpret'; return json(route, 200, evidenceSetFixture('APPROVED', 2))
}

async function approveStrategy(route: Route, state: State) {
  if (state.role !== 'approver') return json(route, 403, safeProblem('APPROVAL_REQUIRED'))
  state.phase = 'complete'; return json(route, 200, strategyFixture('APPROVED', 3, true))
}

function detailFixture(phase: Phase) {
  const strategyVisible = ['critic', 'resolved', 'review', 'complete'].includes(phase)
  const interpretationVisible = !['evidence', 'interpret'].includes(phase)
  const anglesVisible = ['angles', 'selected', 'critic', 'resolved', 'review', 'complete'].includes(phase)
  const evidence = evidenceSetByPhase[phase]
  const interpretation = interpretationByPhase[phase]
  const strategy = strategyByPhase[phase]
  return {
    opportunity: opportunityFixture(phase), sources: [sourceFixture()], evidenceItems: [evidenceFixture()],
    evidenceSet: evidenceSetFixture(evidence.status, evidence.version),
    interpretation: interpretationVisible
      ? interpretationFixture(interpretation.status, interpretation.version) : null,
    angles: anglesVisible ? anglesFixture(phase) : [],
    strategy: strategyVisible
      ? strategyFixture(strategy.status, strategy.version, strategy.resolved) : null,
    briefId: null, runs: [], nextAction: nextActionByPhase[phase],
  }
}

const evidenceSetByPhase: Record<Phase, { status: string; version: number }> = {
  evidence: { status: 'IN_REVIEW', version: 1 }, interpret: { status: 'APPROVED', version: 2 },
  confirm: { status: 'APPROVED', version: 2 }, angleReady: { status: 'APPROVED', version: 2 },
  angles: { status: 'APPROVED', version: 2 },
  selected: { status: 'APPROVED', version: 2 }, critic: { status: 'APPROVED', version: 2 },
  resolved: { status: 'APPROVED', version: 2 }, review: { status: 'APPROVED', version: 2 },
  complete: { status: 'APPROVED', version: 2 },
}
const interpretationByPhase: Record<Phase, { status: string; version: number }> = {
  evidence: { status: 'DRAFT', version: 1 }, interpret: { status: 'DRAFT', version: 1 },
  confirm: { status: 'DRAFT', version: 1 }, angleReady: { status: 'APPROVED', version: 2 },
  angles: { status: 'APPROVED', version: 2 },
  selected: { status: 'APPROVED', version: 2 }, critic: { status: 'APPROVED', version: 2 },
  resolved: { status: 'APPROVED', version: 2 }, review: { status: 'APPROVED', version: 2 },
  complete: { status: 'APPROVED', version: 2 },
}
const strategyByPhase: Record<Phase, { status: string; version: number; resolved: boolean }> = {
  evidence: { status: 'DRAFT', version: 1, resolved: false }, interpret: { status: 'DRAFT', version: 1, resolved: false },
  confirm: { status: 'DRAFT', version: 1, resolved: false }, angleReady: { status: 'DRAFT', version: 1, resolved: false },
  angles: { status: 'DRAFT', version: 1, resolved: false },
  selected: { status: 'DRAFT', version: 1, resolved: false }, critic: { status: 'DRAFT', version: 1, resolved: false },
  resolved: { status: 'DRAFT', version: 1, resolved: true }, review: { status: 'IN_REVIEW', version: 2, resolved: true },
  complete: { status: 'APPROVED', version: 3, resolved: true },
}
const nextActionByPhase: Record<Phase, string> = {
  evidence: 'Complete the current governed action.', interpret: 'Complete the current governed action.',
  confirm: 'Complete the current governed action.', angleReady: 'Complete the current governed action.',
  angles: 'Complete the current governed action.',
  selected: 'Complete the current governed action.', critic: 'Complete the current governed action.',
  resolved: 'Complete the current governed action.', review: 'Complete the current governed action.',
  complete: 'Draft the campaign brief.',
}

function opportunityFixture(phase: Phase) {
  return {
    id: opportunityId, tenantId, clientId: '98000000-0000-0000-0000-000000000001',
    title: 'Evidence-led workspace growth', sourceType: 'DISCOVERY', sourceRef: 'local', ownerUserId: ownerId,
    stage: phase === 'evidence' ? 'EVIDENCE_REVIEW' : phase === 'complete' ? 'BRIEF_READY' : 'STRATEGY_READY',
    expectedValueMinor: null, currency: null, deadline: null,
    problemSummary: 'Demand is not documented.', objectiveSummary: 'Create qualified enquiries.',
    version: phase === 'complete' ? 5 : 4, updatedAtUtc: now,
  }
}

function sourceFixture() {
  return { id: '99000000-0000-0000-0000-000000000001', opportunityId, type: 'SUPPLIED_TEXT',
    locator: 'supplied:1', title: 'Supplied qualification', contentHash: 'a'.repeat(64),
    policyBasis: 'OWNER_SUPPLIED', captureStatus: 'COMPLETED', version: 1, capturedAtUtc: now }
}
function evidenceFixture() {
  return { id: '99100000-0000-0000-0000-000000000001', sourceId: '99000000-0000-0000-0000-000000000001',
    locator: 'supplied:1#claim', claimType: 'BUSINESS_CONTEXT', originalValueJson: '{}',
    reviewedValueJson: '{}', excerpt: 'Modular furniture for Gauteng teams.', confidence: 1,
    reviewStatus: 'APPROVED', decision: 'APPROVE', reviewReason: null,
    createdBy: ownerId, reviewedBy: reviewerId, version: 2 }
}
function evidenceSetFixture(status: string, version: number) {
  return { id: evidenceSetId, opportunityId, versionNumber: 1,
    evidenceItemIds: ['99100000-0000-0000-0000-000000000001'], gaps: [], status,
    createdBy: ownerId, approvedBy: status === 'APPROVED' ? reviewerId : null, version }
}
function interpretationFixture(status: string, version: number) {
  return { id: interpretationId, opportunityId, evidenceSetId, versionNumber: 1,
    artifactJson: '{"offering":"Modular furniture"}', evidenceBindingsJson: '[]',
    unknownsJson: '[]', assumptionsJson: '[]', status, createdBy: ownerId,
    confirmedBy: status === 'APPROVED' ? ownerId : null, version }
}
function anglesFixture(phase: Phase) {
  const selected = ['selected', 'critic', 'resolved', 'review', 'complete'].includes(phase)
  return [angleFixture(angleId, 1, selected ? 'SELECTED' : 'PROPOSED'),
    angleFixture(secondAngleId, 2, selected ? 'REJECTED' : 'PROPOSED')]
}
function angleFixture(id: string, rank: number, status: string) {
  return { id, angleSetId: '99200000-0000-0000-0000-000000000001', rank,
    title: rank === 1 ? 'Verified discovery' : 'Qualified enquiry', rationale: 'Bound to evidence.',
    evidenceItemIdsJson: '["99100000-0000-0000-0000-000000000001"]', confidence: 0.8,
    status, selectedBy: status === 'SELECTED' ? ownerId : null, version: status === 'PROPOSED' ? 1 : 2 }
}
function strategyFixture(status: string, version: number, resolved: boolean) {
  return { id: strategyId, opportunityId, versionNumber: 1,
    artifactJson: '{"diagnosis":"Evidence-led demand path"}', evidenceBindingsJson: '[]',
    unknownsJson: '[]', assumptionsJson: '[]', status, createdBy: ownerId,
    submittedBy: status === 'DRAFT' ? null : ownerId, approvedBy: status === 'APPROVED' ? approverId : null,
    rejectedBy: null, rejectionReason: null,
    version, objections: [objectionFixture(resolved)] }
}
function objectionFixture(resolved: boolean) {
  return { id: objectionId, severity: 'MATERIAL', fieldPath: 'artifact.objectives',
    evidenceGap: 'Baseline is unknown.', recommendedResolution: 'Keep it explicit.',
    resolution: resolved ? 'ADDRESSED' : null, resolutionReason: resolved ? 'Recorded constraint.' : null,
    resolvedBy: resolved ? ownerId : null, version: resolved ? 2 : 1 }
}
function runFixture(kind: string) {
  return { id: crypto.randomUUID(), opportunityId, runKind: kind, status: 'QUEUED', currentStep: null,
    attempts: 0, errorCode: null, recoveryAction: null, incrementalCostMinor: 0, version: 1, updatedAtUtc: now }
}
function sessionFixture() { return { authenticated: true, antiforgeryToken: 'csrf-opportunity', expiresAtUtc: '2026-08-29T20:00:00Z' } }
function workspaceFixture() { return { membershipId: '99300000-0000-0000-0000-000000000001', tenantId, name: 'Northstar Agency', slug: 'northstar', roleCode: 'platform_admin', version: 1 } }
function safeProblem(code: string) { return { title: 'Denied', status: 403, code, correlationId: crypto.randomUUID() } }
function assertCommandHeaders(route: Route) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-opportunity'); expect(headers['idempotency-key']).toBeTruthy()
}
async function json(route: Route, status: number, body: unknown) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}

declare global { interface Window { switchOpportunityRole: (role: Role) => Promise<void> } }
