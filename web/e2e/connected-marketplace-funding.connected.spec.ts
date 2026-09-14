import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const financeUserId = '10000000-0000-0000-0000-000000000042'
const financeReviewerUserId = '10000000-0000-0000-0000-000000000050'
const pdf = { name: 'connected-funding.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\n%connected-funding') }

type Session = { antiforgeryToken: string }
type ProposalSummary = { id: string; title: string; status: string; createdAtUtc: string }
type Proposal = {
  id: string
  status: string
  options: Array<{ id: string; budgetMinor: number; currency: string }>
  decision: null | { optionId: string | null }
}
type Order = {
  id: string
  proposalVersionId: string
  proposalOptionId: string
  purchaseOrderNumber: string
  status: string
  version: number
}
type Funding = {
  purchaseOrders: Order[]
  invoices: Array<{ id: string; purchaseOrderId: string; invoiceNumber: string }>
  payments: Array<{ id: string; purchaseOrderId: string; status: string }>
}

test('selected Marketplace proposal reaches confirmed funding with independent finance review', async ({ page }) => {
  test.setTimeout(120_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const proposal = await selectedProposal(page)
  const option = proposal.options.find(item => item.id === proposal.decision?.optionId)
  expect(option).toBeTruthy()
  const poNumber = `PO-CONNECTED-${proposal.id.slice(0, 8).toUpperCase()}`
  const fundingUrl = `/funding?${new URLSearchParams({
    proposalVersionId: proposal.id,
    proposalOptionId: option!.id,
    amountMinor: String(option!.budgetMinor),
    currency: option!.currency,
  })}`

  let funding = await fundingState(page)
  let order = funding.purchaseOrders.find(item =>
    item.proposalVersionId === proposal.id && item.proposalOptionId === option!.id) ?? null
  if (!order) {
    await page.goto(fundingUrl)
    await expect(page.getByRole('heading', { name: 'Finance', exact: true })).toBeVisible()
    await page.getByLabel('Purchase order number').fill(poNumber)
    await page.getByLabel(/Purchase order amount/).fill((option!.budgetMinor / 100).toFixed(2))
    await page.getByLabel('Signed purchase order').setInputFiles(pdf)
    await mutate(page, page.getByRole('button', { name: 'Submit for review', exact: true }), /\/purchase-orders$/, [200, 201])
    funding = await fundingState(page)
    order = funding.purchaseOrders.find(item =>
      item.proposalVersionId === proposal.id && item.proposalOptionId === option!.id) ?? null
    expect(order).toBeTruthy()
  }

  await switchIdentity(page, financeUserId)
  await chooseWorkspace(page, tenantId)
  await page.goto(fundingUrl)
  await expect(page.getByRole('heading', { name: 'Finance', exact: true })).toBeVisible()

  funding = await fundingState(page)
  order = funding.purchaseOrders.find(item =>
    item.proposalVersionId === proposal.id && item.proposalOptionId === option!.id)!
  const card = fundingCard(page, order.purchaseOrderNumber)

  if (order.status === 'SUBMITTED') {
    await card.getByLabel('Reconciliation reason').fill('The signed PO amount and selected proposal option were independently reconciled.')
    await mutate(page, card.getByRole('button', { name: 'Approve reconciled PO', exact: true }), /\/purchase-orders\/[^/]+:approve$/, [200])
  }

  funding = await fundingState(page)
  order = funding.purchaseOrders.find(item => item.id === order!.id)!
  let invoice = funding.invoices.find(item => item.purchaseOrderId === order.id) ?? null
  if (!invoice) {
    await expect(card.getByLabel('Invoice number')).toBeVisible()
    await card.getByLabel('Invoice number').fill(`INV-CONNECTED-${proposal.id.slice(0, 8).toUpperCase()}`)
    await mutate(page, card.getByRole('button', { name: 'Issue invoice', exact: true }), /\/invoices:issue$/, [200, 201])
    funding = await fundingState(page)
    invoice = funding.invoices.find(item => item.purchaseOrderId === order!.id) ?? null
    expect(invoice).toBeTruthy()
  }

  let payment = funding.payments.find(item => item.purchaseOrderId === order.id) ?? null
  if (!payment) {
    await mutate(page, card.getByRole('button', { name: 'Start payment record', exact: true }), /\/payment-intents$/, [200, 201])
    funding = await fundingState(page)
    payment = funding.payments.find(item => item.purchaseOrderId === order!.id) ?? null
    expect(payment).toBeTruthy()
  }

  if (payment!.status !== 'CONFIRMED') {
    await switchIdentity(page, financeReviewerUserId)
    await chooseWorkspace(page, tenantId)
    await page.goto(fundingUrl)
    await expect(page.getByRole('heading', { name: 'Finance', exact: true })).toBeVisible()
    const reviewerCard = fundingCard(page, order.purchaseOrderNumber)
    await expect(reviewerCard.getByLabel('Bank reference')).toBeVisible()
    await reviewerCard.getByLabel('Bank reference').fill(`BANK-${proposal.id.slice(0, 8)}`)
    await reviewerCard.getByLabel('Receipt evidence').setInputFiles(pdf)
    await reviewerCard.getByLabel('Reconciliation reason').fill('Receipt evidence matches the selected option, approved PO and issued invoice.')
    await mutate(page, reviewerCard.getByRole('button', { name: 'Confirm reconciled payment', exact: true }), /\/payment-intents\/[^/]+:reconcile$/, [200])
  }

  funding = await fundingState(page)
  order = funding.purchaseOrders.find(item => item.id === order!.id)!
  payment = funding.payments.find(item => item.purchaseOrderId === order.id) ?? null
  expect(order.status).toBe('APPROVED')
  expect(payment?.status).toBe('CONFIRMED')
  await expect(fundingCard(page, order.purchaseOrderNumber).getByText('Funding confirmed', { exact: true })).toBeVisible()
})

function fundingCard(page: Page, poNumber: string) {
  return page.locator('article.funding-record-card').filter({ has: page.getByRole('heading', { name: poNumber, exact: true }) })
}

async function fundingState(page: Page): Promise<Funding> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/funding`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Funding
}

async function selectedProposal(page: Page): Promise<Proposal> {
  const list = await page.request.get(`/api/v1/tenants/${tenantId}/proposals`)
  expect(list.ok(), await list.text()).toBe(true)
  const rows = await list.json() as ProposalSummary[]
  const selected = rows
    .filter(item => /Connected Marketplace OOH Proposal/i.test(item.title) && item.status === 'SELECTED')
    .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))[0]
  expect(selected, 'Expected selected Marketplace proposal').toBeTruthy()
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/proposals/${selected.id}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Proposal
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
