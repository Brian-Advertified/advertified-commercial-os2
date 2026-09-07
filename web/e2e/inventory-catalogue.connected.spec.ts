import { expect, test } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000002'
const productId = '86761a0b-593a-5e51-bba7-d7c55d12fa68'

test('connected catalogue pages, filters and opens a legacy product', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: /Good morning, Local/ })).toBeVisible()

  await page.goto('/inventory')
  await expect(page.getByRole('heading', { name: 'Media inventory' })).toBeVisible()
  const supplier = page.getByLabel('Supplier')
  await expect(supplier.locator('option', { hasText: 'Insight Outdoor' })).toHaveCount(1)
  const firstPage = await cardLinks(page)
  expect(firstPage).toHaveLength(24)
  await expect(page.locator('.approved-inventory-card img[src*="/assets/media-inventory/"]')).toHaveCount(0)

  await page.getByRole('button', { name: 'Next' }).click()
  await expect(page.getByText('Page 2', { exact: true })).toBeVisible()
  expect(await cardLinks(page)).not.toEqual(firstPage)
  await page.getByRole('button', { name: 'Previous' }).click()
  await expect(page.getByText('Page 1', { exact: true })).toBeVisible()
  expect(await cardLinks(page)).toEqual(firstPage)

  await supplier.selectOption({ label: 'Insight Outdoor' })
  await page.getByRole('button', { name: 'Update results' }).click()
  await expect(page.locator('.approved-inventory-card footer small').first()).toHaveText('Insight Outdoor')
  expect(new Set(await page.locator('.approved-inventory-card footer small').allTextContents()))
    .toEqual(new Set(['Insight Outdoor']))

  await page.goto(`/inventory/products/${productId}`)
  await expect(page.locator('#product-title')).toHaveText('Lynnwood Road, The Grove')
  await expect(page.getByRole('heading', { name: 'Product could not be loaded' })).toHaveCount(0)
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/inventory-products/${productId}`)
  expect(response.status(), await response.text()).toBe(200)
  const product = await response.json() as ProductPayload
  expect(product.spatial?.pointsOfInterest).toEqual([])
  if (product.rate.commercialTerms) {
    expect(product.rate.commercialTerms.inclusions).toEqual([])
    expect(product.rate.commercialTerms.exclusions).toEqual([])
    expect(product.rate.commercialTerms.conditions).toEqual([])
  }
})

function cardLinks(page: import('@playwright/test').Page) {
  return page.locator('.approved-inventory-card')
    .evaluateAll(nodes => nodes.map(node => node.getAttribute('href')))
}

type ProductPayload = {
  rate: { commercialTerms: null | {
    inclusions: string[]; exclusions: string[]; conditions: string[]
  } }
  spatial: null | { pointsOfInterest: unknown[] }
}
