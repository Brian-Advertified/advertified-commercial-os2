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

test('every declared public page renders inside the public shell', async ({ page }) => {
  test.setTimeout(120_000)

  for (const route of publicRoutes) {
    await page.goto(route.path)
    await expect(page.getByRole('main')).toBeVisible()
    await expect(page.getByRole('banner').getByRole('link', { name: 'Advertified home' })).toBeVisible()
    await expect(page).toHaveTitle(route.title)
  }
})
