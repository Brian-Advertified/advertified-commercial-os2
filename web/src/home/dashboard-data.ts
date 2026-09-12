import { api } from '../api/client'
import { bookingApi } from '../api/booking-client'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProductPage } from '../api/inventory-schemas'
import { marketplaceApi } from '../api/marketplace-client'
import type { MarketplaceRfq } from '../api/marketplace-schemas'
import { opportunityApi } from '../api/opportunity-client'
import { planningApi } from '../api/planning-client'
import type { PlanningSummary } from '../api/planning-schemas'
import { proposalApi } from '../api/proposal-client'
import type { ProposalSummary } from '../api/proposal-schemas'
import type { CurrentUser, HumanTask, Tenant, Workspace } from '../api/schemas'
import { masterDataCodes } from '../generated/master-data-codes'

export type DashboardData = {
  tenant: Tenant
  user: CurrentUser
  campaigns: Campaign[]
  bookings: Booking[]
  tasks: HumanTask[]
  planning: PlanningSummary[]
  proposals: ProposalSummary[]
  rfqs: MarketplaceRfq[]
  inventory: InventoryProductPage | null
}

const operatorRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])
const supplierRoles = new Set<string>([
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.supplierUser,
])
const advertiserRoles = new Set<string>([
  masterDataCodes.roles.advertiserAdmin,
  masterDataCodes.roles.advertiserApprover,
])

export function homeAudience(roleCode: string) {
  if (operatorRoles.has(roleCode)) return 'operator'
  if (supplierRoles.has(roleCode)) return 'supplier'
  if (roleCode === masterDataCodes.roles.influencerRep) return 'creator'
  if (advertiserRoles.has(roleCode)) return 'advertiser'
  return null
}

export async function loadDashboard(workspace: Workspace): Promise<DashboardData> {
  const audience = homeAudience(workspace.roleCode)
  if (!audience) throw new Error('Dashboard access is not available for this workspace role.')
  const { tenantId } = workspace
  const supply = audience === 'supplier' || audience === 'creator'
  const [tenant, userProfile, bookings, tasks] = await Promise.all([
    api.getTenant(tenantId), api.getCurrentUser(),
    bookingApi.list(tenantId), opportunityApi.listTasks(tenantId),
  ])
  // Only load the data used by this role. A failed authorised request is not an empty result.
  const [campaigns, planning, proposals, rfqs, inventory] = await Promise.all([
    supply ? Promise.resolve([]) : campaignApi.list(tenantId),
    supply ? Promise.resolve([]) : planningApi.list(tenantId),
    supply ? Promise.resolve([]) : proposalApi.list(tenantId),
    supply ? marketplaceApi.listRfqs(tenantId).then(page => page.items) : Promise.resolve([]),
    audience === 'advertiser' ? Promise.resolve(null) : inventoryApi.search(tenantId, {}),
  ])
  return { tenant, user: userProfile.user, campaigns, bookings, tasks, planning, proposals, rfqs, inventory }
}
