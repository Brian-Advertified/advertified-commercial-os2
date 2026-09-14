import { expect, test, type Page } from '@playwright/test'

const BRIEF_ID = 'a314a1b2-7459-4878-b673-0fb145463991'
const BRIEF_VERSION_ID = 'c4fcbe9d-77ca-4367-b2f8-f72f0d4a8c04'
const LOCAL_TENANT_ID = '10000000-0000-0000-0000-000000000002'
const FLIGHT = { start: '2026-10-01', end: '2026-12-31' }
const MARKETS = ['Pretoria', 'Midrand', 'Broader Gauteng', 'Mpumalanga', 'Limpopo']

const RADIO_PURCHASES: Purchase[] = [
  {
    inventoryTenantId: LOCAL_TENANT_ID,
    inventoryProductId: '8e09532e-7fb2-5a13-9214-e7b01871ad45',
    productVersionId: '98033253-9c1d-5688-a1ed-d927830ed941',
    rateId: 'a7586223-f083-51e9-a114-b2292ff48ffc',
    rateType: 'SPOT_RATE',
    quantity: 10,
  },
  {
    inventoryTenantId: LOCAL_TENANT_ID,
    inventoryProductId: '6cea0d75-53da-5f1f-ad05-9fd80833b652',
    productVersionId: '74e76b0d-cfc1-508a-bbd8-72f5a18d9942',
    rateId: '76ddb047-554f-552b-aa22-ed573ef552d0',
    rateType: 'SPOT_RATE',
    quantity: 25,
  },
  {
    inventoryTenantId: LOCAL_TENANT_ID,
    inventoryProductId: '2c4bd9b8-1874-570c-aa8f-2cb538189746',
    productVersionId: '5758f66b-9b22-5f9b-bcaf-03f8e2c37ac5',
    rateId: 'ba235472-2db6-5074-a189-27614ee8f137',
    rateType: 'SPOT_RATE',
    quantity: 25,
  },
]

// Connected, intentionally stateful production-behaviour canary for the local Rayetsa Brief.
test('Rayetsa runs from approved Brief through approved media plan to proposal', async ({ page }) => {
  await signIn(page)
  const tenantId = await localTenant(page)
  const token = await antiforgeryToken(page)

  let approvedPlans = await listApprovedPlans(page, tenantId)
  if (!approvedPlans.length) {
    const mix = await ensureBuyableMix(page, tenantId, token)
    const shortlist = await generateShortlist(page, tenantId, token)
    expect(shortlist.mixVersionId).toBe(mix.id)
    const selected = selectCommerciallyUsableCandidates(shortlist)
    const confirmed = await selectShortlist(page, tenantId, token, shortlist, selected)
    const plan = await generatePlan(page, tenantId, token)
    const reviewed = await resolvePlanObjections(page, tenantId, token, plan)
    await approvePlan(page, tenantId, token, reviewed)
    approvedPlans = await listApprovedPlans(page, tenantId)
    expect(confirmed.status).toBe('APPROVED')
  }

  expect(approvedPlans.length, 'Rayetsa must have an approved plan before proposal preparation').toBeGreaterThan(0)
  const proposal = await ensureProposal(page, tenantId, token, approvedPlans[0])
  expect(proposal.briefId).toBe(BRIEF_ID)

  await page.goto(`/briefs/${BRIEF_ID}/proposals/new`)
  await expect(page.getByRole('heading', { name: 'Proposal builder' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'No approved plans yet' })).toHaveCount(0)
  await expect(page.getByText('1 approved plan', { exact: true })).toBeVisible()
})

type Purchase = {
  inventoryTenantId: string
  inventoryProductId: string
  productVersionId: string
  rateId: string
  rateType: string
  quantity: number
  denominator?: number | null
}

type Allocation = {
  channel: string
  budgetMinor: number
  role: string
  runningPeriods: Array<{ start: string; end: string }>
  purchases?: Purchase[] | null
  geographyAllocations?: Array<{ geography: string; budgetMinor: number }>
  schedule?: { weekdays: string[]; dayparts: string[] } | null
}

type Mix = {
  id: string
  status: string
  version: number
  impactEstimate?: unknown
  allocations: Allocation[]
}

type Candidate = {
  id: string
  inventoryProductId: string
  channel: string
  name: string
  isEligible: boolean
  rateAmountMinor: number | null
  score: number | null
  commercialReadiness: null | { evidenceGaps: string[]; rateType?: string | null }
}

type Shortlist = {
  id: string
  mixVersionId: string
  status: string
  version: number
  candidates: Candidate[]
}

type Plan = {
  id: string
  status: string
  version: number
  objections: Array<{ code: string; severity: string; resolution: string | null }>
}

type ApprovedPlan = { id: string; briefVersionId: string; totalMinor: number; currency: string }
type Proposal = { id: string; briefId: string; status: string }

type Planning = { mediaMix: Mix | null }

async function ensureBuyableMix(page: Page, tenantId: string, token: string) {
  const planning = await getPlanning(page, tenantId)
  const current = planning.mediaMix
  if (current?.status === 'DRAFT' && hasExpectedRadioPurchases(current)) {
    return approveMix(page, tenantId, token, current)
  }
  const generated = await command<Mix>(page,
    `/api/v1/tenants/${tenantId}/brief-versions/${BRIEF_VERSION_ID}/media-mixes:generate`,
    token, {}, undefined)
  const allocations = generated.allocations.map(allocation => ({
    ...allocation,
    runningPeriods: [FLIGHT],
    geographyAllocations: splitBudget(allocation.budgetMinor),
    schedule: allocation.schedule ?? null,
    purchases: allocation.channel === 'RADIO' ? RADIO_PURCHASES : null,
  }))
  const updated = await command<Mix>(page,
    `/api/v1/tenants/${tenantId}/media-mix-versions/${generated.id}:update`,
    token,
    { allocations, impactEstimate: generated.impactEstimate ?? null,
      reason: 'Production-behaviour planning: apply the approved flight, market allocation and explicit radio spot quantities.' },
    generated.version)
  return approveMix(page, tenantId, token, updated)
}

async function approveMix(page: Page, tenantId: string, token: string, mix: Mix) {
  return command<Mix>(page,
    `/api/v1/tenants/${tenantId}/media-mix-versions/${mix.id}:approve`,
    token, { reason: 'Planner-approved buyable mix for inventory selection.' }, mix.version)
}

function hasExpectedRadioPurchases(mix: Mix) {
  const radio = mix.allocations.find(item => item.channel === 'RADIO')
  return radio?.purchases?.length === RADIO_PURCHASES.length
}

async function generateShortlist(page: Page, tenantId: string, token: string) {
  return command<Shortlist>(page,
    `/api/v1/tenants/${tenantId}/brief-versions/${BRIEF_VERSION_ID}/shortlists:generate`, token, {}, undefined)
}

function selectCommerciallyUsableCandidates(shortlist: Shortlist) {
  const eligible = shortlist.candidates.filter(item => item.isEligible)
  const selected: Candidate[] = []
  const ooh = best(eligible.filter(item => item.channel === 'OOH'))
  const radio = eligible.filter(item => item.channel === 'RADIO' &&
    RADIO_PURCHASES.some(purchase => purchase.inventoryProductId === item.inventoryProductId))
  const digital = best(eligible.filter(item => item.channel === 'DIGITAL'))
  const socialPool = eligible.filter(item => item.channel === 'SOCIAL')
  const social = socialPool.find(item => /Social media \(EWN\)/i.test(item.name)) ?? best(socialPool)
  expect(ooh, 'OOH should have a buyable eligible placement').toBeTruthy()
  expect(radio.length, 'All three governed radio purchases should become shortlist-eligible').toBe(3)
  expect(digital, 'Digital should have a buyable eligible placement').toBeTruthy()
  expect(social, 'Social should have a buyable eligible placement').toBeTruthy()
  selected.push(ooh!, ...radio, digital!, social!)
  return selected.map(item => item.id)
}

function best(candidates: Candidate[]) {
  return [...candidates].sort((a, b) =>
    (b.score ?? 0) - (a.score ?? 0) || (a.rateAmountMinor ?? Number.MAX_SAFE_INTEGER) - (b.rateAmountMinor ?? Number.MAX_SAFE_INTEGER))[0]
}

async function selectShortlist(page: Page, tenantId: string, token: string, shortlist: Shortlist, selectedCandidateIds: string[]) {
  return command<Shortlist>(page,
    `/api/v1/tenants/${tenantId}/shortlist-versions/${shortlist.id}:select`, token,
    { selectedCandidateIds,
      reason: 'Selected the best currently eligible placements across every funded channel; supplier availability remains subject to booking reconfirmation.' },
    shortlist.version)
}

async function generatePlan(page: Page, tenantId: string, token: string) {
  return command<Plan>(page,
    `/api/v1/tenants/${tenantId}/brief-versions/${BRIEF_VERSION_ID}/media-plans:generate`, token, {}, undefined)
}

async function resolvePlanObjections(page: Page, tenantId: string, token: string, initial: Plan) {
  let plan = initial
  for (const objection of plan.objections.filter(item => item.resolution === null)) {
    const reason = objection.code === 'SUPPLY_UNCONFIRMED'
      ? 'Accepted for proposal planning only. Supplier confirmation is still required before booking.'
      : objection.code === 'COMMERCIAL_EVIDENCE_INCOMPLETE'
        ? 'Accepted for proposal planning with the published undated rate-card evidence; rates must be reconfirmed before booking.'
        : 'Reviewed and accepted for proposal planning; the limitation remains visible and must be resolved before booking.'
    plan = await command<Plan>(page,
      `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}/objections/${objection.code}:resolve`,
      token, { resolution: 'ACCEPTED_WITH_REASON', reason }, plan.version)
  }
  return plan
}

async function approvePlan(page: Page, tenantId: string, token: string, plan: Plan) {
  const approved = await command<Plan>(page,
    `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}:approve`, token,
    { reason: 'Approved for client proposal preparation; supplier booking remains a later controlled step.' }, plan.version)
  expect(approved.status).toBe('APPROVED')
  return approved
}

async function ensureProposal(page: Page, tenantId: string, token: string, plan: ApprovedPlan) {
  const listResponse = await page.request.get(`/api/v1/tenants/${tenantId}/proposals`)
  expect(listResponse.ok(), await listResponse.text()).toBe(true)
  const existing = (await listResponse.json() as Proposal[]).find(item => item.briefId === BRIEF_ID)
  if (existing) return existing
  return command<Proposal>(page,
    `/api/v1/tenants/${tenantId}/briefs/${BRIEF_ID}/proposals:generate`, token,
    {
      title: 'Rayetsa Furniture — Q4 2026 Campaign Proposal',
      terms: 'Media rates and availability remain subject to supplier reconfirmation before booking. The approved media plan is the commercial source of truth.',
      expiryAtUtc: '2026-09-30T21:59:59Z',
      options: [{
        planVersionId: plan.id,
        label: 'Recommended integrated plan',
        outcome: 'Build awareness and measurable demand across the approved OOH, radio, digital and social mix within the Rayetsa Furniture brief.',
      }],
    }, undefined)
}

async function getPlanning(page: Page, tenantId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/brief-versions/${BRIEF_VERSION_ID}/planning`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as Planning
}

async function listApprovedPlans(page: Page, tenantId: string) {
  const response = await page.request.get(`/api/v1/tenants/${tenantId}/briefs/${BRIEF_ID}/approved-plans`)
  expect(response.ok(), await response.text()).toBe(true)
  return await response.json() as ApprovedPlan[]
}

function splitBudget(total: number) {
  const base = Math.floor(total / MARKETS.length)
  return MARKETS.map((geography, index) => ({
    geography,
    budgetMinor: index === MARKETS.length - 1 ? total - base * (MARKETS.length - 1) : base,
  }))
}

async function command<T>(page: Page, path: string, token: string, data: unknown, version: number | undefined) {
  const response = await page.request.post(path, { data, headers: commandHeaders(token, version) })
  const body = await response.text()
  expect(response.status(), body).toBe(200)
  return JSON.parse(body) as T
}

function commandHeaders(token: string, version?: number) {
  return {
    Origin: 'http://localhost:3017',
    'X-CSRF-TOKEN': token,
    'Idempotency-Key': crypto.randomUUID(),
    'X-Correlation-ID': crypto.randomUUID(),
    ...(version === undefined ? {} : { 'If-Match': `"${version}"` }),
  }
}

async function localTenant(page: Page) {
  const response = await page.request.get('/api/v1/workspaces')
  expect(response.ok(), await response.text()).toBe(true)
  const workspaces = await response.json() as Array<{ tenantId: string; name: string }>
  return (workspaces.find(item => /Advertified Local/i.test(item.name)) ?? workspaces[0]).tenantId
}

async function antiforgeryToken(page: Page) {
  const response = await page.request.get('/api/v1/session')
  expect(response.ok(), await response.text()).toBe(true)
  return (await response.json() as { antiforgeryToken: string }).antiforgeryToken
}

async function signIn(page: Page) {
  await page.goto('/sign-in')
  await page.getByRole('button', { name: /Continue to Advertified/ }).click()
  await page.getByRole('button', { name: /Advertified Local/ }).click()
  await expect(page).toHaveURL(/\/home$/)
}
