import { masterDataCodes } from '../generated/master-data-codes'

export const inventoryCodes = {
  role: {
    platformAdmin: masterDataCodes.roles.platformAdmin,
    inventoryOperations: masterDataCodes.roles.inventoryOps,
    supplierUser: masterDataCodes.roles.supplierUser,
    influencerRep: masterDataCodes.roles.influencerRep,
  },
  decision: {
    approve: masterDataCodes.inventoryReviewDecisions.approve,
    reject: masterDataCodes.inventoryReviewDecisions.reject,
    edit: masterDataCodes.inventoryReviewDecisions.edit,
  },
  rejectionReason: {
    missingInformation: masterDataCodes.rejectionReasons.missingInfo,
    duplicate: masterDataCodes.rejectionReasons.duplicate,
    qualityIssue: masterDataCodes.rejectionReasons.qualityIssue,
    staleRate: masterDataCodes.rejectionReasons.staleRate,
  },
  importStatus: {
    uploaded: masterDataCodes.lifecycleStatuses.uploaded,
    reviewRequired: masterDataCodes.lifecycleStatuses.reviewRequired,
  },
  candidateStatus: {
    approved: masterDataCodes.lifecycleStatuses.approved,
    reviewRequired: masterDataCodes.lifecycleStatuses.reviewRequired,
  },
  extractionStatus: masterDataCodes.inventoryExtractionAttemptStatuses,
  availability: { planningAvailable: masterDataCodes.availabilityStatuses.planningAvailable },
  assetRights: {
    approved: masterDataCodes.assetRightsStatuses.approved,
    restricted: masterDataCodes.assetRightsStatuses.restricted,
    revoked: masterDataCodes.assetRightsStatuses.revoked,
  },
  assetRightsScope: {
    internalPlanning: masterDataCodes.assetRightsScopes.internalPlanning,
    namedClientProposal: masterDataCodes.assetRightsScopes.namedClientProposal,
    marketplaceDisplay: masterDataCodes.assetRightsScopes.marketplaceDisplay,
    publicMarketingSocial: masterDataCodes.assetRightsScopes.publicMarketingSocial,
  },
  duplicateStatus: {
    open: masterDataCodes.inventoryDuplicateStatuses.open,
  },
  channel: {
    ooh: masterDataCodes.channels.ooh,
    dooh: masterDataCodes.channels.dooh,
    radio: masterDataCodes.channels.radio,
  },
} as const

export type InventoryDecision = typeof inventoryCodes.decision[keyof typeof inventoryCodes.decision]
