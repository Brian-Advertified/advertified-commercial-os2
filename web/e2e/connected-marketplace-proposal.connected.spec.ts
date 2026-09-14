import { expect, test, type Locator, type Page } from '@playwright/test'

const buyerTenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const title = `Connected Marketplace Campaign ${Date.now()}`

type Session = { antiforgeryToken: string }
type Planning = {
  mediaPlan: null | {
    id: string
    status: string
    lines: Array<{ marketplaceListingVersionId: string | null; inventoryTenantId: string }>
  }
}

test('isolated buyer reaches Proposal with Marketplace-backed plan lineage', async ({ page }) => {
  test.setTimeout(240_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, buyerTenantId)

  await page.goto('/briefs/new')
  await page.getByLabel('Campaign or Brief name').fill(title)
  await page.getByLabel('Client requirement').fill([
    'Client: Connected Marketplace Client',
    'Problem: Johannesburg business buyers need awareness of the new workspace range.',
    'Objective: Generate qualified enquiries.',
    'Audience: Small business owners and office managers.',
    'Geography: Johannesburg.',
    'Timing: 2026-10-01 to 2026-10-31.',
    'Budget: ZAR 200,000 including VAT.',
    'Media: OOH and DOOH only.',
    'Measurement: Qualified enquiries.',
  ].join('\n'))
  await page.getByRole('button', { name: /Next: AI Interpretation/ }).click()
  await expect(page.getByRole('heading', { name: 'AI brief interpretation' })).toBeVisible()
  await page.getByRole('button', { name: /Approve and continue/ }).click()
  await expect(page).toHaveURL(/\/stp\/[0-9a-f-]{36}$/)
  await expect(page.getByRole('button', { name: /^Research & build audience strategy$/i })).toBeVisible()

  for (let step = 0; step < 50; step += 1) {
    await fillAudienceDirections(page)
    if (await generateShortlist(page)) continue
    if (await selectInventory(page)) continue
    if (await buildMediaPlan(page)) continue
    if (await reviewMediaPlan(page)) continue
    if (await prepareProposal(page)) continue
    if (await proposalVisible(page)) break
    const action = await nextAction(page)
    expect(action, `No forward action at ${page.url()}`).not.toBeNull()
    await action!.click()
    await page.waitForTimeout(250)
    const alert = page.getByRole('alert').first()
    if (await alert.isVisible()) throw new Error(`Visible application error: ${await alert.innerText()}`)
  }

  expect(await proposalVisible(page), `Proposal was not reached: ${page.url()}`).toBe(true)
  const briefVersionId = await latestApprovedVersion(page)
  const planningResponse = await page.request.get(
    `/api/v1/tenants/${buyerTenantId}/brief-versions/${briefVersionId}/planning`,
  )
  expect(planningResponse.ok(), await planningResponse.text()).toBe(true)
  const planning = await planningResponse.json() as Planning
  expect(planning.mediaPlan?.status).toBe('APPROVED')
  expect(planning.mediaPlan?.lines.length).toBeGreaterThan(0)
  expect(planning.mediaPlan!.lines.every(line => Boolean(line.marketplaceListingVersionId))).toBe(true)
  expect(planning.mediaPlan!.lines.every(line => line.inventoryTenantId !== buyerTenantId)).toBe(true)
})

async function fillAudienceDirections(page: Page) {
  if (!new URL(page.url()).pathname.startsWith('/stp/')) return
  const basis = page.locator('details.connected-strategy-basis')
  if (!(await basis.count())) return
  if (await basis.getAttribute('open') === null) await basis.locator('summary').click()
  for (const textarea of await basis.locator('textarea').all()) {
    if (await textarea.isVisible() && !(await textarea.inputValue()).trim()) {
      await textarea.fill('Hypothesis: Use the approved Brief objective and audience only; validate any unsupported claims before external release.')
    }
  }
}

async function generateShortlist(page: Page) {
  if (!new URL(page.url()).pathname.startsWith('/planning/')) return false
  const button = page.getByRole('button', { name: /^＋?\s*Add Placement$/i })
  if (!(await button.isVisible()) || !(await button.isEnabled())) return false
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    /\/brief-versions\/[^/]+\/shortlists:generate$/.test(new URL(response.url()).pathname),
    { timeout: 60_000 },
  )
  await button.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  await expect(page.locator('summary').filter({ hasText: /^Review shortlist and alternatives$/ })).toBeVisible()
  return true
}

async function selectInventory(page: Page) {
  if (!new URL(page.url()).pathname.startsWith('/planning/')) return false
  const summary = page.locator('summary').filter({ hasText: /^Review shortlist and alternatives$/ })
  if (!(await summary.isVisible())) return false
  if (await summary.locator('..').getAttribute('open') === null) await summary.click()
  const reason = page.getByRole('textbox', { name: /^Why are you carrying these placements forward\?/ })
  if (!(await reason.isVisible())) return false
  const checked = page.locator('.shortlist-card input[type="checkbox"]:checked')
  if (!(await checked.count())) {
    const candidates = page.locator('.shortlist-card input[type="checkbox"]:not([disabled])')
    const count = await candidates.count()
    expect(count, 'Marketplace buyer needs eligible external placements').toBeGreaterThan(0)
    for (let index = 0; index < count; index += 1) await candidates.nth(index).check()
  }
  await reason.fill('Carry forward the current eligible published Marketplace placements for the approved Johannesburg OOH campaign. Supplier confirmation remains required before booking.')
  const confirm = page.getByRole('button', { name: /Confirm selected inventory/i })
  if (!(await confirm.isVisible()) || !(await confirm.isEnabled())) return false
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    /\/shortlist-versions\/[^/]+:select$/.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await confirm.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  await expect(page.getByRole('button', { name: 'Build client-ready media plan', exact: true })).toBeVisible()
  return true
}

async function buildMediaPlan(page: Page) {
  const button = page.getByRole('button', { name: 'Build client-ready media plan', exact: true })
  if (!(await button.isVisible()) || !(await button.isEnabled())) return false
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    /\/brief-versions\/[^/]+\/media-plans:generate$/.test(new URL(response.url()).pathname),
    { timeout: 30_000 },
  )
  await button.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Reconciled plan', exact: true })).toBeVisible()
  return true
}

async function prepareProposal(page: Page) {
  if (!/^\/briefs\/[^/]+\/proposals\/new$/.test(new URL(page.url()).pathname)) return false
  const plans = page.locator('.approved-plan-card')
  await expect(plans.first()).toBeVisible()
  if (!(await page.locator('.approved-plan-card[aria-pressed="true"]').count())) {
    await plans.first().click()
  }
  const choice = page.getByLabel('Choice name', { exact: true })
  if (await choice.isVisible() && !(await choice.inputValue()).trim()) {
    await choice.fill('Recommended Johannesburg OOH plan')
  }
  const outcome = page.getByRole('textbox', { name: 'Client outcome', exact: true })
  if (await outcome.isVisible() && !(await outcome.inputValue()).trim()) {
    await outcome.fill('Use the approved Johannesburg OOH and DOOH placements to support qualified enquiries, subject to supplier confirmation and final booking.')
  }
  const title = page.getByLabel('Proposal title', { exact: true })
  if (await title.isVisible()) await title.fill('Connected Marketplace OOH Proposal')
  const terms = page.getByRole('textbox', { name: 'Commercial terms', exact: true })
  if (await terms.isVisible()) {
    await terms.fill('Published Marketplace prices and availability remain subject to the exact supplier quote and booking confirmation. Funding and payment do not themselves confirm media delivery.')
  }
  return false
}

async function reviewMediaPlan(page: Page) {
  const reason = page.getByRole('textbox', { name: /^Review reason for / }).first()
  if (await reason.isVisible()) {
    if (!(await reason.inputValue()).trim()) {
      await reason.fill('Reviewed for connected acceptance. Retain this limitation visibly and require the missing supplier or measurement evidence before any unsupported claim is made.')
    }
    const card = reason.locator('xpath=ancestor::article[1]')
    const review = card.getByRole('button', { name: 'Review and accept', exact: true })
    if (await review.isVisible() && await review.isEnabled()) {
      const responsePromise = page.waitForResponse(response =>
        response.request().method() === 'POST' && /media-plan-versions/.test(new URL(response.url()).pathname),
        { timeout: 30_000 },
      )
      await review.click()
      const response = await responsePromise
      expect(response.status(), await response.text()).toBe(200)
      return true
    }
  }
  return false
}

async function nextAction(page: Page): Promise<Locator | null> {
  const patterns = [
    /^Research & build audience strategy$/i,
    /^Research & rebuild strategy$/i,
    /^Approve audience strategy & continue$/i,
    /^Next: Media Planning/i,
    /^Generate strategy recommendations$/i,
    /^Create strategy recommendations$/i,
    /^Regenerate strategy recommendations$/i,
    /^Approve channel recommendations$/i,
    /^Build media allocation$/i,
    /^Create media mix$/i,
    /^Save changes$/i,
    /^Confirm media mix$/i,
    /^Approve media plan$/i,
    /^Next: Proposal/i,
    /^Prepare proposal$/i,
    /^Create proposal$/i,
    /continue|next/i,
  ]
  for (let attempt = 0; attempt < 30; attempt += 1) {
    for (const pattern of patterns) {
      const candidates = page.locator('main').getByRole('button', { name: pattern })
        .or(page.locator('main').getByRole('link', { name: pattern }))
      for (const candidate of await candidates.all()) {
        if (await candidate.isVisible() && await candidate.isEnabled()) return candidate
      }
    }
    await page.waitForTimeout(100)
  }
  return null
}

async function proposalVisible(page: Page) {
  return /^\/proposals\/[0-9a-f-]{36}$/i.test(new URL(page.url()).pathname)
    && await page.getByRole('heading', { name: /proposal/i }).first().isVisible()
}

async function latestApprovedVersion(page: Page) {
  const response = await page.request.get(`/api/v1/tenants/${buyerTenantId}/briefs`)
  expect(response.ok(), await response.text()).toBe(true)
  const rows = await response.json() as Array<{ title: string; approvedVersionId: string | null }>
  const row = rows.find(item => item.title === title)
  expect(row?.approvedVersionId).toBeTruthy()
  return row!.approvedVersionId!
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
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

async function chooseWorkspace(page: Page, tenantId: string) {
  await page.evaluate((id) => sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId: id })), tenantId)
}

function mutationHeaders(token: string) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
  }
}
