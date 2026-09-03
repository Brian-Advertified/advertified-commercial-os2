import { expect, test, type Route } from '@playwright/test'

const tenantId = 'ab100000-0000-0000-0000-000000000001'
const now = '2026-09-03T09:00:00Z'

const agents = [
  ['business_interpretation', 'Business Interpretation Agent'],
  ['opportunity_intelligence', 'Opportunity Intelligence Agent'],
  ['strategy', 'Strategy Agent'],
  ['critic_readiness', 'Critic and Readiness Agent'],
  ['brief_drafting', 'Brief Drafting Agent'],
  ['audience', 'Audience Agent'],
  ['inventory_intelligence', 'Inventory Intelligence Agent'],
  ['media_planning', 'Media Planning Agent'],
  ['proposal_narrative', 'Proposal Narrative Agent'],
  ['creative', 'Creative Intelligence Agent'],
  ['measurement', 'Measurement Agent'],
] as const

test('agency administrator can find agent budgets and recorded costs in settings', async ({ page }) => {
  await page.addInitScript((selectedTenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: selectedTenantId }))
  }, tenantId)
  await page.route('**/api/v1/**', handleApi)

  await page.goto('/admin/commercial')
  const agentOperations = page.getByRole('link', { name: 'Agent operations', exact: true })
  await expect(agentOperations).toBeVisible()
  await agentOperations.click()

  await expect(page).toHaveURL(/\/admin\/agents$/)
  await expect(page.getByRole('heading', { name: 'Agent operations', exact: true })).toBeVisible()
  await expect(page.getByText('Paid AI disabled', { exact: true })).toBeVisible()
  await expect(page.getByText('Local deterministic agents do not call a paid provider.')).toBeVisible()
  await expect(page.getByText('any retained historical usage remains visible below.')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Agent budgets and costs' })).toBeVisible()
  await expect(page.getByRole('row', { name: /Business Interpretation Agent/ }))
    .toContainText('$0.00')
  const usage = page.getByRole('region', { name: 'Recent recorded usage' })
  await expect(usage.getByRole('row').filter({ hasText: 'Business Interpretation' })
    .filter({ hasText: 'Failed' })).toContainText('$0.00')
  const runs = page.getByRole('region', { name: 'Recent durable runs' })
  await expect(runs.getByRole('row').filter({ hasText: 'Review Required' }))
    .toContainText('$0.00')
})

async function handleApi(route: Route) {
  const path = new URL(route.request().url()).pathname
  if (path === '/api/v1/session') return json(route, 200, sessionFixture())
  if (path === '/api/v1/workspaces') return json(route, 200, [workspaceFixture()])
  if (path === `/api/v1/tenants/${tenantId}/commercial-policy`) return json(route, 200, null)
  if (path === `/api/v1/tenants/${tenantId}/agent-operations`) {
    return json(route, 200, agentOperationsFixture())
  }
  return json(route, 404, safeProblem())
}

function agentOperationsFixture() {
  return {
    currency: 'USD', provider: 'deterministic', liveProviderEnabled: false,
    totalIncrementalCostMinor: 0, durableRunCount: 1, attentionRunCount: 1,
    agents: agents.map(([agentCode, displayLabel], index) => ({
      agentCode, displayLabel, provider: 'deterministic', model: 'fixture-v1',
      costCapMinor: 0, usageCount: index === 0 ? 1 : 0,
      incrementalCostMinor: 0, lastUsedAtUtc: index === 0 ? now : null,
    })),
    recentUsage: [{
      id: 'ab200000-0000-0000-0000-000000000001',
      agentCode: 'business_interpretation', workType: 'INTERPRETATION', status: 'FAILED',
      provider: 'deterministic', model: 'fixture-v1', units: 0, toolCalls: 0,
      incrementalCostMinor: 0, recordedAtUtc: now,
    }],
    recentRuns: [{
      id: 'ab300000-0000-0000-0000-000000000001',
      opportunityId: 'ab400000-0000-0000-0000-000000000001', campaignId: null,
      runKind: 'INTERPRETATION', status: 'REVIEW_REQUIRED', currentStep: 'INTERPRETATION',
      attempts: 1, errorCode: 'AGENT_OUTPUT_INVALID', incrementalCostMinor: 0,
      updatedAtUtc: now,
    }],
  }
}

function workspaceFixture() {
  return {
    membershipId: 'ab500000-0000-0000-0000-000000000001', tenantId,
    name: 'Advertified Local Development', slug: 'advertified-local-development',
    roleCode: 'agency_admin', version: 1,
  }
}

function sessionFixture() {
  return {
    authenticated: true, antiforgeryToken: 'csrf-agent-operations',
    expiresAtUtc: '2026-09-03T17:00:00Z', signInPath: null, signOutPath: null,
  }
}

function safeProblem() {
  return {
    status: 404, title: 'Not found', code: 'NOT_FOUND',
    correlationId: 'ab600000-0000-0000-0000-000000000001',
  }
}

async function json(route: Route, status: number, body: unknown) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}
