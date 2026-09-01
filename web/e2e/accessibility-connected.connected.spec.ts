import { expect, test } from '@playwright/test'

test('connected critical shell has usable keyboard and accessibility semantics', async ({ page }) => {
  await page.goto('/sign-in')

  await expect(page.getByRole('main')).toHaveCount(1)
  await expect(page.getByRole('heading', {
    name: 'The calm centre of campaign delivery.',
  })).toBeVisible()
  await expect(page.getByRole('button', {
    name: /Continue to Advertified/,
  })).toBeVisible()

  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()

  const mainContent = page.locator('#main-content')
  await expect(mainContent).toBeFocused()

  await page.reload()
  await expect(page.getByRole('heading', { name: 'Work dashboard', exact: true }))
    .toBeVisible()
  const skipLink = page.getByRole('link', { name: 'Skip to main content' })
  await page.keyboard.press('Tab')
  await expect(skipLink).toBeFocused()
  await expect(skipLink).toBeVisible()
  await page.keyboard.press('Enter')
  await expect(mainContent).toBeFocused()

  await expect(page.getByRole('navigation', { name: 'Workspace navigation' }))
    .toBeVisible()
  await expect(page.getByRole('main')).toHaveCount(1)
  await expect(page.getByRole('link', { name: 'New campaign Brief' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible()

  await page.getByRole('link', { name: 'New campaign Brief' }).click()
  await expect(mainContent).toBeFocused()
  await expect(page.getByRole('heading', { name: 'Start with the Brief, not a form' }))
    .toBeVisible()
  await expect(page.getByLabel('Campaign or Brief name')).toBeVisible()
  await expect(page.getByLabel('Original Brief')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Understand this Brief' })).toBeVisible()
})
