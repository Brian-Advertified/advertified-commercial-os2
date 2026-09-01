import { expect, test } from '@playwright/test'

const localTenantId = '10000000-0000-0000-0000-000000000002'
const planningStart = '2026-10-01'
const planningEnd = '2026-10-31'

test('connected Brief becomes an approved, rendered and shared proposal', async ({ page }) => {
  test.setTimeout(90_000)
  const failedApiResponses: string[] = []
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failedApiResponses.push(`${response.status()} ${new URL(response.url()).pathname}`)
    }
  })

  await signIn(page)

  const inventoryResponse = await page.request.get(
    `/api/v1/tenants/${localTenantId}/inventory-products?pageSize=24`,
  )
  expect(inventoryResponse.status(), await inventoryResponse.text()).toBe(200)
  const catalogue = await inventoryResponse.json() as { items: unknown[] }
  expect(catalogue.items, 'Proposal planning requires reviewed, published inventory.')
    .not.toHaveLength(0)

  const proposalTitle = `Connected proposal ${Date.now()}`
  await createClearBrief(page, proposalTitle)
  await prepareApprovedPlan(page)

  await page.getByRole('link', { name: 'Prepare proposal' }).click()
  await expect(page.getByRole('heading', {
    name: 'Build clear choices from approved plans',
  })).toBeVisible()
  await page.locator('button.approved-plan-card').first().click()
  await page.getByLabel('Proposal title').fill(proposalTitle)
  await page.getByRole('button', { name: 'Create proposal' }).click()

  await expect(page.getByRole('heading', { name: proposalTitle })).toBeVisible()
  await page.getByLabel('Executive summary').fill(
    'A source-linked Johannesburg OOH route for qualified local demand.',
  )
  await page.getByRole('button', { name: 'Save wording' }).click()
  await page.getByRole('button', { name: 'Approve proposal' }).click()
  await page.getByRole('button', { name: 'Create branded PDF' }).click()

  const pdfLink = page.getByRole('link', { name: 'Open proposal PDF' })
  await expect(pdfLink).toBeVisible()
  const pdfHref = await pdfLink.getAttribute('href')
  expect(pdfHref).toBeTruthy()
  const pdfResponse = await page.request.get(pdfHref!)
  expect(pdfResponse.status(), await pdfResponse.text()).toBe(200)
  expect(pdfResponse.headers()['content-type']).toContain('application/pdf')
  expect((await pdfResponse.body()).subarray(0, 4).toString()).toBe('%PDF')

  await page.getByLabel('Client recipient').selectOption({
    label: 'Local Client Approver · client.approver@advertified.local',
  })
  await page.getByRole('button', { name: 'Share with client' }).click()
  await expect(page.getByText('Waiting for the client decision')).toBeVisible()

  expect(failedApiResponses).toEqual([])
})

async function signIn(page: import('@playwright/test').Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to local workspace/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()
}

async function createClearBrief(
  page: import('@playwright/test').Page,
  title: string,
) {
  await page.goto('/briefs/new')
  await page.getByLabel('Campaign or Brief name').fill(title)
  await page.getByLabel('Original Brief').fill([
    'Client: Local Proposal Client',
    'Problem: Local business buyers do not know about the new workspace range.',
    'Objective: Generate 500 qualified enquiries.',
    'Audience: Small business owners and office managers.',
    'Geography: Johannesburg.',
    `Timing: ${planningStart} to ${planningEnd}.`,
    'Budget: ZAR 100,000 including VAT.',
    'Media: OOH and DOOH only.',
    'Measurement: Qualified enquiries.',
  ].join('\n'))
  await page.getByRole('button', { name: 'Understand this Brief' }).click()

  await expect(page).toHaveURL(/\/planning\/[0-9a-f-]{36}$/, {
    timeout: 30_000,
  })
  await expect(page.getByRole('heading', { name: 'Planning workbench' }))
    .toBeVisible()
  await expect(page.getByRole('heading', { name: 'OOH and DOOH only' }))
    .toBeVisible()
}

async function prepareApprovedPlan(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: 'Build audience direction' }).click()
  await expect(page.getByRole('heading', {
    name: 'Audience strategy for this campaign',
  })).toBeVisible()

  await page.getByRole('button', { name: 'Create media mix' }).click()
  await expect(page.getByRole('heading', {
    name: 'Shape the investment and timing',
  })).toBeVisible()

  const cards = page.locator('.media-allocation-card')
  const count = await cards.count()
  for (let index = 0; index < count; index += 1) {
    const card = cards.nth(index)
    if (await card.getByLabel('Start').count() === 0) {
      await card.getByRole('button', { name: '+ Add period' }).click()
    }
    await card.getByLabel('Start').first().fill(planningStart)
    await card.getByLabel('End').first().fill(planningEnd)
  }
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByRole('button', { name: 'Confirm media mix' })).toBeEnabled()
  await page.getByRole('button', { name: 'Confirm media mix' }).click()

  await page.getByRole('button', { name: 'Build inventory shortlist' }).click()
  await expect(page.getByRole('heading', {
    name: 'Choose the placements to carry forward',
  })).toBeVisible()
  const inventoryRationale = page.locator('.inventory-rationale').first()
  await expect(inventoryRationale).toContainText('Inventory Intelligence:')
  await expect(inventoryRationale).toContainText('eligible after governed hard constraints')
  await page.getByLabel(
    'Select Local Demo Johannesburg Digital Billboard',
  ).check()
  await page.getByRole('button', { name: 'Confirm selected inventory' }).click()

  await page.getByRole('button', { name: 'Create media plan' }).click()
  await expect(page.getByRole('heading', { name: 'Reconciled plan' })).toBeVisible()
  for (let index = 0; index < 5; index += 1) {
    const review = page.locator('button:not([disabled])', {
      hasText: 'Review and accept',
    })
    const approve = page.locator('button:not([disabled])', {
      hasText: 'Approve media plan',
    })
    await expect(review.first().or(approve)).toBeVisible()
    if (await approve.count() > 0) break
    await review.first().click()
  }
  await expect(page.getByRole('button', { name: 'Approve media plan' }))
    .toBeEnabled()
  await page.getByRole('button', { name: 'Approve media plan' }).click()
  await expect(page.getByText(
    'Media plan approved and ready for proposal preparation.',
  )).toBeVisible()
}
