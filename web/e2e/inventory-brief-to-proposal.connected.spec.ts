import { expect, test, type Locator, type Page, type Response } from '@playwright/test'

const baseUrl = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3017'
const brief = `
Takealot Black Friday rapid OOH campaign.
Budget: R320,000 excluding VAT, with approval to increase to R400,000.
Flight dates: 4 November 2026 to 28 November 2026.
Digital OOH only in Johannesburg, Cape Town and Durban.
Prioritise Mall of Africa, Sandton City, Gateway, Cavendish and Menlyn.
Audience: online shoppers aged 18-54, families and deal seekers.
Objective: drive Black Friday awareness and online purchases.
A human must approve the final inventory before proposal release.
`.trim()

test.describe('published inventory brief-to-proposal canary', () => {
  test('creates a rapid OOH proposal from published corpus inventory', async ({ page }) => {
    test.setTimeout(240_000)
    const inventoryPayloads: unknown[] = []
    const proposalPayloads: unknown[] = []
    page.on('response', (response) => {
      void capture(response, inventoryPayloads, proposalPayloads)
    })

    await openBriefIntake(page)
    await populateVisibleFields(page)
    await fillBrief(page)

    for (let step = 0; step < 35; step += 1) {
      await page.waitForLoadState('domcontentloaded')
      await page.waitForTimeout(350)
      await populateVisibleFields(page)
      await selectFirstInventory(page)

      if (await proposalIsVisible(page)) break
      const action = await nextAction(page)
      expect(action, `No forward action was available at ${page.url()}`).not.toBeNull()
      await action!.click()
      await page.waitForTimeout(700)
      await failOnVisibleError(page)
    }

    await expect(page).toHaveURL(/proposal|proposals/i, { timeout: 30_000 })
    await expect(
      page.getByRole('heading', { name: /proposal/i }).first(),
    ).toBeVisible({ timeout: 30_000 })

    const inventoryItems = inventoryPayloads.reduce(
      (total, payload) => total + countInventory(payload),
      0,
    )
    expect(
      inventoryItems,
      'The connected journey did not receive any published inventory.',
    ).toBeGreaterThan(0)
    expect(
      proposalPayloads.length,
      'The connected journey did not create or load a proposal payload.',
    ).toBeGreaterThan(0)

    const body = await page.locator('body').innerText()
    expect(body).not.toMatch(/fixture|deterministic-zero-cost|internal gate/i)
    expect(body).toMatch(/OOH|out.of.home|digital screen|billboard/i)
  })
})

async function openBriefIntake(page: Page) {
  for (const path of ['/briefs/new', '/briefs', '/']) {
    await page.goto(`${baseUrl}${path}`, { waitUntil: 'domcontentloaded' })
    if (await firstVisible(page.locator('textarea'))) return
    const start = page.getByRole('button', { name: /start.*brief|new.*brief/i })
      .or(page.getByRole('link', { name: /start.*brief|new.*brief/i }))
    if (await firstVisible(start)) {
      await start.first().click()
      if (await firstVisible(page.locator('textarea'))) return
    }
  }
  throw new Error('The production brief intake could not be opened.')
}

async function fillBrief(page: Page) {
  const textareas = page.locator('textarea:visible')
  expect(await textareas.count()).toBeGreaterThan(0)
  const target = textareas.first()
  await target.fill(brief)
}

async function populateVisibleFields(page: Page) {
  const inputs = page.locator('input:visible')
  const count = await inputs.count()
  for (let index = 0; index < count; index += 1) {
    const input = inputs.nth(index)
    if (!(await input.isEditable())) continue
    const value = await input.inputValue()
    if (value.trim()) continue
    const type = (await input.getAttribute('type')) ?? 'text'
    const name = `${await input.getAttribute('name') ?? ''} ${await input.getAttribute('placeholder') ?? ''} ${await input.getAttribute('aria-label') ?? ''}`.toLowerCase()
    const content = visibleFieldValue(type, name)
    if (content !== null) await input.fill(content)
  }
}

function visibleFieldValue(type: string, name: string): string | null {
  if (type === 'date') return name.includes('end') ? '2026-11-28' : '2026-11-04'
  if (type === 'number' || /budget|amount/.test(name)) return '320000'
  if (/email/.test(name)) return 'production-canary@advertified.com'
  if (/client|company|advertiser|brand/.test(name)) return 'Advertified Production Canary'
  if (/name|title/.test(name)) return 'Takealot Black Friday OOH Canary'
  return null
}

async function selectFirstInventory(page: Page) {
  const checked = page.locator('input[type="checkbox"]:checked:visible')
  if (await checked.count()) return
  const checkbox = page.locator('input[type="checkbox"]:visible:not([disabled])')
  if (await checkbox.count()) {
    await checkbox.first().check()
    return
  }
  const select = page.getByRole('button', { name: /select|add.*plan|use.*inventory/i })
  if (await firstVisible(select)) await select.first().click()
}

async function nextAction(page: Page) {
  const patterns = [
    /create.*brief|submit.*brief|analyse|analyze|interpret/i,
    /approve.*brief|confirm.*brief/i,
    /continue|next/i,
    /generate.*plan|create.*plan|start.*planning/i,
    /continue.*inventory|view.*inventory|find.*inventory/i,
    /confirm.*inventory|approve.*inventory/i,
    /generate.*proposal|create.*proposal|continue.*proposal/i,
    /open.*proposal|view.*proposal/i,
  ]
  for (const pattern of patterns) {
    const candidate = page.getByRole('button', { name: pattern })
      .or(page.getByRole('link', { name: pattern }))
    const count = await candidate.count()
    for (let index = 0; index < count; index += 1) {
      const item = candidate.nth(index)
      if (await item.isVisible() && await item.isEnabled()) return item
    }
  }
  return null
}

async function proposalIsVisible(page: Page) {
  return /proposal|proposals/i.test(new URL(page.url()).pathname)
    && await firstVisible(page.getByRole('heading', { name: /proposal/i }))
}

async function failOnVisibleError(page: Page) {
  const alert = page.getByRole('alert')
  if (!(await firstVisible(alert))) return
  const text = (await alert.first().innerText()).trim()
  if (/error|failed|unable|problem|invalid/i.test(text)) {
    throw new Error(`Visible application error: ${text}`)
  }
}

async function capture(
  response: Response,
  inventoryPayloads: unknown[],
  proposalPayloads: unknown[],
) {
  if (!response.ok()) return
  const url = response.url().toLowerCase()
  if (!url.includes('/api/')) return
  if (!url.includes('inventory') && !url.includes('proposal')) return
  const contentType = response.headers()['content-type'] ?? ''
  if (!contentType.includes('json')) return
  try {
    const payload = await response.json()
    if (url.includes('inventory')) inventoryPayloads.push(payload)
    if (url.includes('proposal')) proposalPayloads.push(payload)
  } catch {
    // A successful empty response is not evidence for either assertion.
  }
}

function countInventory(value: unknown): number {
  if (Array.isArray(value)) return value.length
  if (!value || typeof value !== 'object') return 0
  const object = value as Record<string, unknown>
  for (const key of ['items', 'products', 'inventory', 'candidates', 'results']) {
    if (Array.isArray(object[key])) return object[key].length
  }
  for (const key of ['data', 'value']) {
    const nested = countInventory(object[key])
    if (nested > 0) return nested
  }
  return 0
}

async function firstVisible(locator: Locator) {
  const count = await locator.count()
  for (let index = 0; index < count; index += 1) {
    if (await locator.nth(index).isVisible()) return true
  }
  return false
}
