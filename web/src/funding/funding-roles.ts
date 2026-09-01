import { masterDataCodes } from '../generated/master-data-codes'

const roles = masterDataCodes.roles

export const fundingViewerRoles = new Set<string>([
  roles.platformAdmin,
  roles.internalPlanner,
  roles.agencyAdmin,
  roles.agencyCampaignUser,
  roles.advertiserAdmin,
  roles.advertiserApprover,
])

export const purchaseOrderSubmitterRoles = new Set<string>([
  roles.platformAdmin,
  roles.internalPlanner,
  roles.agencyAdmin,
  roles.agencyCampaignUser,
])

export const fundingAdministratorRoles = new Set<string>([
  roles.platformAdmin,
])

export const paymentStarterRoles = new Set<string>([
  roles.platformAdmin,
  roles.internalPlanner,
  roles.agencyAdmin,
  roles.agencyCampaignUser,
])
