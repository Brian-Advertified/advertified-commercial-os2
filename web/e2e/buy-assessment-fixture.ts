// Synthetic UI evidence only; never imported into retained inventory.
export const buyAssessmentFixture = {
  campaignSupplierCostMinor: 300_000, currency: 'ZAR', reach: 10_000, impressions: 40_000,
  averageFrequency: 4, costPerThousandImpressionsMinor: 7_500,
  costPerPersonReachedMinor: 30, universe: 'Synthetic audience',
  measurementPeriod: '2026-09-01/2026-09-30', measurementSource: 'Synthetic study',
  methodology: 'Deterministic UI fixture', isTargetAudience: false,
  digitalExposure: { spotLengthSeconds: 5, slotLengthSeconds: 5, loopLengthSeconds: 60,
    playsPerLoop: 1, loopSharePercent: 8.3333 },
  evidenceGaps: ['buyAssessment.measurementNotForecast'],
}

export function combinationFixture(candidateId: string) {
  return { alternatives: [{ candidateIds: [candidateId], campaignSupplierCostMinor: 300_000,
    currency: 'ZAR', channelCosts: [{ channel: 'OOH', supplierCostMinor: 300_000, budgetMinor: 1_000_000 }],
    coveredRequirementIds: [], evidenceGaps: ['campaignCombination.uniqueReachAndDuplication'],
  }], searchTruncated: true, candidatesConsidered: 1, missingCostCandidateCount: 0 }
}
