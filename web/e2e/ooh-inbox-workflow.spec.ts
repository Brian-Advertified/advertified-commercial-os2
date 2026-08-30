import { expect, test, type Route } from '@playwright/test'

const tenantId = 'e1000000-0000-0000-0000-000000000001'
const userId = 'e2000000-0000-0000-0000-000000000001'
const clientId = 'e3000000-0000-0000-0000-000000000001'
const mailboxId = 'e4000000-0000-0000-0000-000000000001'
const sentEmailId = 'e5000000-0000-0000-0000-000000000001'
const reviewEmailId = 'e5000000-0000-0000-0000-000000000002'
const sentRunId = 'e6000000-0000-0000-0000-000000000001'
const reviewRunId = 'e6000000-0000-0000-0000-000000000002'
const briefId = 'e7000000-0000-0000-0000-000000000001'
const briefVersionId = 'e7000000-0000-0000-0000-000000000002'
const proposalId = 'e8000000-0000-0000-0000-000000000001'
const now = '2026-08-30T18:00:00Z'

type State = { configured: boolean }

test('operator connects one mailbox and monitors automatic OOH proposals', async ({ page }) => {
  const state: State = { configured: false }
  await page.addInitScript((id) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id }))
  }, tenantId)
  await page.route('**/api/v1/**', async (route) => handleApi(route, state))

  await page.goto('/ooh-inbox')
  await expect(page.getByRole('heading', { name: 'Proposal inbox' })).toBeVisible()
  await expect(page.getByText('OOH / DOOH only')).toBeVisible()
  await page.getByLabel('Mailbox address').fill('ooh@advertified.com')
  await page.getByLabel('Allowed sender domains').fill('client.example')
  await page.getByRole('button', { name: 'Connect mailbox' }).click()

  await expect(page.getByRole('heading', { name: 'ooh@advertified.com' })).toBeVisible()
  await expect(page.getByText('Complete proposals send themselves')).toBeVisible()
  await expect(page.getByText('The proposal was sent automatically')).toBeVisible()
  await expect(page.getByText('OOH-only campaign')).toBeVisible()
  await expect(page.getByText(/cannot be widened later/i)).toBeVisible()

  await page.getByRole('button', { name: /Radio requested with OOH/ }).click()
  await expect(page.getByRole('heading', { name: 'Nothing was sent' })).toBeVisible()
  await expect(page.getByRole('status').getByText(/start a new full campaign from the beginning/i)).toBeVisible()
  await expect(page.getByRole('link', { name: 'Open proposal' })).toHaveCount(0)
})

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
    const items = state.configured ? [sentEmail(), reviewEmail()] : []
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

function sentRun() {
  return runFixture({ id: sentRunId, inboundEmailId: sentEmailId, status: 'SENT',
    checkpoint: 'SENT', proposalVersionId: proposalId, failureCode: null, failureMessage: null })
}

function reviewRun() {
  return runFixture({ id: reviewRunId, inboundEmailId: reviewEmailId, status: 'REVIEW_REQUIRED',
    checkpoint: 'SOURCE_CAPTURED', proposalVersionId: null, failureCode: 'NON_OOH_REQUEST',
    failureMessage: 'The request includes radio and cannot continue as OOH-only.' })
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
    deliveryProviderId: 'delivery-1', failureCode: null, failureMessage: null,
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
