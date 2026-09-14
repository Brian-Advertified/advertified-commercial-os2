import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const clientUserId = '10000000-0000-0000-0000-000000000004'
const csv = {
  name: 'connected-performance.csv',
  mimeType: 'text/csv',
  buffer: Buffer.from('metric,value\nqualified_enquiries,42\n'),
}

type Session = { antiforgeryToken: string }
type ProposalSummary = { id: string; title: string; status: string; createdAtUtc: string }
type Evidence = { id: string; sourceReference: string; status: string; reviewedBy: string | null }
type Report = { id: string; status: string; reviewedBy: string | null }
type Campaign = {
  id: string
  proposalVersionId: string
  status: string
  performanceEvidence: Evidence[]
  measurementReports: Report[]
  deliveryProofs: Array<{ status: string }>
}

const sourceReference = 'connected-certification:qualified-enquiries'

test('completed Marketplace campaign reaches approved measurement report and learning through independent review', async ({ page }) => {
  test.setTimeout(150_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const proposalId = await selectedProposalId(page)
  let campaign = await campaignForProposal(page, proposalId)
  expect(campaign.status).toBe('COMPLETED')
  expect(campaign.deliveryProofs.some(item => item.status === 'APPROVED')).toBe(true)

  let evidence = campaign.performanceEvidence.find(item => item.sourceReference === sourceReference) ?? null
  if (!evidence) {
    await page.goto(`/campaigns/${campaign.id}#measurement-stage`)
    await openGovernance(page)
    await page.getByLabel('Source reference').fill(sourceReference)
    await page.getByLabel('Captured at').fill('2026-11-01T10:00')
    await page.getByLabel('Evidence quality').selectOption('VERIFIED')
    await page.getByLabel('Assigned reviewer').selectOption(clientUserId)
    await page.getByLabel('Methodology').fill('Count retained qualified enquiry records captured during the exact booked campaign flight.')
    await page.getByLabel('Limitations — one per line').fill('The retained source does not establish causal attribution.\nThe metric is limited to the recorded platform enquiry source.')
    await page.getByRole('combobox', { name: 'Metric' }).selectOption('CONVERSIONS')
    await page.getByLabel('Value', { exact: true }).fill('42')
    await page.getByRole('combobox', { name: 'Unit' }).selectOption('COUNT')
    await page.getByLabel('Metric source locator').fill('connected-certification:qualified-enquiries:count')
    await page.getByLabel('Evidence file').setInputFiles(csv)
    await mutate(page, page.getByRole('button', { name: 'Submit evidence for review', exact: true }), /\/performance-evidence$/, [200, 201])
    campaign = await getCampaign(page, campaign.id)
    evidence = campaign.performanceEvidence.find(item => item.sourceReference === sourceReference) ?? null
    expect(evidence).toBeTruthy()
  }

  if (evidence!.status !== 'APPROVED') {
    await switchIdentity(page, clientUserId)
    await chooseWorkspace(page, tenantId)
    await page.goto(`/campaigns/${campaign.id}#measurement-stage`)
    await openGovernance(page)
    const card = page.locator('article.performance-evidence-card').filter({ hasText: sourceReference })
    await expect(card).toBeVisible()
    await card.getByLabel('Evidence review reason').fill('The retained source, methodology, metric and explicit limitations are complete for this exact evidence set.')
    await mutate(page, card.getByRole('button', { name: 'Approve evidence', exact: true }), /\/performance-evidence\/[^/]+:review$/, [200])
    campaign = await getCampaign(page, campaign.id)
    evidence = campaign.performanceEvidence.find(item => item.id === evidence!.id)!
    expect(evidence.status).toBe('APPROVED')
  }

  let report = campaign.measurementReports.find(item => item.status === 'APPROVED')
    ?? campaign.measurementReports.find(item => !item.reviewedBy) ?? null
  if (!report) {
    await switchIdentity(page, buyerUserId)
    await chooseWorkspace(page, tenantId)
    await page.goto(`/campaigns/${campaign.id}#measurement-stage`)
    await openGovernance(page)
    await page.getByLabel('Assigned report reviewer').selectOption(clientUserId)
    await mutate(page, page.getByRole('button', { name: 'Generate sourced report', exact: true }), /\/measurement-reports:generate$/, [200, 201])
    campaign = await getCampaign(page, campaign.id)
    report = campaign.measurementReports.find(item => !item.reviewedBy)
      ?? campaign.measurementReports.find(item => item.status === 'APPROVED') ?? null
    expect(report).toBeTruthy()
  }

  if (report!.status !== 'APPROVED') {
    await switchIdentity(page, clientUserId)
    await chooseWorkspace(page, tenantId)
    await page.goto(`/campaigns/${campaign.id}#measurement-stage`)
    await openGovernance(page)
    const card = page.locator('article.measurement-report-card').filter({ has: page.getByRole('button', { name: 'Approve client report', exact: true }) })
    await expect(card).toBeVisible()
    await card.getByLabel('Report review reason').fill('The report retains the approved metric, source methodology and all stated limitations without unsupported causal claims.')
    await mutate(page, card.getByRole('button', { name: 'Approve client report', exact: true }), /\/measurement-reports\/[^/]+:review$/, [200])
    campaign = await getCampaign(page, campaign.id)
    report = campaign.measurementReports.find(item => item.id === report!.id)!
    expect(report.status).toBe('APPROVED')
  }

  await page.goto(`/campaigns/${campaign.id}#measurement-stage`)
  await page.getByRole('button', { name: 'Learning & insights', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'What the evidence says', exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Reusable learnings', exact: true })).toBeVisible()

  campaign = await getCampaign(page, campaign.id)
  expect(campaign.performanceEvidence.some(item => item.status === 'APPROVED')).toBe(true)
  expect(campaign.measurementReports.some(item => item.status === 'APPROVED')).toBe(true)
})

async function openGovernance(page: Page) {
  const details = page.locator('details.connected-measurement-governance')
  await expect(details).toBeVisible()
  if (!(await details.getAttribute('open'))) await details.locator('summary').click()
}

async function mutate(page: Page, action: Locator, route: RegExp, statuses: number[]) {
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && route.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await action.click()
  const response = await responsePromise
  expect(statuses, await response.text()).toContain(response.status())
  await page.waitForTimeout(150)
}

async function getCampaign(page: Page, campaignId: string): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns/${campaignId}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Campaign
}

async function campaignForProposal(page: Page, proposalId: string): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Campaign[]
  const campaign = rows.find(item => item.proposalVersionId === proposalId)
  expect(campaign, 'Expected funded selected proposal campaign').toBeTruthy()
  return await getCampaign(page, campaign!.id)
}

async function selectedProposalId(page: Page) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/proposals`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as ProposalSummary[]
  const selected = rows.filter(item => /Connected Marketplace OOH Proposal/i.test(item.title) && item.status === 'SELECTED')
    .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))[0]
  expect(selected).toBeTruthy()
  return selected.id
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
}

async function bootstrap(page: Page) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/bootstrap', {
    data: {}, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
}

async function switchIdentity(page: Page, userId: string) {
  const session = await sessionFor(page)
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId }, headers: mutationHeaders(session.antiforgeryToken),
  })
  expect(response.status(), await response.text()).toBe(200)
  await page.evaluate(() => sessionStorage.removeItem('advertified.workspace'))
}

async function sessionFor(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Session
}

async function chooseWorkspace(page: Page, id: string) {
  await page.evaluate((tenantId) => sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId })), id)
}

function mutationHeaders(token: string) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}
