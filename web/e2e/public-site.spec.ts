import { expect, test } from '@playwright/test'
import { publicRoutes } from '../src/public/publicRoutes'

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/public/inventory-summary', async (route) => {
    await route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' })
  })
  await page.route('**/api/v1/session', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        authenticated: false,
        antiforgeryToken: 'csrf-public-site',
        expiresAtUtc: null,
      }),
    })
  })
})

test('media network counts stations and channels rather than parent owners', async ({ page }) => {
  await page.route('**/api/v1/public/inventory-summary', route => route.fulfill({ json: {
    totalCount: 3, channels: [
      { channel: 'radio', count: 2, countBasis: 'canonical_radio_stations', units: [
        { id: 'station-a', name: 'Synthetic Station A', logoUrl: null },
        { id: 'station-b', name: 'Synthetic Station B', logoUrl: null }] },
      { channel: 'television', count: 1, countBasis: 'canonical_television_channels', units: [
        { id: 'channel-a', name: 'Synthetic Channel A', logoUrl: null }] },
    ],
  } }))
  await page.goto('/')
  await page.getByRole('button', { name: 'Accept necessary' }).click()
  await expect(page.getByRole('link', { name: '2 radio stations. View directory.' })).toBeVisible()
  await expect(page.getByRole('link', { name: '1 tv channels. View directory.' })).toBeVisible()
  await page.getByRole('link', { name: '2 radio stations. View directory.' }).click()
  await expect(page.getByRole('heading', { name: 'Radio stations' })).toBeVisible()
  await expect(page.getByText(/not their parent owners/)).toBeVisible()
  await expect(page.getByText('Synthetic Station A', { exact: true }).last()).toBeVisible()
  await expect(page.getByText('Synthetic Station B', { exact: true }).last()).toBeVisible()
})

test('public journey reaches solutions and the governed brief handoff', async ({ page }, testInfo) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Intelligence layer for modern advertising.' })).toBeVisible()
  await expect(page.getByRole('dialog', { name: 'Cookie preferences' })).toBeVisible()
  await page.getByRole('button', { name: 'Accept necessary' }).click()

  if (testInfo.project.name === 'compact') {
    await page.getByRole('button', { name: 'Open navigation' }).click()
  }
  await page
    .getByRole('navigation', { name: 'Primary navigation' })
    .getByRole('link', { name: 'Solutions', exact: true })
    .click()
  await expect(page).toHaveURL('/solutions')
  await expect(page).toHaveTitle('Cross-media advertising solutions | Advertified')
  await expect(page.getByRole('heading', {
    name: 'Build the media mix around the campaign job - not the loudest channel.',
  })).toBeVisible()

  await page.goto('/start')
  const addBriefLink = page.getByRole('link', { name: 'Sign in to add a brief' })
  await expect(addBriefLink).toHaveAttribute('href', '/sign-in?returnTo=/briefs/new')
  await addBriefLink.click()
  await expect(page).toHaveURL('/sign-in?returnTo=/briefs/new')
  await expect(page.getByRole('heading', { name: 'The calm centre of campaign delivery.' })).toBeVisible()
})

test('public onboarding stays truthful until an administrator grants access', async ({ page }) => {
  await page.goto('/register/agency')
  await expect(page.getByRole('heading', { name: 'Agency registration' })).toBeVisible()
  await expect(page.getByText('No account, membership or campaign access is created automatically.')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Email Advertified' })).toHaveAttribute(
    'href',
    /^mailto:ad@advertified\.com\?subject=/u,
  )
})

test('homepage restores monochrome scrolling logos with motion preferences', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.goto('/')
  await page.getByRole('button', { name: 'Accept necessary' }).click()
  const strip = page.getByRole('region', { name: 'MEDIA PARTNERS', exact: true })
  await expect(strip).toBeVisible()
  const logos = strip.locator('.media-partner-set:not([aria-hidden]) img')
  expect(await logos.count()).toBeGreaterThan(0)
  await expect.poll(() => logos.evaluateAll(images => images.every(image =>
    image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0 &&
    getComputedStyle(image).filter === 'grayscale(1)'))).toBe(true)
  const track = strip.locator('.media-partner-track')
  await expect(track).toHaveCSS('animation-name', 'media-partner-scroll')
  await strip.locator('.media-partner-scroll').focus()
  await expect(track).toHaveCSS('animation-play-state', 'paused')
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await expect(track).toHaveCSS('animation-name', 'none')
  await expect(strip.locator('.media-partner-set[aria-hidden="true"]')).toBeHidden()
})

test('media partners uses progressive disclosure instead of a mobile wall', async ({ page }, testInfo) => {
  await page.goto('/media-partners')
  const cards = page.locator('.partner-cards article')
  const toggle = page.getByRole('button', { name: /Show all .* partners|Show fewer partners/u })
  await expect(toggle).toBeVisible()
  expect(await cards.count()).toBeLessThanOrEqual(12)
  await toggle.click()
  expect(await cards.count()).toBeGreaterThan(12)
  await expect(toggle).toHaveText(/Show fewer partners/u)
  if (testInfo.project.name === 'compact') {
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(1)
  }
})

test('compact how-it-works shows one journey stage at a time', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'compact', 'Compact progressive disclosure only')
  await page.goto('/how-it-works')
  const stages = page.locator('.hiw-stage')
  await expect(stages).toHaveCount(6)
  await expect(stages.filter({ visible: true })).toHaveCount(1)
  await page.getByRole('button', { name: /Plan/u }).click()
  await expect(stages.nth(2)).toBeVisible()
  await expect(stages.nth(0)).toBeHidden()
})

test('every declared public page renders inside the public shell', async ({ page }) => {
  test.setTimeout(120_000)

  for (const route of publicRoutes) {
    await page.goto(route.path)
    await expect(page.getByRole('main')).toBeVisible()
    await expect(page.getByRole('banner').getByRole('link', { name: 'Advertified home' })).toBeVisible()
    await expect(page).toHaveTitle(route.title)
  }
})
