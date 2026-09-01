import { masterDataCodes } from '../generated/master-data-codes'

const roles = masterDataCodes.roles
const agencyOperators = [
  roles.platformAdmin,
  roles.internalPlanner,
  roles.agencyAdmin,
  roles.agencyCampaignUser,
]
const advertiserReviewers = [
  roles.platformAdmin,
  roles.advertiserAdmin,
  roles.advertiserApprover,
]
const supplierOperators = [
  roles.platformAdmin,
  roles.inventoryOps,
  roles.supplierAdmin,
  roles.supplierUser,
]

export const campaignViewerRoles = new Set<string>([
  ...agencyOperators,
  roles.advertiserAdmin,
  roles.advertiserApprover,
])

export const campaignBookingConfirmerRoles = new Set<string>(agencyOperators)
export const creativeRequesterRoles = new Set<string>(agencyOperators)
export const creativeUploaderRoles = new Set<string>(agencyOperators)
export const creativeBrandReviewerRoles = new Set<string>(advertiserReviewers)
export const creativeApproverRoles = new Set<string>(advertiserReviewers)
export const campaignDeliveryOperatorRoles = new Set<string>(agencyOperators)
export const supplierCreativeReviewerRoles = new Set<string>(supplierOperators)
export const deliveryProofSubmitterRoles = new Set<string>(supplierOperators)
export const deliveryProofReviewerRoles = new Set<string>(agencyOperators)
export const performanceEvidenceSubmitterRoles = new Set<string>(agencyOperators)
export const performanceEvidenceReviewerRoles = new Set<string>([
  roles.platformAdmin,
  roles.internalPlanner,
  roles.advertiserAdmin,
  roles.advertiserApprover,
])
export const measurementReportGeneratorRoles = new Set<string>(agencyOperators)
export const measurementReportReviewerRoles = new Set<string>([
  roles.platformAdmin,
  roles.internalPlanner,
  roles.advertiserAdmin,
  roles.advertiserApprover,
])
