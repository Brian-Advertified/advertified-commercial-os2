import { expect, test } from '@playwright/test'

test('agency administrator sees live local agent budgets and costs', async ({ page }) => {
  const failedApiResponses: string[] = []
  page.on('response', response => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failedApiResponses.push(`${response.status()} ${new URL(response.url()).pathname}`)
    }
  })

  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()

  const workspacesResponse = await page.request.get('/api/v1/workspaces')
  expect(workspacesResponse.status(), await workspacesResponse.text()).toBe(200)
  const workspaces = await workspacesResponse.json() as Array<{ roleCode: string }>
  expect(workspaces.some(workspace => workspace.roleCode === 'agency_admin')).toBe(true)

  await page.goto('/admin/commercial')
  const operationsResponsePromise = page.waitForResponse(response =>
    /\/api\/v1\/tenants\/[^/]+\/agent-operations$/.test(new URL(response.url()).pathname))
  await page.getByRole('link', { name: 'Agent operations', exact: true }).click()
  const operationsResponse = await operationsResponsePromise
  expect(operationsResponse.status(), await operationsResponse.text()).toBe(200)

  await expect(page).toHaveURL(/\/admin\/agents$/)
  await expect(page.getByRole('heading', { name: 'Agent operations', exact: true })).toBeVisible()
  await expect(page.getByText('Paid AI disabled', { exact: true })).toBeVisible()
  const budgets = page.getByRole('region', { name: 'Agent budgets and costs' })
  await expect(budgets.locator('tbody tr')).toHaveCount(11)
  await expect(budgets.getByRole('row', { name: /Business Interpretation Agent/ }))
    .toContainText('$0.00')
  await expect(page.getByRole('heading', { name: 'Recent recorded usage' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Recent durable runs' })).toBeVisible()
  expect(failedApiResponses).toEqual([])
})
