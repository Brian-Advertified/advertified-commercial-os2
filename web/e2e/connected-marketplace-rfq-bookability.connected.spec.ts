import { expect, test, type Locator, type Page } from '@playwright/test'

const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const supplierUserId = '10000000-0000-0000-0000-000000000041'

type Session = { antiforgeryToken: string }
type ProposalSummary = { id: string; title: string; status: string; createdAtUtc: string }
type ProposalLine = {
  marketplaceListingVersionId: string | null
  name: string
  quantity: number
  clientPriceMinor: number
  feesMinor: number
  vatMinor: number
  currency?: string
  runningPeriods: Array<{ start: string; end: string }>
}
type ProposalRecord = {
  id: string
  status: string
  decision: { optionId: string | null } | null
  options: Array<{ id: string; currency: string; inventory: ProposalLine[] }>
}
type Rfq = {
  id: string
  listingVersionId: string
  subject: string
  requestedStart: string
  requestedEnd: string
  quantity: number
  status: string
  response: null | { id: string; amountMinor: number; currency: string; responseVersion: number }
}

const subjects = new Map<string, string>()

test('buyer and supplier complete exact RFQs for every selected Marketplace line', async ({ page }) => {
  test.setTimeout(120_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)

  const proposal = await selectedProposal(page)
  const option = proposal.options.find(item => item.id === proposal.decision?.optionId)
  expect(option).toBeTruthy()
  const lines = option!.inventory
  expect(lines.length).toBeGreaterThan(0)
  expect(lines.every(line => Boolean(line.marketplaceListingVersionId))).toBe(true)

  for (const line of lines) {
    const existing = await acceptedRfq(page, line)
    if (existing) {
      subjects.set(line.marketplaceListingVersionId!, existing.subject)
      continue
    }
    const subject = `Connected booking quote · ${line.name} · ${proposal.id.slice(0, 8)}`
    subjects.set(line.marketplaceListingVersionId!, subject)
    await createAndSendRfq(page, line, subject)
  }

  await switchIdentity(page, supplierUserId)
  await chooseWorkspace(page, supplierTenantId)
  await page.goto('/marketplace')
  await page.getByRole('button', { name: /^Requests/ }).click()
  await expect(page.getByRole('heading', { name: 'Requests', exact: true })).toBeVisible()

  for (const line of lines) {
    if (await acceptedRfqAsSupplier(page, line)) continue
    const subject = subjects.get(line.marketplaceListingVersionId!)!
    await selectRequest(page, subject)
    const prepare = page.getByRole('button', { name: 'Prepare response', exact: true })
    await expect(prepare).toBeVisible()
    await prepare.click()
    await expect(page.getByRole('heading', { name: subject, exact: true })).toBeVisible()
    const supplierCostMinor = line.clientPriceMinor - line.feesMinor - line.vatMinor
    await page.getByLabel('Amount').fill((supplierCostMinor / 100).toFixed(2))
    await page.getByLabel('Currency').selectOption('ZAR')
    await page.getByLabel('Availability').selectOption('AVAILABLE')
    await page.getByLabel('Valid until').fill('2026-11-01T12:00')
    await page.getByLabel('Terms').fill('Exact supplier quote for the selected Marketplace line and retained campaign flight.')
    await page.getByLabel('Evidence references, one per line').fill(`connected-supplier-confirmation:${line.marketplaceListingVersionId}`)
    const submit = page.getByRole('button', { name: 'Submit immutable response', exact: true })
    await mutate(page, submit, /\/marketplace-rfqs\/[^/]+\/responses$/, [200])
  }

  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)
  await page.goto('/marketplace')
  await page.getByRole('button', { name: /^Requests/ }).click()
  await expect(page.getByRole('heading', { name: 'Requests', exact: true })).toBeVisible()

  for (const line of lines) {
    const accepted = await acceptedRfq(page, line)
    if (accepted) continue
    const subject = subjects.get(line.marketplaceListingVersionId!)!
    await selectRequest(page, subject)
    const accept = page.getByRole('button', { name: 'Accept exact response', exact: true })
    await expect(accept).toBeVisible()
    await mutate(page, accept, /\/marketplace-responses\/[^/]+:accept$/, [200])
  }

  const finalRfqs = await listRfqs(page, buyerTenantId)
  for (const line of lines) {
    const supplierCostMinor = line.clientPriceMinor - line.feesMinor - line.vatMinor
    const match = finalRfqs.find(item =>
      item.listingVersionId === line.marketplaceListingVersionId &&
      item.requestedStart === line.runningPeriods[0].start &&
      item.requestedEnd === line.runningPeriods.at(-1)!.end &&
      item.quantity === line.quantity && item.status === 'ACCEPTED')
    expect(match, `Accepted exact RFQ missing for ${line.name}`).toBeTruthy()
    expect(match!.response?.amountMinor).toBe(supplierCostMinor)
    expect(match!.response?.currency).toBe(option!.currency)
  }
})

async function createAndSendRfq(page: Page, line: ProposalLine, subject: string) {
  await page.goto('/marketplace')
  await page.getByLabel('Product or supplier').fill(line.name)
  const search = page.getByRole('button', { name: 'Search marketplace', exact: true })
  await search.click()
  await expect(page.getByText(line.name, { exact: true }).first()).toBeVisible()
  await page.getByRole('button', { name: `Open ${line.name}`, exact: true }).click()
  await expect(page.getByRole('heading', { name: line.name, exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Request availability', exact: true }).click()
  await page.getByLabel('Request subject').fill(subject)
  await page.getByLabel('Start date').fill(line.runningPeriods[0].start)
  await page.getByLabel('End date').fill(line.runningPeriods.at(-1)!.end)
  await page.getByLabel('Quantity').fill(String(line.quantity))
  await page.getByLabel('Supplier response due').fill('2026-09-20T12:00')
  const create = page.getByRole('button', { name: 'Create draft request', exact: true })
  await mutate(page, create, /\/marketplace-rfqs$/, [200, 201])
  await expect(page.getByRole('heading', { name: 'Requests', exact: true })).toBeVisible()
  await selectRequest(page, subject)
  const send = page.getByRole('button', { name: 'Send to supplier', exact: true })
  await expect(send).toBeVisible()
  await mutate(page, send, /\/marketplace-rfqs\/[^/]+:send$/, [200])
}

async function selectRequest(page: Page, subject: string) {
  const row = page.getByRole('button').filter({ hasText: subject }).first()
  await expect(row).toBeVisible()
  await row.click()
  await expect(page.getByRole('heading', { name: subject, exact: true })).toBeVisible()
}

async function mutate(page: Page, action: Locator, route: RegExp, statuses: number[]) {
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && route.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await action.click()
  const response = await responsePromise
  expect(statuses, await response.text()).toContain(response.status())
}

async function selectedProposal(page: Page): Promise<ProposalRecord> {
  const list = await page.request.get(`/api/v1/tenants/${buyerTenantId}/proposals`)
  expect(list.ok(), await list.text()).toBe(true)
  const rows = await list.json() as ProposalSummary[]
  const selected = rows
    .filter(item => /Connected Marketplace OOH Proposal/i.test(item.title) && item.status === 'SELECTED')
    .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))[0]
  expect(selected, 'Expected selected Marketplace proposal').toBeTruthy()
  const response = await page.request.get(`/api/v1/tenants/${buyerTenantId}/proposals/${selected.id}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as ProposalRecord
}

async function listRfqs(page: Page, tenantId: string): Promise<Rfq[]> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/marketplace-rfqs?pageSize=50`)
  expect(response.ok(), await response.text()).toBe(true)
  return (await response.json() as { items: Rfq[] }).items
}

async function acceptedRfq(page: Page, line: ProposalLine) {
  const rows = await listRfqs(page, buyerTenantId)
  return rows.find(item => item.listingVersionId === line.marketplaceListingVersionId &&
    item.requestedStart === line.runningPeriods[0].start &&
    item.requestedEnd === line.runningPeriods.at(-1)!.end &&
    item.quantity === line.quantity && item.status === 'ACCEPTED') ?? null
}

async function acceptedRfqAsSupplier(page: Page, line: ProposalLine) {
  const rows = await listRfqs(page, supplierTenantId)
  return rows.find(item => item.listingVersionId === line.marketplaceListingVersionId &&
    item.requestedStart === line.runningPeriods[0].start &&
    item.requestedEnd === line.runningPeriods.at(-1)!.end &&
    item.quantity === line.quantity && item.status === 'ACCEPTED') ?? null
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
