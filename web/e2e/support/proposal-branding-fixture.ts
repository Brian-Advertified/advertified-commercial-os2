// Deterministic human approval evidence for journeys testing later proposal stages.
export function authorisedUnbrandedFixture(approvedBy: string, approvedAtUtc: string) {
  return {
    status: 'UNBRANDED_AUTHORISED', agencyName: 'Fixture agency', clientBrandName: 'Fixture client',
    primaryColour: null, secondaryColour: null, agencyAsset: null, clientAsset: null,
    unbrandedApprovedBy: approvedBy, unbrandedApprovedAtUtc: approvedAtUtc,
    unbrandedApprovalReason: 'Synthetic authorised unbranded document for the workflow fixture.',
  }
}
