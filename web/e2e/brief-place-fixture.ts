import { expect, type Page } from '@playwright/test'

export async function addVerifiedPlace(page: Page) {
  const place = page.getByRole('region', { name: 'Find placements near a place' })
  await page.route('**/place-discovery?*', route => route.fulfill({ json: {
    available: true, places: [{ id: 'osm:node:123', name: 'Synthetic mapped pharmacy',
      address: 'Synthetic area, South Africa', latitude: -26.2, longitude: 28.04,
      sourceLocator: 'https://www.openstreetmap.org/node/123',
      attribution: '© OpenStreetMap contributors · ODbL', retrievedAtUtc: '2026-09-08T10:00:00Z',
      geometryBasis: 'Mapped point; verify the exact branch' }],
  } }))
  await place.getByRole('textbox', { name: 'Branch or landmark and area' }).fill('Synthetic mapped pharmacy')
  await place.getByRole('button', { name: 'Find mapped branches' }).click()
  await place.getByRole('button', { name: 'Use location: Synthetic mapped pharmacy' }).click()
  await expect(place.getByLabel('Location source or reference')).toHaveValue(
    /https:\/\/www.openstreetmap.org\/node\/123; retrieved:2026-09-08T10:00:00Z/)
  await page.route('**/inventory-places?*', async route => {
    expect(new URL(route.request().url()).searchParams.get('search')).toBe('Synthetic pharmacy')
    await route.fulfill({ json: [{ id: '90000000-0000-4000-8000-000000000001',
      name: 'Synthetic pharmacy branch', category: 'Fixture pharmacy', context: 'Synthetic area',
      latitude: -26.2, longitude: 28.04,
      productVersionId: '90000000-0000-4000-8000-000000000002',
      sourceImportId: '90000000-0000-4000-8000-000000000003', sourceLocator: 'fixture:branch-directory' }] })
  })
  await place.getByRole('textbox', { name: 'Search supplied place evidence' }).fill('Synthetic pharmacy')
  await place.getByRole('button', { name: 'Search places' }).click()
  await place.getByRole('button', { name: 'Use location: Synthetic pharmacy branch' }).click()
  await expect(place.getByLabel('Location source or reference')).toHaveValue(
    /inventory:product-version:90000000-0000-4000-8000-000000000002:poi:90000000-0000-4000-8000-000000000001/)
  await place.getByLabel('Distance around this place (metres)').fill('500')
  await place.getByRole('button', { name: 'Add place for review' }).click()
  const map = page.getByRole('region', { name: 'Brief spatial requirements map' })
  await map.getByLabel('Inspect mapped geography').selectOption({ label: 'Synthetic pharmacy branch' })
  await expect(map.getByRole('status')).toContainText('Needs human verification')
  await expect(map.getByText(/not a driving route, footfall or measured audience reach/)).toBeVisible()
  const geography = page.getByRole('group', { name: 'Synthetic pharmacy branch' })
  await expect(geography.getByLabel('EPSG:4326 GeoJSON')).toHaveValue(
    JSON.stringify({ type: 'Point', coordinates: [28.04, -26.2] }))
  const verified = geography.getByRole('checkbox')
  await expect(verified).not.toBeChecked()
  await verified.check()
  await expect(map.getByRole('status')).toContainText('Verified by a human')
  await geography.getByLabel('Location source or reference').fill('fixture:corrected-branch')
  await expect(verified).not.toBeChecked()
  await verified.check()
}
