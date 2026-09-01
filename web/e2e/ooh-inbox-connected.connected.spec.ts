import { expect, test, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000002'
const mailboxAddress = 'proposals@advertified.local'
const mailboxPath = '/email-automation/mailbox'
const messagesPath = '/email-automation/messages'

test('connected proposal inbox opens against the local API', async ({ page }) => {
  const failedApiResponses: string[] = []
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failedApiResponses.push(`${response.status()} ${new URL(response.url()).pathname}`)
    }
  })

  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()
  await ensureLocalMailbox(page)

  const mailboxResponsePromise = page.waitForResponse(response =>
    new URL(response.url()).pathname.endsWith(mailboxPath))
  const messagesResponsePromise = page.waitForResponse(response =>
    new URL(response.url()).pathname.endsWith(messagesPath))
  const userResponsePromise = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/v1/me')

  await page.goto('/ooh-inbox')

  const [mailboxResponse, messagesResponse, userResponse] = await Promise.all([
    mailboxResponsePromise,
    messagesResponsePromise,
    userResponsePromise,
  ])

  const mailboxBody = await mailboxResponse.text()
  const messagesBody = await messagesResponse.text()
  const userBody = await userResponse.text()
  expect(mailboxResponse.status(), mailboxBody).toBe(200)
  expect(messagesResponse.status(), messagesBody).toBe(200)
  expect(userResponse.status(), userBody).toBe(200)
  await expect(page.getByRole('heading', {
    name: 'Proposal inbox',
    exact: true,
  })).toBeVisible()
  await expect(page.getByRole('heading', {
    name: mailboxAddress,
    exact: true,
  })).toBeVisible()
  await expect(page.getByRole('heading', {
    name: 'Proposal inbox could not be opened',
  })).toHaveCount(0)
  expect(failedApiResponses).toEqual([])
})

async function ensureLocalMailbox(page: Page) {
  const mailboxUrl = `/api/v1/tenants/${tenantId}/email-automation/mailbox`
  const current = await page.request.get(mailboxUrl)
  const currentBody = await current.text()
  expect(current.status(), currentBody).toBe(200)
  if (currentBody.trim()) return

  const sessionResponse = await page.request.get('/api/v1/session')
  const session = await sessionResponse.json() as { antiforgeryToken: string }
  const userResponse = await page.request.get('/api/v1/me')
  const user = await userResponse.json() as { id: string }
  const created = await page.request.post(mailboxUrl, {
    data: {
      address: mailboxAddress,
      provider: 'DETERMINISTIC',
      ownerUserId: user.id,
      defaultClientAccountId: null,
      autoSendEnabled: false,
      allowedSenderDomains: ['advertified.local'],
    },
    headers: {
      Origin: 'http://localhost:3017',
      'X-CSRF-TOKEN': session.antiforgeryToken,
      'Idempotency-Key': `connected-local-mailbox-${Date.now()}`,
      'X-Correlation-ID': crypto.randomUUID(),
    },
  })
  expect(created.status(), await created.text()).toBe(201)
}
