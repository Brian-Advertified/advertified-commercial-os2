import { expect, test, type Page } from '@playwright/test'

const stageLabels = ['Strategy & STP', 'Planning', 'Proposals', 'Approvals', 'Measurement', 'Reports']
const topLevelLabels = ['Home', 'Opportunities', 'Briefs', 'Inventory', 'Marketplace', 'OOH Inbox', 'Bookings', 'Campaigns', 'Tasks', 'Finance']

test('live 3017 uses one Advertified shell across all authenticated modules', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()

  const shellWidths: number[] = []
  const contentWidths: number[] = []
  const routes = [
    ['/home', '.approved-dashboard'],
    ['/opportunities', '.approved-opportunity-page'],
    ['/briefs/new', '.brief-intake-page'],
    ['/inventory', '.inventory-workbench-page'],
    ['/ooh-inbox', '.ooh-inbox-page'],
  ] as const
  for (const [route, root] of routes) {
    await page.goto(route)
    await expect(page.locator('.approved-shell--workspace')).toBeVisible()
    await expect(page.locator('.approved-home-topbar')).toBeVisible()
    await expectGlobalNavigation(page)
    shellWidths.push(await page.locator('.approved-sidebar').evaluate(element => element.getBoundingClientRect().width))
    contentWidths.push(await page.locator(root).evaluate(element => element.getBoundingClientRect().width))
  }
  expect(new Set(shellWidths.map(value => Math.round(value))).size).toBe(1)
  expect(new Set(contentWidths.map(value => Math.round(value))).size).toBe(1)

  const briefsResponse = page.waitForResponse(response =>
    response.request().method() === 'GET' && /\/api\/v1\/tenants\/[^/]+\/briefs$/.test(new URL(response.url()).pathname))
  await page.goto('/briefs')
  expect((await briefsResponse).status()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Briefs', exact: true })).toBeVisible()

  const policyResponse = page.waitForResponse(response =>
    response.request().method() === 'GET' && /\/api\/v1\/tenants\/[^/]+\/commercial-policy$/.test(new URL(response.url()).pathname))
  await page.goto('/admin/commercial')
  const policy = await policyResponse
  expect(policy.status(), await policy.text()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Commercial policy', exact: true })).toBeVisible()

  await page.goto('/opportunities')
  const opportunitiesTitle = page.getByRole('heading', { name: 'Opportunities', exact: true })
  await expect(opportunitiesTitle).toHaveCSS('font-size', '22px')
  await expect(page.locator('.approved-opportunity-metrics')).toBeVisible()
  await expect(page.locator('.approved-opportunity-layout')).toBeVisible()

  await page.goto('/briefs/new')
  await expect(page.getByRole('region', { name: 'Campaign Flow' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Campaign Flow' }).getByRole('link')).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'Start with the Brief, not a form' })).toHaveCSS('font-size', '22px')

  await page.goto('/inventory')
  await expect(page.getByRole('region', { name: 'Inventory Intelligence Flow' })).toBeVisible()
  await expect(page.getByText('ADVERTIFIED', { exact: true })).toHaveCount(1)
  await expect(page.getByRole('heading', { name: 'Media inventory', exact: true })).toHaveCSS('font-size', '22px')
  const sizes = await visibleFontSizes(page)
  expect(Math.min(...sizes)).toBeGreaterThanOrEqual(11)

  await page.goto('/ooh-inbox')
  await expect(page.getByRole('region', { name: 'OOH-only Campaign Flow' })).toBeVisible()
  await expect(page.locator('.approved-sidebar')).toHaveCSS('background-color', 'rgb(255, 255, 255)')
})

async function expectGlobalNavigation(page: Page) {
  const navigation = page.getByRole('navigation', { name: 'Workspace navigation' })
  for (const label of stageLabels) await expect(navigation.getByText(label, { exact: true })).toHaveCount(0)
  for (const label of topLevelLabels) await expect(navigation.getByText(label, { exact: true })).toBeVisible()
}

async function visibleFontSizes(page: Page) {
  return page.locator('.approved-shell--workspace').evaluate((root) => {
    const elements = [...root.querySelectorAll('p,span,strong,small,label,button,input,select,a')]
      .filter((element) => {
        const style = getComputedStyle(element)
        return (element.textContent ?? '').trim().length > 0 &&
          !element.classList.contains('approved-wordmark-mark') &&
          style.display !== 'none' && style.visibility !== 'hidden' &&
          Number.parseFloat(style.fontSize) > 0
      })
    return elements.map((element) => Number.parseFloat(getComputedStyle(element).fontSize))
  })
}
