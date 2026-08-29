import { masterDataCodes } from '../generated/master-data-codes'

export const opportunityCodes = {
  status: {
    created: masterDataCodes.lifecycleStatuses.created,
    qualifying: masterDataCodes.lifecycleStatuses.qualifying,
    pending: masterDataCodes.lifecycleStatuses.pending,
    draft: masterDataCodes.lifecycleStatuses.draft,
    inReview: masterDataCodes.lifecycleStatuses.inReview,
    approved: masterDataCodes.lifecycleStatuses.approved,
    strategyReady: masterDataCodes.lifecycleStatuses.strategyReady,
    briefReady: masterDataCodes.lifecycleStatuses.briefReady,
    planning: masterDataCodes.lifecycleStatuses.planning,
  },
  sourceType: { suppliedText: masterDataCodes.evidenceSourceTypes.suppliedText },
  policyBasis: { ownerSupplied: masterDataCodes.evidencePolicyBases.ownerSupplied },
  claimType: { businessContext: masterDataCodes.evidenceClaimTypes.businessContext },
  reviewDecision: { approve: masterDataCodes.evidenceReviewDecisions.approve },
  angleStatus: { selected: masterDataCodes.opportunityAngleStatuses.selected },
  objectionResolution: { addressed: masterDataCodes.objectionResolutions.addressed },
  currency: { zar: masterDataCodes.currencies.zar },
  briefConfirmerRole: {
    internalPlanner: masterDataCodes.roles.internalPlanner,
    agencyAdmin: masterDataCodes.roles.agencyAdmin,
    agencyCampaignUser: masterDataCodes.roles.agencyCampaignUser,
  },
} as const
