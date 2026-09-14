import { expect, test, type Locator, type Page } from '@playwright/test'

const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const supplierUserId = '10000000-0000-0000-0000-000000000041'

type Session = { antiforgeryToken: string }
type BookableLine = {
  mediaPlanLineId: string
  proposalVersionId: string
  proposalOptionId: string
  productName: string
  alreadyBooked: boolean
}
type Booking = {
  id: string
  mediaPlanLineId: string | null
  productName: string
  status: string
  buyerTenantId: string
  supplierTenantId: string
  clientPriceMinor: number | null
  supplierCostMinor?: number | null
}

test('funded Marketplace lines become supplier-confirmed bookings through buyer and supplier UI', async ({ page }) => {
  test.setTimeout(120_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)

  const bookable = await bookableLines(page)
  const targetLines = bookable.filter(item => /Local Demo Johannesburg/i.test(item.productName))
  expect(targetLines.length, 'Expected funded Marketplace lines to be bookable').toBeGreaterThan(0)

  let buyerBookings = await bookings(page, buyerTenantId)
  for (const line of targetLines) {
    let booking = buyerBookings.find(item => item.mediaPlanLineId === line.mediaPlanLineId) ?? null
    if (!booking) {
      await page.goto('/bookings')
      await expect(page.getByRole('heading', { name: 'Bookings', exact: true })).toBeVisible()
      const card = bookingCard(page, line.productName, 'Not booked')
      await expect(card).toBeVisible()
      await mutate(page, card.getByRole('button', { name: 'Create booking draft', exact: true }), /\/bookings$/, [200, 201])
      buyerBookings = await bookings(page, buyerTenantId)
      booking = buyerBookings.find(item => item.mediaPlanLineId === line.mediaPlanLineId) ?? null
      expect(booking).toBeTruthy()
    }
    if (booking!.status === 'DRAFT') {
      await page.goto('/bookings')
      const card = bookingCard(page, line.productName, 'Draft')
      await expect(card).toBeVisible()
      await mutate(page, card.getByRole('button', { name: 'Request supplier confirmation', exact: true }), /\/bookings\/[^/]+:request-confirmation$/, [200])
      buyerBookings = await bookings(page, buyerTenantId)
      booking = buyerBookings.find(item => item.id === booking!.id)!
      expect(booking.status).toBe('PENDING_SUPPLIER')
    }
  }

  const targetIds = new Set(buyerBookings
    .filter(item => targetLines.some(line => line.mediaPlanLineId === item.mediaPlanLineId))
    .map(item => item.id))

  await switchIdentity(page, supplierUserId)
  await chooseWorkspace(page, supplierTenantId)
  await page.goto('/bookings')
  await expect(page.getByRole('heading', { name: 'Bookings', exact: true })).toBeVisible()

  let supplierBookings = await bookings(page, supplierTenantId)
  const supplierTargets = supplierBookings.filter(item => targetIds.has(item.id))
  expect(supplierTargets.length).toBe(targetIds.size)
  for (const booking of supplierTargets) {
    expect(booking.clientPriceMinor).toBeNull()
    expect(booking.supplierCostMinor).not.toBeNull()
    if (booking.status === 'CONFIRMED') continue
    expect(booking.status).toBe('PENDING_SUPPLIER')
    const card = bookingCard(page, booking.productName, 'Supplier review')
    await expect(card).toBeVisible()
    await card.getByLabel(/I confirm the current rate, availability, schedule, and frozen terms/).check()
    await card.getByLabel('Supplier note').fill('Current Marketplace rate, availability, schedule and frozen terms confirmed for this exact booking.')
    await mutate(page, card.getByRole('button', { name: 'Confirm booking', exact: true }), /\/bookings\/[^/]+:confirm$/, [200])
    supplierBookings = await bookings(page, supplierTenantId)
    expect(supplierBookings.find(item => item.id === booking.id)?.status).toBe('CONFIRMED')
  }

  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)
  await page.goto('/bookings')
  buyerBookings = await bookings(page, buyerTenantId)
  const finalTargets = buyerBookings.filter(item => targetIds.has(item.id))
  expect(finalTargets).toHaveLength(targetIds.size)
  expect(finalTargets.every(item => item.status === 'CONFIRMED')).toBe(true)
  for (const booking of finalTargets) {
    await expect(bookingCard(page, booking.productName, 'Confirmed')
      .getByText('Confirmed by both buyer workflow and supplier.', { exact: true })).toBeVisible()
  }
})

function bookingCard(page: Page, productName: string, status: string) {
  return page.locator('article.booking-card').filter({
    has: page.getByRole('heading', { name: productName, exact: true }),
  }).filter({ hasText: status }).first()
}

async function bookableLines(page: Page): Promise<BookableLine[]> {
  const response = await page.request.get(`/api/v1/tenants/${buyerTenantId}/bookings/bookable-lines`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as BookableLine[]
}

async function bookings(page: Page, tenantId: string): Promise<Booking[]> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/bookings`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Booking[]
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
