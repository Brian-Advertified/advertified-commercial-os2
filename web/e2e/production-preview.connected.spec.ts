import { expect, test, type Page } from '@playwright/test'
import { mkdir, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import { approveRenderAndSave, baseUrl, brianId, clickWhenReady, fillWhenReady,
  outputDirectory, progressToProposal, signIn, type BriefScenario } from './production-preview-flow'

const scenarios: BriefScenario[] = [
  {
    slug: 'ooh-jameson-select',
    title: 'Jameson Select high-SEM digital OOH preview',
    client: 'Jameson Select',
    inventoryPattern: /digital|screen|billboard|large format/i,
    source: [
      'Certification preview based on the supplied Jameson Select brief.',
      'Client: Jameson Select.',
      'Objective: build premium awareness in high-SEM areas.',
      'Audience: legal-drinking-age adults in high-SEM urban areas.',
      'Geography: Sandton, Fourways, Ballito, Umhlanga, Durban and Cape Town.',
      'Timing: 15 August 2026 to 30 September 2026.',
      'Budget: ZAR 500,000 excluding VAT is a certification assumption, not a client-supplied fact.',
      'Media: OOH and DOOH only. Use only digital large-format sites. Do not use 3 x 6 sites.',
      'Measurement: booked sites, proof of flight and supplied delivery metrics.',
      'Brand assets: none supplied; use Advertified proposal branding only.',
      'Brian reviews and self-approves every internal governed step.',
    ].join('\n'),
  },
  {
    slug: 'full-remittance-platform',
    title: 'Remittance audience full-campaign preview',
    client: 'CLIENT X Remittance',
    inventoryPattern: /social|facebook|instagram|tiktok|youtube/i,
    source: [
      'Separate full-campaign certification scenario inspired by the supplied remittance OOH brief.',
      'Client: CLIENT X Remittance.',
      'Objective: generate awareness and qualified money-transfer enquiries.',
      'Audience: South African residents who send money to Bangladesh, India and Pakistan.',
      'Geography: urban South African communities near relevant mosques, temples and retail nodes.',
      'Timing: 1 October 2026 to 31 December 2026.',
      'Budget: ZAR 900,000 excluding VAT is a certification assumption, not a client-supplied fact.',
      'Media: full campaign using billboards, source-priced social and platform media, Google Search and radio.',
      'Social scope: Facebook, Instagram, TikTok, LinkedIn and YouTube where source-backed inventory exists.',
      'Google Search and Display are auction-based Digital media; use a selected media budget, not a guaranteed public rate.',
      'Measurement: approved delivery proof, spend, impressions, clicks, video views and qualified enquiries.',
      'Brand assets: none supplied; use Advertified proposal branding only.',
      'Brian reviews and self-approves every internal governed step.',
    ].join('\n'),
  },
]

test.describe.configure({ mode: 'serial' })

test('resume the authorised OOH preview after shortlist', async ({ page }) => {
  test.setTimeout(110_000)
  await signIn(page)
  const briefId = requiredPreviewId('PREVIEW_JAMESON_BRIEF_ID')
  await page.goto(baseUrl + '/briefs/' + briefId + '/proposals/new')
  const choice = page.getByRole('button', { name: /^Plan 1/ })
  await choice.click()
  await expect(choice).toHaveAttribute('aria-pressed', 'true')
  const created = page.waitForURL(/\/proposals\/[0-9a-f-]{36}$/i, { timeout: 90_000 })
  await page.getByRole('button', { name: /^Create proposal$/ }).click()
  await created
  await approveRenderAndSave(page, scenarios[0].slug)
})

test('resume the authorised full campaign after media mix', async ({ page }) => {
  test.setTimeout(110_000)
  await signIn(page)
  const briefVersionId = requiredPreviewId('PREVIEW_MUKURU_BRIEF_VERSION_ID')
  await page.goto(baseUrl + '/planning/' + briefVersionId)
  await progressToProposal(page, scenarios[1])
  await approveRenderAndSave(page, scenarios[1].slug)
})

test('Rayetsa opportunity reaches its governed campaign Brief', async ({ page }) => {
  test.setTimeout(420_000)
  await signIn(page)
  const opportunityId = requiredPreviewId('PREVIEW_RAYETSA_OPPORTUNITY_ID')
  await page.goto(baseUrl + '/opportunities/' + opportunityId)
  await qualifyRayetsaOpportunity(page)
  await saveOpportunityPreview(page)
})

async function qualifyRayetsaOpportunity(page: Page) {
  await clickWhenReady(page, /^Start qualification$/)
  await clickWhenReady(page, /^Approve assigned evidence$/)
  await fillWhenReady(page, 'Evidence-set approver user ID', brianId)
  await clickWhenReady(page, /^Submit evidence set$/)
  await clickWhenReady(page, /^Approve assigned evidence set$/)
  await clickWhenReady(page, /^Interpret approved evidence$/)
  await clickWhenReady(page, /^Confirm interpretation$/)
  await clickWhenReady(page, /^Generate opportunity angles$/)
  await clickWhenReady(page, /^Select angle 1$/)
  await fillWhenReady(page, 'Strategy approver user ID', brianId)
  await clickWhenReady(page, /^Generate strategy and critic$/)
  await resolveStrategyObjections(page)
  await clickWhenReady(page, /^Submit strategy$/)
  await clickWhenReady(page, /^Approve assigned strategy$/)
  await clickWhenReady(page, /^Draft campaign Brief$/)
  const briefLink = await waitForVisible(page, page.getByRole('link', {
    name: /^Review campaign Brief$/,
  }))
  await briefLink.click()
  await waitForSettled(page)
}

async function resolveStrategyObjections(page: Page) {
  for (let index = 0; index < 10; index += 1) {
    if (await firstVisible(page.getByRole('button', { name: /^Submit strategy$/ }))) return
    const resolve = page.getByRole('button', { name: /^Resolve .* objection$/ })
    if (await firstVisible(resolve)) {
      await resolve.first().click()
      await waitForSettled(page)
    } else {
      await refresh(page)
    }
  }
}

async function saveOpportunityPreview(page: Page) {
  await mkdir(outputDirectory, { recursive: true })
  await writeFile(
    join(outputDirectory, 'opportunity-rayetsa.json'),
    JSON.stringify({
      scenario: 'opportunity',
      title: 'Rayetsa Furniture growth opportunity',
      reviewer: 'Brian',
      approvalMode: 'SELF',
      finalUrl: page.url(),
    }, null, 2) + '\n',
  )
  await expect(page.getByRole('heading', { name: /Brief|Rayetsa/i }).first()).toBeVisible()
}

function requiredPreviewId(name: string) {
  const value = process.env[name]
  if (!value || !/^[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(value)) {
    throw new Error(name + ' must identify the existing authorised preview record.')
  }
  return value
}
