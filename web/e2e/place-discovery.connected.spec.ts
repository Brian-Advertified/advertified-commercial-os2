import { expect, test } from '@playwright/test'

const tenantId = '10000000-0000-0000-0000-000000000002'

test('authorised live development place lookup returns attributed South African branches', async ({ page }) => {
  test.skip(process.env.ADVERTIFIED_VERIFY_LIVE_PLACES !== 'true',
    'Explicit owner-authorised live provider verification only; ordinary tests remain deterministic.')
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL('/home')
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/place-discovery`, {
    params: { search: 'Clicks Cape Town' },
  })
  expect(response.status()).toBe(200)
  const result = await response.json() as { available: boolean; places: {
    name: string; latitude: number; longitude: number; sourceLocator: string; attribution: string;
  }[] }
  expect(result.available).toBe(true)
  expect(result.places.length).toBeGreaterThan(0)
  expect(result.places.length).toBeLessThanOrEqual(10)
  for (const place of result.places) {
    expect(place.name.toLowerCase()).toContain('clicks')
    expect(place.latitude).toBeGreaterThan(-35)
    expect(place.latitude).toBeLessThan(-22)
    expect(place.longitude).toBeGreaterThan(16)
    expect(place.longitude).toBeLessThan(33)
    expect(place.sourceLocator).toMatch(/^https:\/\/www\.openstreetmap\.org\/(node|way|relation)\/\d+$/)
    expect(place.attribution).toContain('OpenStreetMap')
  }
  const denied = await page.request.get('/api/v1/tenants/10000000-0000-0000-0000-000000000099/place-discovery', {
    params: { search: 'Clicks Cape Town' },
  })
  expect(denied.status()).toBe(403)
})
