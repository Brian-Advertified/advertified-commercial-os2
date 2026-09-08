import { expect, type Page } from '@playwright/test'

export async function addVerifiedPlace(page: Page) {
  const place = page.getByRole('region', { name: 'Find placements near a place' })
  await place.getByLabel('Place or branch name').fill('Synthetic pharmacy branch')
  await place.getByLabel('Location source or reference').fill('fixture:branch-directory')
  await place.getByLabel('Latitude', { exact: true }).fill('-26.2')
  await place.getByLabel('Longitude', { exact: true }).fill('28.04')
  await place.getByLabel('Distance around this place (metres)').fill('500')
  await place.getByRole('button', { name: 'Add place for review' }).click()
  const geography = page.getByRole('group', { name: 'Synthetic pharmacy branch' })
  await expect(geography.getByLabel('EPSG:4326 GeoJSON')).toHaveValue(
    JSON.stringify({ type: 'Point', coordinates: [28.04, -26.2] }))
  const verified = geography.getByRole('checkbox')
  await expect(verified).not.toBeChecked()
  await verified.check()
  await geography.getByLabel('Location source or reference').fill('fixture:corrected-branch')
  await expect(verified).not.toBeChecked()
  await verified.check()
}
