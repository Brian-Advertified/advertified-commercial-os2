import { expect, type Page } from '@playwright/test'

// Explicit test-only dispositions for observed, reviewable planning risks. Unknown
// objections must stop the canary; this does not assert supplier confirmation.
const reviewReasons: Readonly<Record<string, string>> = {
  'Review reason for commercial evidence incomplete':
    'Internal draft verification only: the published rate has no explicit validity period. Retain the supplied amount as indicative; obtain dated supplier confirmation before any client release or booking.',
  'Review reason for benchmark insufficient':
    'Internal draft verification only: fewer than three comparable local rates means no reliable value benchmark. Retain the published placement for comparison without claiming best value.',
}

export async function reviewVisibleMediaPlan(page: Page): Promise<boolean> {
  const plan = page.getByRole('region', { name: 'Reconciled plan', exact: true })
  if (!(await plan.isVisible())) return false
  const reasons = plan.getByRole('textbox', { name: /^Review reason for / })
  if (!(await reasons.count())) return false
  const field = reasons.first()
  const label = await field.evaluate(element =>
    (element as HTMLTextAreaElement).labels?.[0]?.textContent?.trim() ?? '')
  const reason = reviewReasons[label]
  expect(reason, `Unreviewed business objection needs a deliberate disposition: ${label}`).toBeTruthy()
  await field.fill(reason)
  const card = field.locator('xpath=ancestor::article[1]')
  const responsePromise = page.waitForResponse(response => response.request().method() === 'POST'
    && /\/media-plan-versions\/[^/]+\/objections\/[^/]+:resolve$/.test(new URL(response.url()).pathname))
  await card.getByRole('button', { name: 'Review and accept', exact: true }).click()
  const response = await responsePromise
  expect(response.status(), await response.text()).toBe(200)
  return true
}

export async function prepareVisibleProposal(page: Page): Promise<void> {
  if (!/^\/briefs\/[^/]+\/proposals\/new$/.test(new URL(page.url()).pathname)) return
  const choices = page.locator('.approved-plan-card')
  await expect(choices.first()).toBeVisible()
  if (!(await page.locator('.approved-plan-card[aria-pressed="true"]').count())) {
    await choices.first().click()
  }
  await page.getByLabel('Choice name', { exact: true }).fill('Published digital OOH draft option')
  await page.getByRole('textbox', { name: 'Client outcome', exact: true }).fill(
    'Draft digital OOH option for review. This selected supply does not yet demonstrate complete coverage of every requested city or preferred mall. No measured reach, supplier booking or sales outcome is claimed.')
  await page.getByLabel('Proposal title', { exact: true }).fill('Takealot Black Friday — internal draft review')
  await page.getByRole('textbox', { name: 'Commercial terms', exact: true }).fill(
    'INTERNAL DRAFT ONLY. Published prices are indicative where rate validity is not supplied. Confirm rates, flight availability, city coverage and preferred malls before client release. No supplier commitment, client acceptance or payment has been made.')
}
