import { expect, type Page } from '@playwright/test'

export async function addAudienceResearch(page: Page) {
  const research = page.getByRole('region', { name: 'Audience research', exact: true })
  await research.getByLabel('Research source reference').fill('fixture:lawfully-supplied-aggregate-study')
  await research.getByLabel('Supporting research excerpt').fill('Synthetic evidence: the named target researches office furniture in English.')
  await research.getByLabel('Research measurement period').fill('2026 Q2')
  await research.getByLabel('Research method and limitations').fill('Synthetic aggregate survey; fixture only.')
  await research.getByText('Audience research details (optional)', { exact: true }).click()
  await research.getByLabel('Audience name', { exact: true }).fill('Workspace furniture buyers')
  await research.getByLabel('Supported language').fill('English')
  await research.getByLabel('Consumer need supported by source').fill('Compare durable office furniture.')
  await research.getByRole('button', { name: 'Add research for review' }).click()
  await expect(research.getByRole('button', { name: 'Remove research record' })).toBeVisible()
}

export function assertAudienceResearch(values: unknown) {
  expect(values).toMatchObject([{ audienceName: 'Workspace furniture buyers',
    sourceLocator: 'fixture:lawfully-supplied-aggregate-study', language: 'English',
    needState: 'Compare durable office furniture.' }])
}
