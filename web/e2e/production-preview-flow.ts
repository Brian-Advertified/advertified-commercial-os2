import { expect, type Locator, type Page } from '@playwright/test'
import { mkdir, writeFile } from 'node:fs/promises'
import { join } from 'node:path'

export const baseUrl = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3017'
const tenantId = '10000000-0000-0000-0000-000000000002'
export const brianId = '10000000-0000-0000-0000-000000000001'
export const outputDirectory = join(process.cwd(), '..', 'artifacts', 'production-readiness', 'preview')

export type BriefScenario = {
  slug: string
  title: string
  client: string
  source: string
  inventoryPattern: RegExp
}

export async function signIn(page: Page) {
  await page.goto(baseUrl + '/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', {
    name: /Good (morning|afternoon|evening), Brian/,
  })).toBeVisible()
  const response = await page.request.get(
    '/api/v1/tenants/' + tenantId + '/commercial-policy',
  )
  expect(response.status(), await response.text()).toBe(200)
  const policy = await response.json() as {
    markupBasisPoints: number
    commissionBasisPoints: number
    vatRateBasisPoints: number
    allowSelfApproval: boolean
  }
  expect(policy).toMatchObject({
    markupBasisPoints: 1000,
    commissionBasisPoints: 500,
    vatRateBasisPoints: 1500,
    allowSelfApproval: true,
  })
}

export async function progressToProposal(page: Page, scenario: BriefScenario) {
  for (let step = 0; step < 45; step += 1) {
    await waitForSettled(page)
    await waitForBusyAction(page)
    await failOnVisibleError(page)
    await ensureRunningPeriods(page, scenario)
    await populateVisibleFields(page, scenario)
    await ensureScenarioChannels(page, scenario)
    await selectInventory(page, scenario.inventoryPattern)
    if (proposalIsVisible(page)) return
    const action = await nextAction(page)
    if (!action) {
      await page.waitForTimeout(1000)
      continue
    }
    await action.click()
    await waitForSettled(page)
    await failOnVisibleError(page)
  }
  throw new Error('The ' + scenario.slug + ' preview did not reach a proposal.')
}

export async function approveRenderAndSave(page: Page, slug: string) {
  const createPdf = page.getByRole('button', { name: /^Create branded PDF$/ })
  if (!await createPdf.isVisible().catch(() => false)) {
    const approve = page.getByRole('button', { name: /^Approve now$/ })
    await approve.waitFor({ state: 'visible', timeout: 60_000 })
    await approve.click()
  }
  await createPdf.waitFor({ state: 'visible', timeout: 60_000 })
  const renderResponse = page.waitForResponse(response =>
    response.request().method() === 'POST' &&
    response.url().includes(':render'),
  )
  await createPdf.click()
  const rendered = await renderResponse
  expect(rendered.status(), await rendered.text()).toBe(200)
  const link = page.getByRole('link', { name: /^Open proposal PDF$/ })
  await link.waitFor({ state: 'visible', timeout: 60_000 })
  const href = await link.getAttribute('href')
  expect(href).toBeTruthy()
  const response = await page.request.get(href!)
  expect(response.status(), await response.text()).toBe(200)
  const bytes = await response.body()
  const text = bytes.toString('ascii')
  expect(text).toContain('ADVERTIFIED')
  expect(text).toContain('COMMERCIAL MEDIA PROPOSAL')
  await mkdir(outputDirectory, { recursive: true })
  await writeFile(join(outputDirectory, slug + '.pdf'), bytes)
  await writeFile(
    join(outputDirectory, slug + '.json'),
    JSON.stringify({
      scenario: slug.startsWith('ooh') ? 'ooh' : 'full',
      reviewer: 'Brian',
      approvalMode: 'SELF',
      proposalUrl: page.url(),
      pdfBytes: bytes.length,
    }, null, 2) + '\n',
  )
}

async function ensureRunningPeriods(page: Page, scenario: BriefScenario) {
  if (!(await page.locator('input[type="date"]:visible').count())) {
    const add = page.getByRole('button', { name: /^\+ Add period$/ })
    const count = await add.count()
    for (let index = 0; index < count; index += 1) await add.nth(index).click()
  }
  const start = page.locator('label').filter({ hasText: /^Start$/ })
    .locator('input[type="date"]')
  for (let index = 0; index < await start.count(); index += 1) {
    if (!(await start.nth(index).inputValue())) {
      await start.nth(index).fill(scenarioDate(scenario, false))
    }
  }
  const end = page.locator('label').filter({ hasText: /^End$/ })
    .locator('input[type="date"]')
  for (let index = 0; index < await end.count(); index += 1) {
    if (!(await end.nth(index).inputValue())) {
      await end.nth(index).fill(scenarioDate(scenario, true))
    }
  }
}

async function populateVisibleFields(page: Page, scenario: BriefScenario) {
  const inputs = page.locator(
    'input:visible:not([type="checkbox"]):not([type="radio"])',
  )
  for (let index = 0; index < await inputs.count(); index += 1) {
    const input = inputs.nth(index)
    if (!(await input.isEditable()) || (await input.inputValue()).trim()) continue
    const type = (await input.getAttribute('type')) ?? 'text'
    const name = [
      await input.getAttribute('name'),
      await input.getAttribute('placeholder'),
      await input.getAttribute('aria-label'),
    ].join(' ').toLowerCase()
    const value = fieldValue(type, name, scenario)
    if (value !== null) await input.fill(value)
  }
}

function fieldValue(type: string, name: string, scenario: BriefScenario) {
  if (type === 'date') return scenarioDate(scenario, name.includes('end'))
  if (isBudgetField(type, name)) return scenarioBudget(scenario)
  if (/email/.test(name)) return 'preview@advertified.com'
  if (/client|company|advertiser|brand/.test(name)) return scenario.client
  if (/name|title/.test(name)) return scenario.title
  if (/approver.*id|reviewer.*id/.test(name)) return brianId
  return null
}

function isBudgetField(type: string, name: string) {
  return type === 'number' ? true : /budget|amount/.test(name)
}

function scenarioDate(scenario: BriefScenario, end: boolean) {
  const dates = scenario.slug.startsWith('ooh')
    ? ['2026-08-15', '2026-09-30']
    : ['2026-10-01', '2026-12-31']
  return dates[end ? 1 : 0]
}

function scenarioBudget(scenario: BriefScenario) {
  return scenario.slug.startsWith('ooh') ? '500000' : '900000'
}

async function ensureScenarioChannels(page: Page, scenario: BriefScenario) {
  if (!scenario.slug.startsWith('full')) return
  const confirm = page.getByRole('button', { name: /^Confirm media mix$/ })
  if (!await confirm.isVisible().catch(() => false)) return
  const social = page.getByRole('heading', { name: /^Social Media$/ })
  if (await social.isVisible().catch(() => false)) return

  const print = page.locator('article').filter({
    has: page.getByRole('heading', { name: /^Print$/ }),
  })
  const removePrint = print.getByRole('button', {
    name: /^Remove Print from media mix$/,
  })
  const replacementBudget = await removePrint.isVisible().catch(() => false)
    ? await print.getByLabel(/^Budget/).inputValue()
    : '0'
  if (await removePrint.isVisible().catch(() => false)) await removePrint.click()

  const channel = page.getByLabel('Add media type')
  await channel.selectOption('SOCIAL')
  await page.getByRole('button', { name: /^Add media type$/ }).click()
  const socialCard = page.locator('article').filter({
    has: page.getByRole('heading', { name: /^Social Media$/ }),
  })
  await socialCard.getByLabel(/^Budget/).fill(replacementBudget)
  await socialCard.getByLabel('Role in the plan').fill(
    'Source-priced social and platform reach for the approved audience.',
  )
}

async function selectInventory(page: Page, pattern: RegExp) {
  const confirm = page.getByRole('button', { name: /^Confirm selected inventory$/ })
  if (await canConfirm(confirm)) return
  const cards = page.locator('article').filter({
    has: page.locator('input[type="checkbox"]:visible:not([disabled])'),
  })
  const selectedChannels = await selectedInventoryChannels(cards)
  const ordered = [
    ...await matchingCardIndexes(cards, pattern),
    ...Array.from({ length: await cards.count() }, (_, index) => index),
  ]
  if (await selectMissingChannels(cards, ordered, selectedChannels, confirm)) return
  const loadMore = page.getByRole('button', { name: /^Load more inventory$/ })
  if (await loadMore.isVisible().catch(() => false)) await loadMore.click()
}

async function canConfirm(confirm: Locator) {
  return await confirm.isVisible().catch(() => false) && await confirm.isEnabled()
}

async function selectedInventoryChannels(cards: Locator) {
  const selected = new Set<string>()
  for (let index = 0; index < await cards.count(); index += 1) {
    const card = cards.nth(index)
    if (await card.locator('input[type="checkbox"]').isChecked()) {
      selected.add(inventoryChannel(await card.innerText()))
    }
  }
  return selected
}

async function selectMissingChannels(
  cards: Locator,
  ordered: number[],
  selected: Set<string>,
  confirm: Locator,
) {
  for (const index of [...new Set(ordered)]) {
    const card = cards.nth(index)
    const checkbox = card.locator('input[type="checkbox"]')
    const channel = inventoryChannel(await card.innerText())
    if (!channel || selected.has(channel) || await checkbox.isChecked()) continue
    await checkbox.check()
    selected.add(channel)
    if (await confirm.isEnabled()) return true
  }
  return false
}

async function matchingCardIndexes(cards: Locator, pattern: RegExp) {
  const indexes: number[] = []
  for (let index = 0; index < await cards.count(); index += 1) {
    if (pattern.test(await cards.nth(index).innerText())) indexes.push(index)
  }
  return indexes
}

function inventoryChannel(value: string) {
  return [
    'Social Media', 'Digital screens', 'Outdoor advertising', 'Digital',
    'Radio', 'Television', 'Print', 'Influencer', 'Experiential',
    'Podcast', 'Retail', 'Transit', 'Mall', 'Email', 'Mobile',
  ].find(channel => value.includes(channel)) ?? ''
}

async function waitForBusyAction(page: Page) {
  const busy = page.locator('button:disabled:visible').filter({
    hasText: /building|generating|preparing|creating|loading|submitting|approving|saving|starting|selecting|confirming|finding|reviewing|working/i,
  })
  for (let attempt = 0; attempt < 120; attempt += 1) {
    if (!(await firstVisible(busy))) return
    await page.waitForTimeout(1000)
  }
  throw new Error('A workflow action did not finish at ' + page.url() + '.')
}

async function nextAction(page: Page) {
  const patterns = [
    /review.*completed.*Brief/i,
    /approve Brief and start planning/i,
    /discover candidate audiences/i,
    /create media mix/i,
    /confirm media mix/i,
    /save changes/i,
    /build inventory shortlist/i,
    /review and accept/i,
    /approve media plan/i,
    /prepare proposal/i,
    /Plan \d+.*Select/i,
    /understand.*Brief|create.*Brief|submit.*Brief/i,
    /approve.*strategy|approve.*STP/i,
    /continue|next/i,
    /generate.*plan|create.*plan|start.*planning/i,
    /view.*inventory|find.*inventory|continue.*inventory/i,
    /confirm.*inventory|approve.*inventory|approve.*shortlist/i,
    /approve.*mix|approve.*plan/i,
    /generate.*proposal|create.*proposal|continue.*proposal/i,
    /open.*proposal|view.*proposal/i,
  ]
  for (const pattern of patterns) {
    const candidate = page.getByRole('button', { name: pattern })
      .or(page.getByRole('link', { name: pattern }))
    for (let index = 0; index < await candidate.count(); index += 1) {
      const item = candidate.nth(index)
      if (await item.isVisible() && await item.isEnabled()) return item
    }
  }
  return null
}

export async function clickWhenReady(page: Page, name: RegExp) {
  const item = await waitForVisible(page, page.getByRole('button', { name }))
  await item.click()
  await waitForSettled(page)
  await failOnVisibleError(page)
}

export async function fillWhenReady(page: Page, label: string, value: string) {
  const item = await waitForVisible(page, page.getByLabel(label))
  await item.fill(value)
}

async function waitForVisible(page: Page, locator: Locator) {
  for (let attempt = 0; attempt < 90; attempt += 1) {
    if (await firstVisible(locator)) return locator.first()
    await refresh(page)
  }
  throw new Error('Timed out waiting for a control at ' + page.url() + '.')
}

async function refresh(page: Page) {
  const button = page.getByRole('button', { name: /^Refresh$/ })
  if (await firstVisible(button)) await button.click()
  else await page.reload({ waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(1500)
}

async function waitForSettled(page: Page) {
  await page.waitForLoadState('domcontentloaded')
  await page.waitForTimeout(700)
}

function proposalIsVisible(page: Page) {
  return /\/proposals\/[0-9a-f-]{36}$/i.test(new URL(page.url()).pathname)
}

async function failOnVisibleError(page: Page) {
  const alert = page.getByRole('alert')
  if (!(await firstVisible(alert))) return
  const message = (await alert.first().innerText()).trim()
  if (/error|failed|unable|problem|invalid|unavailable|not configured|too many requests|something went wrong/i.test(message)) {
    throw new Error('Visible application error: ' + message)
  }
}

async function firstVisible(locator: Locator) {
  for (let index = 0; index < await locator.count(); index += 1) {
    if (await locator.nth(index).isVisible()) return true
  }
  return false
}
