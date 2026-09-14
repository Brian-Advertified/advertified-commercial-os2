import { expect, test, type Page } from '@playwright/test'

const TENANT = '10000000-0000-0000-0000-000000000002'
const VERSION = '3817c516-085c-47ff-8639-bf042e23c8e6'

test('diagnose manual Rayetsa strategy blocker', async ({ page }) => {
  await signIn(page)
  const planningRes = await page.request.get(`/api/v1/tenants/${TENANT}/brief-versions/${VERSION}/planning`)
  expect(planningRes.ok(), await planningRes.text()).toBe(true)
  const planning = await planningRes.json()
  const strategyRes = await page.request.get(`/api/v1/tenants/${TENANT}/brief-versions/${VERSION}/intelligence/media-strategy`)
  const strategy = strategyRes.ok() ? await strategyRes.json() : { statusCode: strategyRes.status(), body: await strategyRes.text() }
  console.log('PLANNING_STATE=' + JSON.stringify(planning))
  console.log('STRATEGY_STATE=' + JSON.stringify(strategy))

  await page.goto(`/planning/${VERSION}#strategy`)
  await expect(page.getByRole('heading', { name: 'Strategy recommendations' })).toBeVisible()
  const buttons = await page.locator('main button:visible, main a:visible').allTextContents()
  console.log('VISIBLE_ACTIONS=' + JSON.stringify(buttons.map(x => x.trim()).filter(Boolean)))
  console.log('VISIBLE_TEXT=' + (await page.locator('main').innerText()).slice(0, 16000))
})

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
