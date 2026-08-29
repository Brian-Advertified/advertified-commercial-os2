export const inventoryCodes = {
  role: {
    platformAdmin: 'platform_admin',
    inventoryOperations: 'inventory_ops',
    supplierAdmin: 'supplier_admin',
  },
  decision: { approve: 'APPROVE', reject: 'REJECT', edit: 'EDIT' },
  rejectionReason: {
    missingInformation: 'MISSING_INFO',
    duplicate: 'DUPLICATE',
    qualityIssue: 'QUALITY_ISSUE',
    staleRate: 'STALE_RATE',
  },
  importStatus: { uploaded: 'UPLOADED', reviewRequired: 'REVIEW_REQUIRED' },
  candidateStatus: { approved: 'APPROVED', reviewRequired: 'REVIEW_REQUIRED' },
  availability: { unknown: 'UNKNOWN' },
  channel: { ooh: 'OOH', dooh: 'DOOH', radio: 'RADIO' },
} as const

export type InventoryDecision = typeof inventoryCodes.decision[keyof typeof inventoryCodes.decision]
