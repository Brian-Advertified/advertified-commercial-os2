import { expect, test, type Page } from '@playwright/test'
import { prepareVisibleProposal, reviewVisibleMediaPlan } from './support/inventory-canary-review'
import { completeCanaryFlights, openCanaryBrief, populateCanaryFields, resolveCanaryAudienceEnrichment,
  resolveCanaryClarifications, selectCanaryInventory, signInCanary } from './support/inventory-canary-intake'
import { inspectSavedProposal, verifyNewProposal } from './support/inventory-canary-evidence'

test('connected digital OOH draft retains its approved planning lineage', async ({ page }) => {
  test.setTimeout(180_000)
  page.setDefaultTimeout(15_000)
  const observed = observeApi(page)
  const actionsTaken = new Set<string>()
  await signInCanary(page)
  const inspect = process.env.ADVERTIFIED_CANARY_INSPECT_BRIEF_VERSION
  if (inspect) {
    await inspectSavedProposal(page, inspect)
    return
  }
  const resume = process.env.ADVERTIFIED_CANARY_RESUME_BRIEF_VERSION
  await openCanaryBrief(page, resume)
  for (let step = 0; step < 35; step += 1) {
    await observed.settled()
    await populateCanaryFields(page)
    await resolveCanaryAudienceEnrichment(page)
    if (await resolveCanaryClarifications(page) || await completeCanaryFlights(page)
      || await reviewVisibleMediaPlan(page)) {
      await observed.settled()
      continue
    }
    if (await proposalIsVisible(page)) break
    await selectCanaryInventory(page)
    await prepareVisibleProposal(page)
    const action = await nextAction(page)
    expect(action, `No forward action at ${page.url()}`).not.toBeNull()
    const actionName = (await action!.innerText()).trim()
    const actionKey = `${page.url()} ${actionName}`
    expect(actionsTaken.has(actionKey), `Journey stalled instead of repeating: ${actionKey}`).toBe(false)
    actionsTaken.add(actionKey)
    console.log(`Journey step ${step + 1}: ${actionName} at ${page.url()}`)
    await action!.click()
    await observed.settled()
    const error = page.getByRole('alert').first()
    if (await error.isVisible()) throw new Error(`Visible application error: ${await error.innerText()}`)
  }
  await verifyNewProposal(page, observed.generatedIds, Boolean(resume))
})

function observeApi(page: Page) {
  const pending = new Set<object>()
  const captures: Array<Promise<void>> = []
  const failures: string[] = []
  const generatedIds: string[] = []
  page.on('request', request => {
    if (request.url().includes('/api/')) pending.add(request)
  })
  page.on('requestfinished', request => pending.delete(request))
  page.on('requestfailed', request => {
    pending.delete(request)
    if (request.url().includes('/api/')) failures.push(`Network failure: ${new URL(request.url()).pathname}`)
  })
  page.on('response', response => {
    const route = new URL(response.url()).pathname
    if (!route.startsWith('/api/')) return
    const method = response.request().method()
    const expectedEmpty = response.status() === 404 && method === 'GET' && route.endsWith('/intelligence/media-strategy')
    if (response.status() >= 400 && !expectedEmpty) {
      captures.push(response.text().then(body => { failures.push(`${response.status()} ${route} ${body}`) }))
    }
    if (response.ok() && method === 'POST' && route.endsWith('/proposals:generate')) {
      captures.push(response.json().then(value => { generatedIds.push(value.id) }))
    }
  })
  return { generatedIds, settled: async () => {
    await page.waitForTimeout(350)
    await expect.poll(() => pending.size, { timeout: 135_000,
      message: 'Wait for the active operation; never repeat paid or commercial commands.' }).toBe(0)
    await Promise.all(captures)
    expect(failures, failures.join('\n')).toEqual([])
    await page.waitForTimeout(200)
  } }
}

async function nextAction(page: Page) {
  const unavailable = page.getByRole('status').filter({ hasText: 'No channel recommendation is available yet.' })
  if (await unavailable.isVisible()) throw new Error('CONNECTED_JOURNEY_BLOCKED: No strategy recommendation was produced.')
  const patterns = [
    /^Approve media plan$/i, /^Next: Proposal/i, /^Create proposal$/i,
    /^Create revised strategy recommendations$/i,
    /^Approve channel recommendations$/i, /^Build media allocation$/i,
    /create.*brief|submit.*brief|understand.*brief|analyse|analyze|next.*ai interpretation/i,
    /review.*brief|approve.*brief|confirm.*brief/i,
    /research.*audience|discover.*audience|generate.*audience|approve.*audience/i,
    /generate.*strategy|approve.*strategy/i,
    /continue|next/i,
    /generate.*plan|create.*plan|start.*planning|build.*plan/i,
    /confirm.*inventory|approve.*inventory/i,
    /add placement|continue.*inventory|view.*inventory|find.*inventory|shortlist.*supply/i,
    /generate.*proposal|create.*proposal|continue.*proposal/i,
    /open.*proposal|view.*proposal/i,
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

async function proposalIsVisible(page: Page) {
  return /^\/proposals\/[0-9a-f-]{36}$/i.test(new URL(page.url()).pathname)
    && await page.getByRole('heading', { name: /proposal/i }).first().isVisible()
}
