import { expect, test, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const briefVersionId = 'ef657f7d-ce52-41b2-b467-c9db186d55c3'

type Session = { antiforgeryToken: string }

test('trace connected Marketplace shortlist mutation from the visible UI', async ({ page }) => {
  test.setTimeout(90_000)
  const events: string[] = []
  page.on('console', message => events.push(`console:${message.type()}:${message.text()}`))
  page.on('pageerror', error => events.push(`pageerror:${error.message}`))
  page.on('request', request => {
    if (request.url().includes('/shortlists:generate')) events.push(`request:${request.method()}:${request.url()}`)
  })
  page.on('requestfailed', request => {
    if (request.url().includes('/shortlists:generate')) {
      events.push(`requestfailed:${request.failure()?.errorText ?? 'unknown'}:${request.url()}`)
    }
  })
  page.on('response', response => {
    if (response.url().includes('/shortlists:generate')) events.push(`response:${response.status()}:${response.url()}`)
  })

  await signIn(page)
  await switchIdentity(page, buyerUserId)
  await page.evaluate((id) => sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id })), tenantId)
  await page.goto(`/planning/${briefVersionId}`)
  await expect(page.getByRole('heading', { name: 'Media plan & partner selection' })).toBeVisible()
  const add = page.getByRole('button', { name: '＋ Add Placement', exact: true })
  await expect(add).toBeVisible()
  await add.click()

  await expect.poll(() => events.some(value => value.startsWith('request:')) || events.some(value => value.startsWith('pageerror:')), {
    timeout: 10_000,
  }).toBe(true)
  if (events.some(value => value.startsWith('request:'))) {
    await expect.poll(() => events.some(value => value.startsWith('response:')) || events.some(value => value.startsWith('requestfailed:')), {
      timeout: 60_000,
    }).toBe(true)
  }
  const alert = page.getByRole('alert').first()
  const alertText = await alert.isVisible().then(async visible => visible ? await alert.innerText() : null)
  console.log(JSON.stringify({ events, alertText }, null, 2))
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}

async function switchIdentity(page: Page, userId: string) {
  const sessionResponse = await page.request.get('/api/v1/session')
  expect(sessionResponse.ok(), await sessionResponse.text()).toBe(true)
  const session = await sessionResponse.json() as Session
  const response = await page.request.post('/api/v1/development/connected-acceptance/identity', {
    data: { userId },
    headers: {
      Origin: 'http://localhost:3017',
      'X-CSRF-TOKEN': session.antiforgeryToken,
      'Idempotency-Key': crypto.randomUUID(),
      'X-Correlation-ID': crypto.randomUUID(),
    },
  })
  expect(response.ok(), await response.text()).toBe(true)
}
