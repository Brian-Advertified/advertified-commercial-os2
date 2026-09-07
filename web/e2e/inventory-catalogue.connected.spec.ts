import { expect, test } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000002'
const productId = '86761a0b-593a-5e51-bba7-d7c55d12fa68'

test('connected catalogue pages, filters and opens a legacy product', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page.getByRole('heading', { name: /Good (morning|afternoon|evening), Local/ })).toBeVisible()

  await page.goto('/inventory')
  await expect(page.getByRole('heading', { name: 'Media inventory' })).toBeVisible()
  const supplier = page.getByLabel('Supplier')
  for (const name of ['BlackSpace', 'Eleven8', 'eMedia', 'Insight Outdoor', 'Kaya 959', 'SABC']) {
    await expect(supplier.locator('option', { hasText: name })).toHaveCount(1)
  }
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
  if (product.rate?.commercialTerms) {
    expect(product.rate.commercialTerms.inclusions).toEqual([])
    expect(product.rate.commercialTerms.exclusions).toEqual([])
    expect(product.rate.commercialTerms.conditions).toEqual([])
  }

  const brandedProducts = [
    ['a7597027-5d86-5e2e-8f66-1124d99d721d', 'metro-fm-mono.webp'],
    ['b46e1929-9d88-5642-9aee-7ff90e928403', '5fm-mono.webp'],
    ['f86fec51-b6b2-579f-bf31-8fb3409fd5e3', 'kaya-959-mono.webp'],
    ['a23a5521-4b8d-5379-8d60-c25ca6617d6e', 'sabc-1-mono.webp'],
    ['51910b75-6ebc-57b9-82f4-c3b60c4ca517', 'sabc-2-mono.webp'],
    ['c2cecc63-d1b1-5ae1-9ace-e5fc6eb80139', 'sabc-3-mono.webp'],
    ['480fc4d6-b5d5-5257-ae31-6f0d1fee9bf8', 'sabc-news.png'],
    ['a5802085-da9a-5b70-a04e-8c373abeaa69', 'sabc-sport.png'],
    ['2093bb45-e8a9-5efb-9690-c48f24b71b48', 'emedia-sales-mono.webp'],
  ] as const
  for (const [id, asset] of brandedProducts) {
    await page.goto(`/inventory/products/${id}`)
    await expect(page.getByRole('heading', { name: 'Product could not be loaded' })).toHaveCount(0)
    await expect(page.locator(`.approved-product-media img[src$="${asset}"]`)).toBeVisible()
  }

  await page.goto('/inventory/products/1cd7a0ac-316a-570d-a863-dc87de4b5273')
  await expect(page.locator('#product-title')).toHaveText('M5 Mowbray')
  await expect(page.getByText('Request supplier quote', { exact: true })).toBeVisible()
})

function cardLinks(page: import('@playwright/test').Page) {
  return page.locator('.approved-inventory-card')
    .evaluateAll(nodes => nodes.map(node => node.getAttribute('href')))
}

type ProductPayload = {
  rate: null | { commercialTerms: null | {
    inclusions: string[]; exclusions: string[]; conditions: string[]
  } }
  spatial: null | { pointsOfInterest: unknown[] }
}
