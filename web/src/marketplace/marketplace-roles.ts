import { masterDataCodes } from '../generated/master-data-codes'

export const marketplaceBuyerRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

export const marketplaceSupplierRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.supplierAdmin,
  masterDataCodes.roles.supplierUser,
])

export const marketplaceViewerRoles = new Set<string>([
  ...marketplaceBuyerRoles,
  ...marketplaceSupplierRoles,
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.advertiserAdmin,
  masterDataCodes.roles.advertiserApprover,
])
