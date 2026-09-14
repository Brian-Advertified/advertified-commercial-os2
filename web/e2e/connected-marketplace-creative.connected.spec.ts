import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const supplierTenantId = '10000000-0000-0000-0000-000000000002'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const clientUserId = '10000000-0000-0000-0000-000000000004'
const supplierUserId = '10000000-0000-0000-0000-000000000041'
const png = { name: 'connected-creative.png', mimeType: 'image/png', buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10, 1]) }

type Session = { antiforgeryToken: string }
type ProposalSummary = { id: string; title: string; status: string; createdAtUtc: string }
type Booking = { id: string; productName: string; status: string }
type Requirement = {
  id: string
  bookingId: string
  formatCode: string
  asset: null | { id: string; currentVersion: { brandReview: null | { decision: string }; supplierReview: null | { decision: string } } }
}
type Campaign = {
  id: string
  proposalVersionId: string
  status: string
  requiredBookingCount: number
  confirmedBookingCount: number
  creative: null | { readyForApproval: boolean; requirements: Requirement[] }
}

test('confirmed Marketplace bookings reach approved booked-format creative through agency, client and supplier roles', async ({ page }) => {
  test.setTimeout(150_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)

  const proposalId = await selectedProposalId(page)
  let campaign = await campaignForProposal(page, proposalId)
  expect(campaign.requiredBookingCount).toBeGreaterThan(0)
  expect(campaign.confirmedBookingCount).toBe(campaign.requiredBookingCount)

  if (campaign.status === 'PLANNED') {
    await page.goto(`/campaigns/${campaign.id}#booking-stage`)
    await expect(page.getByRole('button', { name: 'Confirm booking coverage', exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Confirm booking coverage', exact: true }).click()
    await page.getByLabel('Confirmation reason').fill('Every client-selected Marketplace line has an exact supplier-confirmed Booking.')
    await mutate(page, page.getByRole('button', { name: 'Confirm', exact: true }), /\/campaigns\/[^/]+:confirm-bookings$/, [200])
    campaign = await getCampaign(page, campaign.id)
  }
  expect(['BOOKED', 'CREATIVE_PENDING', 'READY']).toContain(campaign.status)

  const confirmedBookings = (await bookings(page)).filter(item => item.status === 'CONFIRMED')
  expect(confirmedBookings.length).toBe(campaign.requiredBookingCount)

  if (!campaign.creative?.requirements.length) {
    await page.goto(`/campaigns/${campaign.id}#creative-stage`)
    await expect(page.getByRole('button', { name: 'Request production creative', exact: true })).toBeVisible()
    for (const booking of confirmedBookings) {
      const fieldset = page.getByRole('group', { name: booking.productName, exact: true })
      await expect(fieldset).toBeVisible()
      await fieldset.getByLabel('Format code').fill(`PNG_${booking.id.slice(0, 8).toUpperCase()}`)
      await fieldset.getByLabel('Required file type').selectOption('image/png')
      await fieldset.getByLabel('Width').fill('1920')
      await fieldset.getByLabel('Height').fill('1080')
      await fieldset.getByLabel('Maximum size (MiB)').fill('5')
      await fieldset.getByLabel('Supplier instructions').fill('Supply a 1920 by 1080 PNG for this exact confirmed booking.')
    }
    await page.getByLabel('Why production is being requested').fill('Every exact selected media line is booked and ready for production artwork.')
    await mutate(page, page.getByRole('button', { name: 'Request production creative', exact: true }), /\/campaigns\/[^/]+:request-creative$/, [200])
    campaign = await getCampaign(page, campaign.id)
  }

  for (const requirement of campaign.creative!.requirements) {
    if (requirement.asset) continue
    await page.goto(`/campaigns/${campaign.id}#creative-stage`)
    const card = creativeCard(page, requirement.formatCode)
    await expect(card).toBeVisible()
    await card.getByLabel('Exact approved copy').fill('Connected Marketplace campaign creative — approved production copy.')
    await card.getByLabel('Production file').setInputFiles(png)
    await mutate(page, card.getByRole('button', { name: 'Upload production file', exact: true }), /\/campaigns\/[^/]+\/creative$/, [200, 201])
    campaign = await getCampaign(page, campaign.id)
  }
  expect(campaign.creative!.requirements.every(item => Boolean(item.asset))).toBe(true)

  await switchIdentity(page, clientUserId)
  await chooseWorkspace(page, tenantId)
  for (const requirement of campaign.creative!.requirements) {
    const current = (await getCampaign(page, campaign.id)).creative!.requirements.find(item => item.id === requirement.id)!
    if (current.asset?.currentVersion.brandReview) continue
    await page.goto(`/campaigns/${campaign.id}#creative-stage`)
    const card = creativeCard(page, current.formatCode)
    await card.getByLabel('Rights state').selectOption('APPROVED')
    await card.getByLabel('Evidence reference').fill(`brand-review:${current.asset!.id}`)
    await card.getByLabel('Review reason').fill('The exact current file passed brand, legal and rights review for this campaign.')
    await mutate(page, card.getByRole('button', { name: 'Approve current version', exact: true }), /\/campaigns\/[^/]+\/creative\/[^/]+:brand-review$/, [200])
  }

  campaign = await getCampaign(page, campaign.id)
  await switchIdentity(page, supplierUserId)
  await chooseWorkspace(page, supplierTenantId)
  for (const requirement of campaign.creative!.requirements) {
    const assetId = requirement.asset!.id
    const supplierAsset = await supplierCreative(page, assetId)
    if (supplierAsset.supplierDecision) continue
    await page.goto(`/creative-assets/${assetId}`)
    await expect(page.getByRole('heading', { name: 'Review the exact production file for your booked format.', exact: true })).toBeVisible()
    await page.getByLabel('Technical evidence reference').fill(`supplier-review:${assetId}`)
    await page.getByLabel('Decision reason').fill('The exact current file meets the booked technical requirement and delivery specification.')
    await mutate(page, page.getByRole('button', { name: 'Approve technical delivery', exact: true }), /\/creative-assets\/[^/]+:supplier-review$/, [200])
  }

  await switchIdentity(page, clientUserId)
  await chooseWorkspace(page, tenantId)
  await page.goto(`/campaigns/${campaign.id}#creative-stage`)
  campaign = await getCampaign(page, campaign.id)
  expect(campaign.creative?.readyForApproval).toBe(true)
  if (campaign.status === 'CREATIVE_PENDING') {
    await page.getByLabel('Approval reason').fill('Every current creative file has approved brand/rights and supplier technical reviews.')
    await mutate(page, page.getByRole('button', { name: 'Approve creative readiness', exact: true }), /\/campaigns\/[^/]+:approve-creative$/, [200])
  }
  campaign = await getCampaign(page, campaign.id)
  expect(campaign.status).toBe('READY')
})

function creativeCard(page: Page, formatCode: string) {
  return page.locator('article.creative-requirement-card').filter({ has: page.getByRole('heading', { name: formatCode, exact: true }) })
}

async function supplierCreative(page: Page, assetId: string) {
  const response = await page.request.get(`/api/v1/tenants/${supplierTenantId}/creative-assets/${assetId}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as { supplierDecision: string | null }
}

async function bookings(page: Page): Promise<Booking[]> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/bookings`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Booking[]
}

async function getCampaign(page: Page, campaignId: string): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns/${campaignId}`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Campaign
}

async function campaignForProposal(page: Page, proposalId: string): Promise<Campaign> {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/campaigns`)
  expect(response.ok(), await response.text()).toBe(true)
  const campaigns = await response.json() as Campaign[]
  const campaign = campaigns.find(item => item.proposalVersionId === proposalId)
  expect(campaign, 'Expected funded selected proposal to create a planned campaign').toBeTruthy()
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
  await page.evaluate(() => sessionStorage.removeItem('advertified.workspace'))
  await page.goto('/workspaces')
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
