import { expect, test, type Locator, type Page } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000040'
const buyerUserId = '10000000-0000-0000-0000-000000000001'
const briefId = 'ecf974b1-b8bb-4cbe-bcb6-61580ad4ddac'
const briefVersionId = 'b84ac418-4d38-4ef8-8f79-0680c03d7555'
const flight = { start: '2026-10-01', end: '2026-10-31' }

type Session = { antiforgeryToken: string }
type Planning = {
  mediaPlan: null | {
    id: string
    status: string
    lines: Array<{ marketplaceListingVersionId: string | null; inventoryTenantId: string }>
  }
}

test('resume Marketplace buyer from Strategy to Proposal with external supply lineage', async ({ page }) => {
  test.setTimeout(180_000)
  page.setDefaultTimeout(15_000)
  await signIn(page)
  await bootstrap(page)
  await switchIdentity(page, buyerUserId)
  await chooseWorkspace(page, tenantId)
  await page.goto(`/planning/${briefVersionId}#strategy`)
  await expect(page.getByRole('heading', { name: 'Strategy recommendations' })).toBeVisible()

  for (let step = 0; step < 50; step += 1) {
    if (await regenerateStrategy(page)) continue
    if (await completeFlights(page)) continue
    if (await selectInventory(page)) continue
    if (await reviewMediaPlan(page)) continue
    if (await prepareProposal(page)) continue
    if (await runGovernedCommand(page)) continue
    if (await proposalVisible(page)) break
    const action = await nextAction(page)
    expect(action, `No forward action at ${page.url()}`).not.toBeNull()
    const name = (await action!.innerText()).trim()
    console.log(`Marketplace resume step ${step + 1}: ${name}`)
    await action!.click()
    await page.waitForTimeout(250)
    const alert = page.getByRole('alert').first()
    if (await alert.isVisible()) {
      const text = await alert.innerText()
      if (!/latest state|loaded the latest state/i.test(text)) throw new Error(`Visible application error: ${text}`)
    }
  }

  expect(await proposalVisible(page), `Proposal was not reached: ${page.url()}`).toBe(true)
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`)
  expect(response.ok(), await response.text()).toBe(true)
  const planning = await response.json() as Planning
  expect(planning.mediaPlan?.status).toBe('APPROVED')
  expect(planning.mediaPlan?.lines.length).toBeGreaterThan(0)
  expect(planning.mediaPlan!.lines.every(line => Boolean(line.marketplaceListingVersionId))).toBe(true)
  expect(planning.mediaPlan!.lines.every(line => line.inventoryTenantId !== tenantId)).toBe(true)
})

async function regenerateStrategy(page: Page) {
  const button = page.getByRole('button', { name: /^Regenerate strategy recommendations$/i })
  if (!(await button.isVisible()) || !(await button.isEnabled())) return false
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    /\/brief-versions\/[^/]+\/intelligence\/media-strategy$/.test(new URL(response.url()).pathname),
    { timeout: 120_000 },
  )
  await button.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  await expect(page.getByRole('button', { name: /^Approve channel recommendations$/i })).toBeVisible({ timeout: 15_000 })
  return true
}

async function completeFlights(page: Page) {
  const missing = page.getByRole('button', { name: /^Edit flight dates for/ }).filter({ hasText: 'Set flight dates' })
  if (!(await missing.first().isVisible())) return false
  await missing.first().click()
  const dialog = page.getByRole('dialog')
  const add = dialog.getByRole('button', { name: '+ Add flight period', exact: true })
  if (await add.isVisible()) await add.click()
  await dialog.getByLabel('Start date', { exact: true }).fill(flight.start)
  await dialog.getByLabel('End date', { exact: true }).fill(flight.end)
  const all = dialog.getByRole('checkbox', { name: 'Apply these dates to all channels without flight dates' })
  if (await all.isVisible()) await all.check()
  await dialog.getByRole('button', { name: 'Save flight changes', exact: true }).click()
  await expect(dialog).toHaveCount(0)
  return true
}

async function selectInventory(page: Page) {
  if (!new URL(page.url()).pathname.startsWith('/planning/')) return false
  const summary = page.locator('summary').filter({ hasText: /^Review shortlist and alternatives$/ })
  if (!(await summary.isVisible())) return false
  if (await summary.locator('..').getAttribute('open') === null) await summary.click()
  const reason = page.getByRole('textbox', { name: /^Why are you carrying these placements forward\?/ })
  if (!(await reason.isVisible())) return false
  if (!(await page.locator('.shortlist-card input[type="checkbox"]:checked').count())) {
    const option = page.locator('.shortlist-card input[type="checkbox"]:not([disabled])').first()
    await expect(option, 'At least one eligible Marketplace placement is required').toBeVisible()
    await option.check()
  }
  await reason.fill('Select the current eligible published Marketplace placement for the approved Johannesburg OOH plan. Supplier confirmation remains required before booking.')
  const confirm = page.getByRole('button', { name: /Confirm selected inventory/i })
  if (await confirm.isVisible() && await confirm.isEnabled()) {
    await confirm.click()
    return true
  }
  return false
}

async function reviewMediaPlan(page: Page) {
  const field = page.getByRole('textbox', { name: /^Review reason for / }).first()
  if (await field.isVisible()) {
    if (!(await field.inputValue()).trim()) {
      await field.fill('Reviewed for connected production acceptance. Keep this limitation visible and obtain the required supplier or measurement evidence before booking or reporting.')
    }
    const card = field.locator('xpath=ancestor::article[1]')
    const button = card.getByRole('button', { name: 'Review and accept', exact: true })
    if (await button.isVisible() && await button.isEnabled()) {
      await button.click()
      return true
    }
  }
  const review = page.locator('button:not([disabled])', { hasText: 'Review and accept' }).first()
  if (await review.isVisible()) {
    await review.click()
    return true
  }
  return false
}

async function prepareProposal(page: Page) {
  if (!/^\/briefs\/[^/]+\/proposals\/new$/.test(new URL(page.url()).pathname)) return false
  const choices = page.locator('.approved-plan-card')
  await expect(choices.first()).toBeVisible()
  if (!(await page.locator('.approved-plan-card[aria-pressed="true"]').count())) await choices.first().click()
  const choice = page.getByLabel('Choice name', { exact: true })
  if (await choice.isVisible() && !(await choice.inputValue()).trim()) await choice.fill('Recommended Johannesburg OOH plan')
  const outcome = page.getByRole('textbox', { name: 'Client outcome', exact: true })
  if (await outcome.isVisible() && !(await outcome.inputValue()).trim()) {
    await outcome.fill('Use the approved Johannesburg OOH placement to support the supplied awareness and qualified-enquiry objective, subject to supplier confirmation.')
  }
  const title = page.getByLabel('Proposal title', { exact: true })
  if (await title.isVisible() && !(await title.inputValue()).trim()) await title.fill('Connected Marketplace OOH Proposal')
  const terms = page.getByRole('textbox', { name: 'Commercial terms', exact: true })
  if (await terms.isVisible() && !(await terms.inputValue()).trim()) {
    await terms.fill('Published Marketplace prices and availability remain subject to the exact supplier quote and booking confirmation. Funding and payment do not themselves confirm media delivery.')
  }
  return false
}

async function runGovernedCommand(page: Page) {
  const commands: Array<{ name: RegExp; route: RegExp }> = [
    { name: /^Build media allocation$/i, route: /\/brief-versions\/[^/]+\/media-mixes:generate$/ },
    { name: /^Save changes$/i, route: /\/media-mix-versions\/[^/]+:update$/ },
    { name: /^Confirm media mix$/i, route: /\/media-mix-versions\/[^/]+:approve$/ },
    { name: /^Build inventory shortlist$/i, route: /\/brief-versions\/[^/]+\/shortlists:generate$/ },
    { name: /^＋?\s*Add Placement$/i, route: /\/brief-versions\/[^/]+\/shortlists:generate$/ },
    { name: /^Create media plan$/i, route: /\/brief-versions\/[^/]+\/media-plans:generate$/ },
    { name: /^Approve media plan$/i, route: /\/media-plan-versions\/[^/]+:approve$/ },
    { name: /^Create proposal$/i, route: /\/briefs\/[^/]+\/proposals:generate$/ },
  ]
  for (const command of commands) {
    const button = page.getByRole('button', { name: command.name })
    if (!(await button.isVisible()) || !(await button.isEnabled())) continue
    const responsePromise = page.waitForResponse(response =>
      response.request().method() === 'POST' && command.route.test(new URL(response.url()).pathname),
      { timeout: 120_000 },
    )
    console.log(`Marketplace governed command: ${(await button.innerText()).trim()}`)
    await button.click()
    const response = await responsePromise
    expect(response.status(), await response.text()).toBe(200)
    await page.waitForTimeout(250)
    return true
  }
  return false
}

async function nextAction(page: Page): Promise<Locator | null> {
  const patterns = [
    /^Regenerate strategy recommendations$/i,
    /^Approve channel recommendations$/i,
    /^Build media allocation$/i,
    /^Create media mix$/i,
    /^Save changes$/i,
    /^Confirm media mix$/i,
    /^Build inventory shortlist$/i,
    /^＋?\s*Add Placement$/i,
    /^Create media plan$/i,
    /^Approve media plan$/i,
    /^Next: Proposal/i,
    /^Prepare proposal$/i,
    /^Create proposal$/i,
    /continue|next/i,
  ]
  for (const pattern of patterns) {
    const candidates = page.locator('main').getByRole('button', { name: pattern })
      .or(page.locator('main').getByRole('link', { name: pattern }))
    for (const candidate of await candidates.all()) {
      if (await candidate.isVisible() && await candidate.isEnabled()) return candidate
    }
  }
  return null
}

async function proposalVisible(page: Page) {
  return /^\/proposals\/[0-9a-f-]{36}$/i.test(new URL(page.url()).pathname)
    && await page.getByRole('heading', { name: /proposal/i }).first().isVisible()
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
