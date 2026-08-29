export const opportunityCodes = {
  status: {
    created: 'CREATED',
    qualifying: 'QUALIFYING',
    pending: 'PENDING',
    draft: 'DRAFT',
    inReview: 'IN_REVIEW',
    approved: 'APPROVED',
    strategyReady: 'STRATEGY_READY',
  },
  sourceType: { suppliedText: 'SUPPLIED_TEXT' },
  policyBasis: { ownerSupplied: 'OWNER_SUPPLIED' },
  claimType: { businessContext: 'BUSINESS_CONTEXT' },
  reviewDecision: { approve: 'APPROVE' },
  angleStatus: { selected: 'SELECTED' },
  objectionResolution: { addressed: 'ADDRESSED' },
} as const
