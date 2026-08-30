import { masterDataCodes } from '../generated/master-data-codes'

export const bookingBuyerRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

export const bookingSupplierRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.supplierAdmin,
  masterDataCodes.roles.supplierUser,
])

export const bookingViewerRoles = new Set<string>([
  ...bookingBuyerRoles,
  ...bookingSupplierRoles,
  masterDataCodes.roles.advertiserAdmin,
  masterDataCodes.roles.advertiserApprover,
])
