import { expect, test, type Page } from '@playwright/test'

test('connected clear Brief reaches OOH planning without a fake approval', async ({ page }) => {
  const failedApiResponses: string[] = []
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failedApiResponses.push(`${response.status()} ${new URL(response.url()).pathname}`)
    }
  })

  await signIn(page)
  await expectLocalSelfApproval(page)

  await page.goto('/briefs/new')
  await expect(page.getByRole('heading', {
    name: 'Start with the Brief, not a form',
  })).toBeVisible()
  await fillBriefSource(page, 'OOH and DOOH only.')
  await page.getByRole('button', { name: 'Understand this Brief' }).click()
  await expect(page.getByRole('heading', {
    name: 'Confirm what Advertified understood before planning begins.',
  })).toBeVisible()
  await page.getByRole('button', { name: 'Approve Brief and start planning' }).click()

  await expect(page).toHaveURL(/\/stp\/[0-9a-f-]{36}$/, { timeout: 30_000 })
  await expect(page.getByRole('heading', { name: 'Strategy & STP' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'OOH_ONLY')
  await expect(page.getByRole('button', { name: /Approve Brief/ })).toHaveCount(0)

  await page.getByRole('link', { name: /Back to Brief/ }).click()
  await expect(page.getByText('Approved', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Campaign type')).toHaveValue('OOH / DOOH only')
  await expect(page.getByLabel('Decision source')).toHaveValue('Supplied Brief Evidence')
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'OOH_ONLY')

  const briefUrl = page.url().split('#')[0]
  await page.goto(`${briefUrl}#brief-objectives`)
  await expect(page.getByRole('heading', { name: 'Objectives', exact: true }))
    .toBeVisible()
  await page.getByRole('button', { name: 'Continue to Audience →' }).click()
  await expect(page).toHaveURL(/#brief-audience$/)
  expect(failedApiResponses).toEqual([])
})

test('connected mixed-channel Brief reaches the Full Campaign flow', async ({ page }) => {
  await signIn(page)
  await expectLocalSelfApproval(page)
  await page.goto('/briefs/new')
  await fillBriefSource(page, 'OOH billboards and radio.')
  await page.getByRole('button', { name: 'Understand this Brief' }).click()
  await expect(page.getByRole('heading', {
    name: 'Confirm what Advertified understood before planning begins.',
  })).toBeVisible()
  await page.getByRole('button', { name: 'Approve Brief and start planning' }).click()

  await expect(page).toHaveURL(/\/stp\/[0-9a-f-]{36}$/, { timeout: 30_000 })
  await expect(page.getByRole('region', { name: 'Full Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', 'FULL_CAMPAIGN')
  await page.getByRole('link', { name: /Back to Brief/ }).click()
  await expect(page.getByLabel('Campaign type')).toHaveValue('Full campaign')
})

test('connected persisted Brief rail follows its canonical campaign mode', async ({ page }) => {
  const briefId = process.env.ADVERTIFIED_CONNECTED_BRIEF_ID
  const expectedMode = process.env.ADVERTIFIED_CONNECTED_CAMPAIGN_MODE
  test.skip(!briefId || !expectedMode,
    'Set the connected Brief id and expected canonical campaign mode to run this check.')
  if (!briefId || !expectedMode) return

  await signIn(page)
  await page.goto(`/briefs/${briefId}#brief-review`)
  if (expectedMode === 'OOH_ONLY') {
    await expect(page.getByLabel('Campaign type')).toHaveValue('OOH / DOOH only')
    await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' }))
      .toHaveAttribute('data-campaign-mode', expectedMode)
    await expect(page.getByRole('region', { name: 'Full Campaign Flow' })).toHaveCount(0)
    return
  }

  expect(expectedMode).toBe('FULL_CAMPAIGN')
  await expect(page.getByLabel('Campaign type')).toHaveValue('Full campaign')
  await expect(page.getByRole('region', { name: 'Full Campaign Flow' }))
    .toHaveAttribute('data-campaign-mode', expectedMode)
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await expect(page.getByRole('heading', {
    name: 'The calm centre of campaign delivery.',
  })).toBeVisible()
  const sessionResponsePromise = page.waitForResponse(response =>
    response.url().endsWith('/api/v1/session') &&
    response.request().method() === 'POST')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  const sessionResponse = await sessionResponsePromise
  expect(sessionResponse.status(), await sessionResponse.text()).toBe(200)
  await expect(page.getByRole('heading', {
    name: 'Where are you working today?',
  })).toBeVisible()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: /Good morning, Local/ })).toBeVisible()
}

async function expectLocalSelfApproval(page: Page) {
  const response = await page.request.get(
    '/api/v1/tenants/10000000-0000-0000-0000-000000000002/commercial-policy',
  )
  const body = await response.text()
  expect(response.status(), body).toBe(200)
  const policy = JSON.parse(body) as { allowSelfApproval: boolean }
  expect(policy.allowSelfApproval,
    'The connected workspace must explicitly permit self-approval.').toBe(true)
}

async function fillBriefSource(page: Page, media: string) {
  await page.getByLabel('Campaign or Brief name')
    .fill(`Connected campaign Brief ${Date.now()}`)
  await page.getByLabel('Original Brief').fill([
    'Client: Client One',
    'Problem: Local buyers do not know about the new workspace range.',
    'Objective: Generate 500 qualified enquiries.',
    'Audience: Small business owners and office managers.',
    'Geography: Johannesburg and Pretoria.',
    'Timing: 2026-10-01 to 2026-10-31.',
    'Budget: ZAR 100,000 including VAT.',
    `Media: ${media}`,
    'Measurement: Qualified enquiries.',
  ].join('\n'))
}
