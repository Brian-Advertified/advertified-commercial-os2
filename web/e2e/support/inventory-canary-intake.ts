import { expect, type Locator, type Page } from '@playwright/test'
import { suppliedBriefUnderstandingSchema } from '../../src/api/brief-understanding-schemas'

export const canaryScope = {
  tenantId: '10000000-0000-0000-0000-000000000002',
  cities: ['Johannesburg', 'Cape Town', 'Durban'],
  start: '2026-11-04', end: '2026-11-28', budgetMinor: 32_000_000,
} as const

const brief = `Takealot Black Friday OOH campaign.
Budget: R320,000 excluding VAT, with approval to increase to R400,000.
Flight dates: 4 November 2026 to 28 November 2026.
Digital OOH only in Johannesburg, Cape Town and Durban.
Prioritise Mall of Africa, Sandton City, Gateway, Cavendish and Menlyn.
Audience: online shoppers aged 18-54, families and deal seekers.
Objective: drive Black Friday awareness and online purchases.
A human must approve the final inventory before proposal release.`

export async function signInCanary(page: Page) {
  await page.goto('/sign-in', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: /Good (morning|afternoon|evening), Brian/ })).toBeVisible()
}

export async function openCanaryBrief(page: Page, resume?: string) {
  if (resume) {
    await page.goto(`/stp/${resume}`)
    await expect(page.getByRole('heading', { name: 'Audience & STP', exact: true })).toBeVisible()
    return
  }
  await page.goto('/briefs/new')
  await page.locator('input[name="sourceTitle"]').fill('Takealot Black Friday OOH Canary')
  await page.locator('textarea[name="sourceContent"]').fill(brief)
  await page.getByRole('combobox', { name: 'Budget range (ZAR)' }).selectOption('confirmed')
  await page.getByRole('spinbutton', { name: 'Confirmed budget (ZAR)' }).fill('320000')
  await populateCanaryFields(page)
}

export async function resolveCanaryClarifications(page: Page): Promise<boolean> {
  const review = page.getByRole('button', { name: /Review the completed Brief/i })
  if (!(await review.isVisible())) return false
  const responsePromise = page.waitForResponse(response => response.url().includes('/briefs:understand')
    && response.request().method() === 'POST')
  await review.click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  const result = suppliedBriefUnderstandingSchema.parse(await response.json())
  expect(result.requiresHumanClarification, JSON.stringify(result.questions)).toBe(false)
  expect(result.campaignMode).toBe('OOH_ONLY')
  expect(result.draft.budgetUnknown).toBe(false)
  expect(result.draft.budgetMinor).toBe(canaryScope.budgetMinor)
  expect(result.draft.currency).toBe('ZAR')
  await expect(page.getByRole('heading', { name: 'AI brief interpretation' })).toBeVisible()
  return true
}

export async function populateCanaryFields(page: Page) {
  if (new URL(page.url()).pathname !== '/briefs/new') return
  for (const input of await page.locator('main input:visible').all()) await fillBriefInput(input)
  const oohMode = page.getByRole('radio', { name: /Outdoor advertising and digital screens only/ })
  if (await oohMode.isVisible() && !(await oohMode.isChecked())) await oohMode.check()
}

async function fillBriefInput(input: Locator) {
  if (!(await input.isEditable()) || (await input.inputValue()).trim()) return
  const hints = await Promise.all(['name', 'placeholder', 'aria-label'].map(key => input.getAttribute(key)))
  const name = hints.filter(Boolean).join(' ').toLowerCase()
  const fields: Array<[RegExp, string]> = [
    [/budget|amount/, '320000'], [/client|company|advertiser|brand/, 'Takealot'],
    [/businessproblem|business problem|problem/, 'Drive Black Friday awareness and online purchases.'],
    [/geograph|location|market/, canaryScope.cities.join(', ')],
    [/timing|flight|running period/, '4 November 2026 to 28 November 2026'],
    [/name|title/, 'Takealot Black Friday OOH Canary'],
  ]
  const value = fields.find(([pattern]) => pattern.test(name))?.[1]
  if (value !== undefined) await input.fill(value)
}

export async function completeCanaryFlights(page: Page): Promise<boolean> {
  const missing = page.getByRole('button', { name: /^Edit flight dates for/ }).filter({ hasText: 'Set flight dates' })
  if (!(await missing.first().isVisible())) return false
  await missing.first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('button', { name: '+ Add flight period', exact: true }).click()
  await dialog.getByLabel('Start date', { exact: true }).fill(canaryScope.start)
  await dialog.getByLabel('End date', { exact: true }).fill(canaryScope.end)
  const scope = dialog.getByRole('checkbox', { name: 'Apply these dates to all channels without flight dates' })
  if (await scope.isVisible()) await scope.check()
  const saved = page.waitForResponse(response => response.request().method() === 'POST'
    && response.url().includes('/media-mix-versions/') && response.url().endsWith(':update'))
  await dialog.getByRole('button', { name: 'Save flight changes', exact: true }).click()
  expect((await saved).status()).toBe(200)
  await expect(dialog).toHaveCount(0)
  return true
}

export async function resolveCanaryAudienceEnrichment(page: Page) {
  if (!new URL(page.url()).pathname.startsWith('/stp/')) return
  const approval = page.locator('.audience-strategy-approval.needs-enrichment')
  if (!(await approval.isVisible())) return
  const basis = page.locator('details.connected-strategy-basis')
  if (await basis.getAttribute('open') === null) await basis.locator('summary').click()
  const positioning = basis.getByLabel('Positioning direction')
  if (await positioning.isVisible() && !(await positioning.inputValue()).trim()) {
    await positioning.fill('Hypothesis: Position the Black Friday campaign around the approved awareness and online-purchase objective. Validate the client proposition before external release.')
  }
}

export async function selectCanaryInventory(page: Page) {
  const url = new URL(page.url())
  if (!url.pathname.startsWith('/planning/') || url.hash === '#strategy') return
  const summary = page.locator('summary').filter({ hasText: /^Review shortlist and alternatives$/ })
  if (!(await summary.isVisible())) return
  if (await summary.locator('..').getAttribute('open') === null) await summary.click()
  const reason = page.getByRole('textbox', { name: /^Why are you carrying these placements forward\?/ })
  if (!(await reason.isVisible())) return
  await reason.fill('Internal draft verification: select eligible published digital OOH in all three requested cities. Preferred malls, dated rates and delivery evidence remain unconfirmed; no external release or booking.')
  for (const city of canaryScope.cities) {
    const cards = page.locator('.shortlist-card').filter({
      has: page.locator('p').filter({ hasText: new RegExp('^' + city + '(?: CBD)?$', 'i') }),
    })
    if (await cards.locator('input[type="checkbox"]:checked').count()) continue
    const option = cards.locator('input[type="checkbox"]:not([disabled])').first()
    await expect(option, `No eligible published digital supply for ${city}`).toBeVisible()
    await option.check()
  }
}
