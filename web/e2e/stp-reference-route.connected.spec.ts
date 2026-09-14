import { expect, test } from '@playwright/test'

const briefVersionId = '022cc072-70e9-4db8-872b-1ed08f1500c2'

test('exact approved Audience & STP reference route exposes the intended composition and useful intelligence', async ({ page }) => {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await page.goto(`/stp/${briefVersionId}`)

  await expect(page.getByRole('heading', { name: 'Audience & STP' })).toBeVisible()
  await expect(page.locator('.connected-audience-top-grid')).toBeVisible()
  await expect(page.locator('.connected-audience-content-grid')).toBeVisible()
  await expect(page.locator('.audience-strategy-direction')).toBeVisible()

  const cards = page.locator('.connected-audience-priority .audience-strategy-card')
  const geography = page.locator('.connected-geography-card')
  const mainPanel = page.locator('.connected-audience-main-panel')
  const insights = page.locator('.connected-audience-insights')
  await expect(cards).toHaveCount(2)
  await expect(geography).toBeVisible()
  await expect(mainPanel).toBeVisible()
  await expect(insights).toBeVisible()

  const [firstCardBox, secondCardBox, geoBox, mainBox, insightsBox] = await Promise.all([
    cards.nth(0).boundingBox(), cards.nth(1).boundingBox(), geography.boundingBox(),
    mainPanel.boundingBox(), insights.boundingBox(),
  ])
  expect(firstCardBox && secondCardBox && geoBox && mainBox && insightsBox).toBeTruthy()
  expect(Math.abs(firstCardBox!.y - secondCardBox!.y)).toBeLessThan(4)
  expect(Math.abs(firstCardBox!.y - geoBox!.y)).toBeLessThan(4)
  expect(Math.abs(mainBox!.y - insightsBox!.y)).toBeLessThan(4)
  expect(mainBox!.width / insightsBox!.width).toBeGreaterThan(1.7)
  expect(mainBox!.width / insightsBox!.width).toBeLessThan(3.2)

  const text = await page.locator('main').innerText()
  expect(text).not.toContain('Confidence not established')
  expect(text).not.toContain('✓ Audience strategy approved\nNext: Strategy →')
  expect(text).toMatch(/AI hypothesis|governed|client requirement|needs enrichment/i)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  console.log('EXACT_STP_URL', page.url())
  console.log('EXACT_STP_TEXT', text.slice(0, 12000))
  console.log('EXACT_STP_GEOMETRY', JSON.stringify({ firstCardBox, secondCardBox, geoBox, mainBox, insightsBox }))
})
