import { expect, type Page } from '@playwright/test'
import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { planningWorkspaceSchema, type PlanningWorkspace } from '../../src/api/planning-schemas'
import { proposalSchema, proposalSummariesSchema, type Proposal } from '../../src/api/proposal-schemas'
import { canaryScope } from './inventory-canary-intake'

const root = fileURLToPath(new URL('../../../', import.meta.url))
const prefix = `/api/v1/tenants/${canaryScope.tenantId}`

async function readJson(page: Page, route: string) {
  const response = await page.request.get(route)
  expect(response.status(), `Read persisted result: ${route}`).toBe(200)
  return response.json()
}

export async function inspectSavedProposal(page: Page, briefVersionId: string) {
  const workspace = planningWorkspaceSchema.parse(await readJson(page,
    `${prefix}/brief-versions/${briefVersionId}/planning`))
  const proposals = proposalSummariesSchema.parse(await readJson(page, `${prefix}/proposals`))
  const saved = proposals.filter(item => item.briefId === workspace.briefId)
    .sort((left, right) => right.versionNumber - left.versionNumber)[0]
  expect(saved, 'The previous receipt must correspond to a persisted proposal.').toBeDefined()
  const proposal = proposalSchema.parse(await readJson(page, `${prefix}/proposals/${saved.id}`))
  const report = await retainResult(page, workspace, proposal, 'read-only')
  console.log(JSON.stringify(report))
}

export async function verifyNewProposal(page: Page, generatedIds: string[], resumed: boolean) {
  await expect(page).toHaveURL(/\/proposals\/[0-9a-f-]{36}$/i)
  const id = new URL(page.url()).pathname.split('/').at(-1)!
  expect(generatedIds, 'A proposal-generation response, not an index page, is required.').toContain(id)
  const proposal = proposalSchema.parse(await readJson(page, `${prefix}/proposals/${id}`))
  const workspace = planningWorkspaceSchema.parse(await readJson(page,
    `${prefix}/brief-versions/${proposal.briefVersionId}/planning`))
  const report = await retainResult(page, workspace, proposal, resumed ? 'resumed' : 'fresh')
  console.log(JSON.stringify(report))
  const failed = Object.entries(report.checks).filter(([, passed]) => !passed).map(([name]) => name)
  expect(failed, `Persisted business requirements failed: ${failed.join(', ')}`).toEqual([])
}

async function retainResult(page: Page, workspace: PlanningWorkspace, proposal: Proposal,
  kind: 'read-only' | 'resumed' | 'fresh') {
  await page.goto(`/proposals/${proposal.id}`)
  await expect(page.getByRole('heading', { name: /proposal/i }).first()).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { name: /proposal/i }).first()).toBeVisible()
  const body = await page.locator('body').innerText()
  expect(body).not.toMatch(/deterministic-zero-cost|internal gate/i)
  const plan = workspace.mediaPlan
  const checks = { ...approvalChecks(workspace, proposal), ...supplyChecks(workspace, proposal) }
  const report = {
    schemaVersion: 'advertified.connected-proposal-proof.v1', verifiedAtUtc: new Date().toISOString(), kind,
    tenantId: canaryScope.tenantId, briefId: proposal.briefId, briefVersionId: proposal.briefVersionId,
    proposalId: proposal.id, planId: plan?.id, checks,
    sourceHashes: sourceHashes(),
    options: proposal.options.map(option => ({ id: option.id, planId: option.planVersionId,
      amountMinor: option.budgetMinor, currency: option.currency, inventory: option.inventory.map(line => ({
        productId: line.inventoryProductId, productVersionId: line.productVersionId, rateId: line.rateId,
        listingVersionId: line.marketplaceListingVersionId, name: line.name, channel: line.channel,
        geography: line.geography, clientPriceMinor: line.clientPriceMinor, vatMinor: line.vatMinor,
        runningPeriods: line.runningPeriods, supplyConfidence: line.supplyConfidence, uncertainties: line.uncertainties,
      })) })),
    reviewDispositions: plan?.objections.map(item => ({ code: item.code, resolution: item.resolution,
      reason: item.resolutionReason, evidenceGap: item.evidenceGap })),
    limitation: 'Internal draft only. City coverage does not prove preferred-mall proximity, audience reach or supplier confirmation. No proposal release, booking or payment was performed.',
  }
  const destination = path.join(root, 'artifacts/production-readiness/preview')
  mkdirSync(destination, { recursive: true })
  writeFileSync(path.join(destination, `proposal-proof-${proposal.id}.json`), JSON.stringify(report, null, 2) + '\n')
  return report
}

function approvalChecks(workspace: PlanningWorkspace, proposal: Proposal) {
  return {
    sameBrief: proposal.briefVersionId === workspace.briefVersionId && proposal.briefId === workspace.briefId,
    oohModeLocked: workspace.campaignMode?.mode === 'OOH_ONLY' && workspace.campaignMode.isLocked,
    audienceApproved: workspace.audience?.status === 'APPROVED',
    mixApproved: workspace.mediaMix?.status === 'APPROVED',
    shortlistApproved: workspace.shortlist?.status === 'APPROVED',
    planApproved: workspace.mediaPlan?.status === 'APPROVED',
    proposalUsesApprovedPlan: proposal.options.every(option => option.planVersionId === workspace.mediaPlan?.id),
    draftWithoutExternalDecision: proposal.status === 'DRAFT' && proposal.decision === null && proposal.approvedBy === null,
  }
}

function supplyChecks(workspace: PlanningWorkspace, proposal: Proposal) {
  const inventory = proposal.options.flatMap(option => option.inventory)
  return {
    positiveInventory: inventory.length > 0,
    digitalOnly: inventory.length > 0 && inventory.every(line => line.channel === 'DOOH'),
    requiredCities: canaryScope.cities.every(city => inventory.some(line =>
      line.geography.toLowerCase() === city.toLowerCase() || line.geography.toLowerCase() === city.toLowerCase() + ' cbd')),
    exactFlights: inventory.length > 0 && inventory.every(line => line.runningPeriods.length > 0
      && line.runningPeriods.every(period => period.start === canaryScope.start && period.end === canaryScope.end)),
    exactInventoryLineage: inventory.every(line => workspace.mediaPlan?.lines.some(item =>
      item.inventoryTenantId === line.inventoryTenantId && item.productVersionId === line.productVersionId
      && item.rateId === line.rateId && item.clientPriceMinor === line.clientPriceMinor)),
    eligibleSelection: inventory.every(line => workspace.shortlist?.candidates.some(candidate =>
      candidate.productVersionId === line.productVersionId && candidate.isEligible && candidate.isSelected)),
    totalsReconcile: proposal.options.every(option => option.budgetMinor === option.inventory.reduce(
      (sum, line) => sum + line.clientPriceMinor, 0)),
    withinWorkingBudgetExcludingVat: proposal.options.every(option => option.currency === 'ZAR'
      && option.inventory.reduce((sum, line) => sum + line.clientPriceMinor - line.vatMinor, 0) <= canaryScope.budgetMinor),
  }
}

function sourceHashes() {
  return Object.fromEntries([
    'web/e2e/inventory-brief-to-proposal.connected.spec.ts',
    'web/e2e/support/inventory-canary-intake.ts', 'web/e2e/support/inventory-canary-evidence.ts',
    'web/e2e/support/inventory-canary-review.ts',
  ].map(file => [file, createHash('sha256').update(readFileSync(path.join(root, file))).digest('hex')]))
}
