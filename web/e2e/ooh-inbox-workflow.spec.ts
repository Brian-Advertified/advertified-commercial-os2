import { expect, test, type Page, type Route } from '@playwright/test'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const userId = 'e2000000-0000-0000-0000-000000000001'
const clientId = 'e3000000-0000-0000-0000-000000000001'
const mailboxId = 'e4000000-0000-0000-0000-000000000001'
const sentEmailId = 'e5000000-0000-0000-0000-000000000001'
const reviewEmailId = 'e5000000-0000-0000-0000-000000000002'
const ambiguousEmailId = 'e5000000-0000-0000-0000-000000000003'
const acceptedEmailId = 'e5000000-0000-0000-0000-000000000004'
const interruptedEmailId = 'e5000000-0000-0000-0000-000000000005'
const sentRunId = 'e6000000-0000-0000-0000-000000000001'
const reviewRunId = 'e6000000-0000-0000-0000-000000000002'
const ambiguousRunId = 'e6000000-0000-0000-0000-000000000003'
const acceptedRunId = 'e6000000-0000-0000-0000-000000000004'
const interruptedRunId = 'e6000000-0000-0000-0000-000000000005'
const briefId = 'e7000000-0000-0000-0000-000000000001'
const briefVersionId = 'e7000000-0000-0000-0000-000000000002'
const proposalId = 'e8000000-0000-0000-0000-000000000001'
const now = '2026-08-30T18:00:00Z'

type State = {
  configured: boolean
  reconcileCalls: number
  acceptedFinishCalls: number
  acceptedFinished: boolean
  resumeCalls: number
  resumed: boolean
  retryCalls: number
}

test('operator connects one mailbox and monitors automatic OOH proposals', async ({ page }) => {
  const state: State = {
    configured: false,
    reconcileCalls: 0,
    acceptedFinishCalls: 0,
    acceptedFinished: false,
    resumeCalls: 0,
    resumed: false,
    retryCalls: 0,
  }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/ooh-inbox')
  await expect(page).toHaveURL(/\/ooh-inbox$/)
  await expect(page.getByRole('heading', { name: 'Proposal inbox' })).toBeVisible({ timeout: 15_000 })
  await expect(page.getByText('OOH / DOOH only')).toBeVisible()
  await page.getByLabel('Mailbox address').fill('ooh@advertified.com')
  await page.getByLabel('Allowed sender domains').fill('client.example')
  await page.getByLabel('Send complete proposals automatically').check()
  await page.getByRole('button', { name: 'Connect mailbox' }).click()

  await expect(page.getByRole('heading', { name: 'ooh@advertified.com' })).toBeVisible()
  await expect(page.getByText('Automatic sending on')).toBeVisible()
  await expect(page.getByText('The proposal was sent automatically')).toBeVisible()
  await expect(page.getByText('OOH-only campaign')).toBeVisible()
  await expect(page.getByText(/cannot be widened later/i)).toBeVisible()

  await page.getByRole('button', { name: /Radio requested with OOH/ }).click()
  await expect(page.getByRole('heading', { name: 'Nothing was sent' })).toBeVisible()
  await expect(page.getByRole('status').getByText(
    'This request includes media beyond OOH. Start a new full campaign instead.',
    { exact: true },
  )).toBeVisible()
  await expect(page.getByRole('link', { name: 'Open proposal' })).toHaveCount(0)

  await page.getByRole('button', { name: /Delivery response unavailable/ }).click()
  const detail = page.locator('.ooh-message-detail')
  await expect(detail.getByRole('heading', { name: 'Provider acceptance is unknown' })).toBeVisible()
  await expect(detail.getByText(/provider may have accepted the original delivery request/i)).toBeVisible()
  await expect(detail.getByText('Not confirmed')).toBeVisible()
  await expect(detail.getByRole('heading', { name: 'Nothing was sent' })).toHaveCount(0)
  await expect(detail.getByRole('button', { name: 'Retry request' })).toHaveCount(0)
  await detail.getByRole('button', { name: 'Check original delivery' }).click()
  await expect.poll(() => state.reconcileCalls).toBe(1)
  expect(state.retryCalls).toBe(0)

  await page.getByRole('button', { name: /Accepted delivery awaiting completion/ }).click()
  await expect(detail.getByRole('heading', { name: 'Provider acceptance is recorded' })).toBeVisible()
  await expect(detail.getByText(/provider accepted the original delivery/i)).toBeVisible()
  await expect(detail.getByRole('button', { name: 'Retry request' })).toHaveCount(0)
  await expect(detail.getByRole('heading', {
    name: 'Confirm what the Brief did not establish',
  })).toHaveCount(0)
  await detail.getByRole('button', { name: 'Finish recorded delivery' }).click()
  await expect.poll(() => state.acceptedFinishCalls).toBe(1)
  await expect(detail.getByRole('heading', {
    name: 'The proposal was sent automatically',
  })).toBeVisible()
  expect(state.retryCalls).toBe(0)

  await resumeInterruptedRun(page, state)
})

async function resumeInterruptedRun(page: Page, state: State) {
  await page.getByRole('button', { name: /Interrupted proposal preparation/ }).click()
  const detail = page.locator('.ooh-message-detail')
  await expect(detail.getByRole('heading', {
    name: 'Run can be resumed from its saved checkpoint',
  })).toBeVisible()
  await expect(detail.getByText(/reuse completed steps/i)).toBeVisible()
  await detail.getByRole('button', { name: 'Resume from saved checkpoint' }).click()
  await expect.poll(() => state.resumeCalls).toBe(1)
  await expect(detail.getByRole('heading', {
    name: 'The proposal was sent automatically',
  })).toBeVisible()
}

async function handleApi(route: Route, state: State) {
  const request = route.request()
  const path = new URL(request.url()).pathname
  if (request.method() === 'GET') return handleRead(route, state, path)
  if (path.endsWith('/email-automation/mailbox')) {
    assertMutation(route)
    const body = request.postDataJSON() as Record<string, unknown>
    expect(body.address).toBe('ooh@advertified.com')
    expect(body.autoSendEnabled).toBe(true)
    expect(body.allowedSenderDomains).toEqual(['client.example'])
    state.configured = true
    return json(route, mailbox())
  }
  if (path.endsWith(`/email-automation/messages/${ambiguousEmailId}:process`)) {
    assertMutation(route)
    expect(request.headers()['if-match']).toBe('"1"')
    expect(request.postDataJSON()).toEqual({})
    state.reconcileCalls += 1
    return json(route, ambiguousRun())
  }
  if (path.endsWith(`/email-automation/messages/${acceptedEmailId}:process`)) {
    assertMutation(route)
    expect(request.headers()['if-match']).toBe('"1"')
    expect(request.postDataJSON()).toEqual({})
    state.acceptedFinishCalls += 1
    state.acceptedFinished = true
    return json(route, acceptedRun(true))
  }
  if (path.endsWith(`/email-automation/messages/${interruptedEmailId}:process`)) {
    assertMutation(route)
    expect(request.headers()['if-match']).toBe('"1"')
    state.resumeCalls += 1
    state.resumed = true
    return json(route, interruptedRun(true))
  }
  if (path.includes('/email-automation/messages/') && path.endsWith(':retry')) {
    state.retryCalls += 1
    return json(route, { code: 'EMAIL_AUTOMATION_NOT_RETRYABLE', status: 409 }, 409)
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

async function handleRead(route: Route, state: State, path: string) {
  if (path === '/api/v1/session') return json(route, session())
  if (path === '/api/v1/workspaces') return json(route, [workspace()])
  if (path === '/api/v1/me') return json(route, user(), 200, { ETag: '"1"' })
  if (path.endsWith('/client-accounts')) {
    return json(route, { items: [client()], nextCursor: null })
  }
  return handleAutomationRead(route, state, path)
}

async function handleAutomationRead(route: Route, state: State, path: string) {
  if (path.endsWith('/email-automation/mailbox')) {
    return json(route, state.configured ? mailbox() : null)
  }
  if (path.endsWith('/email-automation/messages')) {
    const items = state.configured
      ? [sentEmail(), reviewEmail(), ambiguousEmail(), acceptedEmail(state.acceptedFinished),
          interruptedEmail(state.resumed)]
      : []
    return json(route, { items, nextCursor: null })
  }
  if (path.endsWith(`/email-automation/messages/${sentEmailId}`)) {
    return json(route, {
      email: sentEmail(), run: sentRun(), sourceContent: 'Complete OOH request.', questions: [],
    })
  }
  if (path.endsWith(`/email-automation/messages/${reviewEmailId}`)) {
    return json(route, {
      email: reviewEmail(), run: reviewRun(), sourceContent: 'OOH and radio request.', questions: [],
    })
  }
  if (path.endsWith(`/email-automation/messages/${ambiguousEmailId}`)) {
    return json(route, {
      email: ambiguousEmail(), run: ambiguousRun(),
      sourceContent: 'Complete OOH request with an unknown delivery response.', questions: [],
    })
  }
  if (path.endsWith(`/email-automation/messages/${acceptedEmailId}`)) {
    return json(route, {
      email: acceptedEmail(state.acceptedFinished), run: acceptedRun(state.acceptedFinished),
      sourceContent: 'Complete OOH request whose delivery was accepted.',
      questions: [{
        fieldPath: 'objective', question: 'What is the campaign objective?', options: [],
      }],
    })
  }
  if (path.endsWith(`/email-automation/messages/${interruptedEmailId}`)) {
    return json(route, {
      email: interruptedEmail(state.resumed), run: interruptedRun(state.resumed),
      sourceContent: 'Complete OOH request interrupted after a saved checkpoint.', questions: [],
    })
  }
  return json(route, { code: 'NOT_FOUND', status: 404 }, 404)
}

function mailbox() {
  return { id: mailboxId, tenantId, address: 'ooh@advertified.com', provider: 'RESEND', ownerUserId: userId,
    defaultClientAccountId: clientId, autoSendEnabled: true,
    allowedSenderDomains: ['client.example'], isEnabled: true, version: 1, updatedAtUtc: now }
}

function sentEmail() {
  return { id: sentEmailId, tenantId, mailboxId, providerEmailId: 'provider-sent',
    providerMessageId: 'message-sent', senderEmail: 'planner@client.example',
    senderName: 'Client Planner', replyToEmail: 'planner@client.example',
    subject: 'Johannesburg OOH proposal', sourceHash: 'a'.repeat(64), attachments: [],
    receivedAtUtc: now, updatedAtUtc: now, status: 'SENT', failureCode: null }
}

function reviewEmail() {
  return { id: reviewEmailId, tenantId, mailboxId, providerEmailId: 'provider-review',
    providerMessageId: 'message-review', senderEmail: 'planner@client.example',
    senderName: 'Client Planner', replyToEmail: 'planner@client.example',
    subject: 'Radio requested with OOH', sourceHash: 'b'.repeat(64), attachments: [],
    receivedAtUtc: now, updatedAtUtc: now, status: 'REVIEW_REQUIRED',
    failureCode: 'NON_OOH_REQUEST' }
}

function ambiguousEmail() {
  return { id: ambiguousEmailId, tenantId, mailboxId, providerEmailId: 'provider-ambiguous',
    providerMessageId: 'message-ambiguous', senderEmail: 'planner@client.example',
    senderName: 'Client Planner', replyToEmail: 'planner@client.example',
    subject: 'Delivery response unavailable', sourceHash: 'c'.repeat(64), attachments: [],
    receivedAtUtc: now, updatedAtUtc: now, status: 'REVIEW_REQUIRED',
    failureCode: 'DELIVERY_AMBIGUOUS' }
}

function acceptedEmail(finished: boolean) {
  return { id: acceptedEmailId, tenantId, mailboxId, providerEmailId: 'provider-accepted',
    providerMessageId: 'message-accepted', senderEmail: 'planner@client.example',
    senderName: 'Client Planner', replyToEmail: 'planner@client.example',
    subject: 'Accepted delivery awaiting completion', sourceHash: 'd'.repeat(64), attachments: [],
    receivedAtUtc: now, updatedAtUtc: now, status: finished ? 'SENT' : 'REVIEW_REQUIRED',
    failureCode: finished ? null : 'DELIVERY_RECORDING_REQUIRED' }
}

function interruptedEmail(finished: boolean) {
  return { id: interruptedEmailId, tenantId, mailboxId, providerEmailId: 'provider-interrupted',
    providerMessageId: 'message-interrupted', senderEmail: 'planner@client.example',
    senderName: 'Client Planner', replyToEmail: 'planner@client.example',
    subject: 'Interrupted proposal preparation', sourceHash: 'e'.repeat(64), attachments: [],
    receivedAtUtc: now, updatedAtUtc: now, status: finished ? 'SENT' : 'PROCESSING',
    failureCode: null }
}

function sentRun() {
  return runFixture({ id: sentRunId, inboundEmailId: sentEmailId, status: 'SENT',
    checkpoint: 'SENT', proposalVersionId: proposalId, failureCode: null, failureMessage: null,
    deliveryProviderCode: 'RESEND', deliveryProviderId: 'delivery-1',
    deliveryRequestedAtUtc: now, deliveryAcceptedAtUtc: now })
}

function reviewRun() {
  return runFixture({ id: reviewRunId, inboundEmailId: reviewEmailId, status: 'REVIEW_REQUIRED',
    checkpoint: 'SOURCE_CAPTURED', proposalVersionId: null, failureCode: 'NON_OOH_REQUEST',
    failureMessage: 'This request includes media beyond OOH. Start a new full campaign instead.' })
}

function ambiguousRun() {
  return runFixture({ id: ambiguousRunId, inboundEmailId: ambiguousEmailId,
    status: 'REVIEW_REQUIRED', checkpoint: 'DELIVERY_REQUESTED',
    failureCode: 'DELIVERY_AMBIGUOUS',
    failureMessage: 'The email provider response did not establish acceptance.',
    deliveryProviderCode: 'RESEND', deliveryRequestedAtUtc: now })
}

function acceptedRun(finished: boolean) {
  return runFixture({ id: acceptedRunId, inboundEmailId: acceptedEmailId,
    status: finished ? 'SENT' : 'REVIEW_REQUIRED',
    checkpoint: finished ? 'SENT' : 'DELIVERY_ACCEPTED',
    failureCode: finished ? null : 'DELIVERY_RECORDING_REQUIRED',
    failureMessage: finished ? null : 'Local delivery recording needs to finish.',
    deliveryProviderCode: 'RESEND', deliveryProviderId: 'delivery-accepted',
    deliveryRequestedAtUtc: now, deliveryAcceptedAtUtc: now })
}

function interruptedRun(finished: boolean) {
  return runFixture({ id: interruptedRunId, inboundEmailId: interruptedEmailId,
    status: finished ? 'SENT' : 'PROCESSING',
    checkpoint: finished ? 'SENT' : 'PLAN_APPROVED',
    failureCode: null, failureMessage: null })
}

function runFixture(overrides: Record<string, unknown>) {
  return { id: sentRunId, tenantId, inboundEmailId: sentEmailId, campaignMode: 'OOH_ONLY',
    status: 'PROCESSING', checkpoint: 'SOURCE_CAPTURED', clientAccountId: clientId,
    briefId, briefVersionId,
    stpVersionId: 'e7100000-0000-0000-0000-000000000001',
    mediaMixVersionId: 'e7200000-0000-0000-0000-000000000001',
    shortlistVersionId: 'e7300000-0000-0000-0000-000000000001',
    mediaPlanVersionId: 'e7400000-0000-0000-0000-000000000001',
    proposalVersionId: proposalId, documentId: 'e7500000-0000-0000-0000-000000000001',
    deliveryProviderCode: null, deliveryProviderId: null,
    deliveryRequestedAtUtc: null, deliveryAcceptedAtUtc: null,
    failureCode: null, failureMessage: null,
    version: 1, incrementalAiCostMinor: 0, createdAtUtc: now, updatedAtUtc: now,
    ...overrides }
}

function session() {
  return { authenticated: true, antiforgeryToken: 'csrf-ooh-inbox', expiresAtUtc: '2026-08-30T22:00:00Z' }
}
function workspace() {
  return { membershipId: 'e9000000-0000-0000-0000-000000000001', tenantId,
    name: 'Agency Workspace', slug: 'agency-workspace', roleCode: 'agency_admin', version: 1 }
}
function user() {
  return { id: userId, email: 'operator@agency.example', displayName: 'Agency Operator',
    phone: null, mfaEnabled: true, version: 1 }
}
function client() {
  return { id: clientId, tenantId, externalReference: 'planning-client', legalName: 'Planning Client',
    tradingName: 'Planning Client', website: null, industry: null, billingProfileJson: '{}',
    primaryContactId: null, statusCode: 'ACTIVE', version: 1, updatedAtUtc: now }
}
function assertMutation(route: Route) {
  const headers = route.request().headers()
  expect(headers['x-csrf-token']).toBe('csrf-ooh-inbox')
  expect(headers['idempotency-key']).toBeTruthy()
}
async function json(route: Route, body: unknown, status = 200, headers?: Record<string, string>) {
  await route.fulfill({ status, headers, contentType: 'application/json', body: JSON.stringify(body) })
}
