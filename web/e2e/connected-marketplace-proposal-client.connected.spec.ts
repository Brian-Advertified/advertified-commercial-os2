import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const clientUserId = '10000000-0000-0000-0000-000000000004'

type Session = { antiforgeryToken: string }
type ProposalSummary = { id: string; title: string; status: string; createdAtUtc: string }
type ProposalRecord = {
  status: string
  document?: unknown | null
  recipientUserId?: string | null
  decision?: { optionId?: string | null } | null
  branding?: { status?: string }
}

test('Marketplace proposal is approved, shared and selected by the client', async ({ page }) => {
  test.setTimeout(90_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const proposal = await latestProposal(page)
  await page.goto(`/proposals/${proposal.id}`)
  await expect(page.getByRole('heading', { name: /Connected Marketplace (?:OOH|Outdoor advertising) Proposal/i })).toBeVisible()

  let record = await getProposal(page, proposal.id)
  if (record.status === 'DRAFT') {
    const summary = page.getByLabel('Executive summary')
    await expect(summary).toBeVisible()
    await summary.fill('A governed Johannesburg OOH and DOOH recommendation built from approved Marketplace supply, with supplier confirmation still required before booking.')
    await mutate(page, page.getByRole('button', { name: 'Save wording', exact: true }), /\/proposal-versions\/[^/]+:update$/)

    record = await getProposal(page, proposal.id)
    if (record.branding?.status !== 'UNBRANDED_AUTHORISED') {
      const reason = page.getByLabel('Reason to proceed without approved logos')
      await expect(reason).toBeVisible()
      await reason.fill('Local connected acceptance has no approved client or agency logo asset; unbranded delivery is explicitly authorised for this test proposal.')
      const unbranded = page.getByRole('button', { name: 'Authorise unbranded proposal', exact: true })
      await expect(unbranded).toBeEnabled()
      await mutate(page, unbranded, /\/proposal-versions\/[^/]+:approve-unbranded$/)
    }

    const approve = page.getByRole('button', { name: /Approve now|Approve proposal/ })
    await expect(approve).toBeVisible()
    await expect(approve).toBeEnabled()
    await mutate(page, approve, /\/proposal-versions\/[^/]+:approve$/)
    expect((await getProposal(page, proposal.id)).status).toBe('APPROVED')
  }

  record = await getProposal(page, proposal.id)
  if (record.status === 'APPROVED' && !record.document) {
    const render = page.getByRole('button', { name: 'Create branded PDF', exact: true })
    await expect(render).toBeVisible()
    await expect(render).toBeEnabled()
    await mutate(page, render, /\/proposal-versions\/[^/]+:render$/)
  }
  await expect(page.getByRole('link', { name: 'Open proposal PDF', exact: true })).toBeVisible()

  record = await getProposal(page, proposal.id)
  if (record.status === 'APPROVED') {
    const recipient = page.getByLabel('Client recipient')
    await expect(recipient).toBeVisible()
    await recipient.selectOption({ label: 'Local Client Approver · client.approver@advertified.local' })
    const share = page.getByRole('button', { name: 'Share with client', exact: true })
    await expect(share).toBeEnabled()
    await mutate(page, share, /\/proposal-versions\/[^/]+:share$/)
  }
  expect((await getProposal(page, proposal.id)).status).toBe('SENT')

  await switchIdentity(page, clientUserId)
  await chooseWorkspace(page, tenantId)
  await page.goto(`/proposals/${proposal.id}`)
  await expect(page.getByRole('heading', { name: 'Choose the route that best fits the campaign', exact: true })).toBeVisible()
  const select = page.getByRole('button', { name: 'Select this option', exact: true }).first()
  await expect(select).toBeVisible()
  await mutate(page, select, /\/proposal-versions\/[^/]+:select-option$/)
  await expect(page.getByText('Your selected route has been recorded. No media is booked until the next commercial steps are completed.')).toBeVisible()
  expect((await getProposal(page, proposal.id)).status).toBe('SELECTED')

  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)
  await page.goto(`/proposals/${proposal.id}`)
  await expect(page.getByRole('link', { name: /Open funding/ })).toBeVisible()
})

async function mutate(page: Page, action: Locator, route: RegExp) {
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && route.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await action.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
}

async function latestProposal(page: Page): Promise<ProposalSummary> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/proposals`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as ProposalSummary[]
  const matches = rows
    .filter(item => /Connected Marketplace OOH Proposal/i.test(item.title))
    .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))
  expect(matches.length, 'Expected a connected Marketplace proposal').toBeGreaterThan(0)
  return matches[0]
}

async function getProposal(page: Page, proposalId: string): Promise<ProposalRecord> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/proposals/${proposalId}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as ProposalRecord
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
