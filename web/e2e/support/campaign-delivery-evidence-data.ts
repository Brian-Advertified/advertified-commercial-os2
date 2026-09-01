import {
  deliveryIds,
  deliveryNow,
  type DeliveryFixtureState,
} from './campaign-delivery-model';

export function creativeWorkspaceFixture(state: DeliveryFixtureState) {
  return {
    readyForApproval: state.brandApproved && state.supplierApproved,
    requirements: [{
      id: deliveryIds.requirement,
      campaignId: deliveryIds.campaign,
      bookingId: deliveryIds.booking,
      mediaPlanLineId: deliveryIds.mediaLine,
      supplierTenantId: deliveryIds.supplierTenant,
      channel: 'DOOH',
      flightStart: '2026-08-01',
      flightEnd: '2026-08-31',
      formatCode: 'DIGITAL_1920X1080',
      width: 1920,
      height: 1080,
      requiredMediaType: 'image/png',
      maximumBytes: 5242880,
      instructions: 'Supply a 1920 by 1080 PNG with approved campaign copy.',
      asset: state.creativeAssetCreated ? creativeAssetFixture(state) : null,
    }],
  };
}

export function creativeAssetFixture(state: DeliveryFixtureState) {
  return {
    id: deliveryIds.asset,
    requirementId: deliveryIds.requirement,
    version: 1 + Number(state.brandApproved) + Number(state.supplierApproved),
    currentVersion: {
      id: deliveryIds.assetVersion,
      versionNumber: 1,
      fileName: 'campaign-artwork.png',
      mediaType: 'image/png',
      sizeBytes: 128,
      contentSha256: '3'.repeat(64),
      approvedCopy: 'Move from interest to a clear next step.',
      commercialSnapshotJson: '{}',
      createdBy: deliveryIds.user,
      createdAtUtc: deliveryNow,
      brandReview: state.brandApproved
        ? creativeReviewFixture('BRAND', deliveryIds.tenant)
        : null,
      supplierReview: state.supplierApproved
        ? creativeReviewFixture('SUPPLIER', deliveryIds.supplierTenant)
        : null,
    },
  };
}

function creativeReviewFixture(reviewType: string, reviewerTenantId: string) {
  return {
    reviewType,
    decision: 'APPROVED',
    rightsStatus: reviewType === 'BRAND' ? 'APPROVED' : null,
    evidenceReference: `${reviewType.toLowerCase()}-review:001`,
    reason: `${reviewType} review passed for the current file version.`,
    reviewedBy: deliveryIds.user,
    reviewerTenantId,
    reviewedAtUtc: deliveryNow,
  };
}

export function supplierCreativeFixture(state: DeliveryFixtureState) {
  return {
    assetId: deliveryIds.asset,
    campaignId: deliveryIds.campaign,
    requirementId: deliveryIds.requirement,
    channel: 'DOOH',
    formatCode: 'DIGITAL_1920X1080',
    width: 1920,
    height: 1080,
    requiredMediaType: 'image/png',
    maximumBytes: 5242880,
    instructions: 'Supply a 1920 by 1080 PNG with approved campaign copy.',
    versionId: deliveryIds.assetVersion,
    versionNumber: 1,
    fileName: 'campaign-artwork.png',
    mediaType: 'image/png',
    sizeBytes: 128,
    contentSha256: '3'.repeat(64),
    supplierDecision: state.supplierApproved ? 'APPROVED' : null,
    version: 1 + Number(state.supplierApproved),
  };
}

export function deliveryProofRequestFixture(state: DeliveryFixtureState) {
  const latestStatus = state.proofApproved ? 'APPROVED' : 'REVIEW_REQUIRED';
  return {
    campaignId: deliveryIds.campaign,
    bookingId: deliveryIds.booking,
    supplierName: 'Metro Media Owner',
    productName: 'N1 Digital Billboard',
    channel: 'DOOH',
    geography: 'Johannesburg',
    flightStart: '2026-08-01',
    flightEnd: '2026-08-31',
    proofRequestedAtUtc: deliveryNow,
    proofRequestReason: 'Submit proof for the exact confirmed Booking.',
    latestProofId: state.proofSubmitted ? deliveryIds.proof : null,
    latestProofStatus: state.proofSubmitted ? latestStatus : null,
  };
}

export function deliveryProofFixture(state: DeliveryFixtureState) {
  return {
    id: deliveryIds.proof,
    campaignId: deliveryIds.campaign,
    bookingId: deliveryIds.booking,
    supplierTenantId: deliveryIds.supplierTenant,
    proofType: 'PHOTO',
    fileName: 'delivery-proof.png',
    mediaType: 'image/png',
    sizeBytes: 128,
    contentSha256: '4'.repeat(64),
    signatureValidated: true,
    malwareScanStatus: 'CLEAN',
    capturedAtUtc: '2026-08-31T09:00:00Z',
    locationDescription: 'N1 digital billboard, Johannesburg',
    latitude: -26.2041,
    longitude: 28.0473,
    sourceReference: 'supplier-camera:001',
    submissionReason: 'Proof captured inside the confirmed delivery window.',
    status: state.proofApproved ? 'APPROVED' : 'REVIEW_REQUIRED',
    submittedBy: deliveryIds.user,
    submitterTenantId: deliveryIds.supplierTenant,
    submittedAtUtc: deliveryNow,
    reviewedBy: state.proofApproved ? deliveryIds.user : null,
    reviewedAtUtc: state.proofApproved ? deliveryNow : null,
    reviewReason: state.proofApproved
      ? 'Proof matches the exact Booking and flight.'
      : null,
    version: state.proofApproved ? 2 : 1,
    updatedAtUtc: deliveryNow,
  };
}

export function performanceEvidenceFixture(state: DeliveryFixtureState) {
  return {
    id: deliveryIds.evidence,
    campaignId: deliveryIds.campaign,
    sourceReference: 'platform-export:campaign-001',
    fileName: 'performance.csv',
    mediaType: 'text/csv',
    sizeBytes: 128,
    contentSha256: '5'.repeat(64),
    signatureValidated: true,
    malwareScanStatus: 'CLEAN',
    capturedAtUtc: '2026-08-31T09:30:00Z',
    methodology: 'Count verified platform responses during the booked flight.',
    limitations: ['The source does not establish causal attribution.'],
    qualityStatus: 'VERIFIED',
    metrics: [{
      id: deliveryIds.metric,
      metricType: 'CONVERSIONS',
      value: 42,
      unit: 'COUNT',
      periodStart: '2026-08-01',
      periodEnd: '2026-08-31',
      sourceLocator: 'platform-export:campaign-001:conversions',
    }],
    status: state.evidenceApproved ? 'APPROVED' : 'REVIEW_REQUIRED',
    reviewerUserId: deliveryIds.reviewer,
    submittedBy: deliveryIds.user,
    submittedAtUtc: deliveryNow,
    reviewedBy: state.evidenceApproved ? deliveryIds.reviewer : null,
    reviewedAtUtc: state.evidenceApproved ? deliveryNow : null,
    reviewReason: state.evidenceApproved
      ? 'The sourced metric and limitations are complete.'
      : null,
    version: state.evidenceApproved ? 2 : 1,
    updatedAtUtc: deliveryNow,
  };
}

export function measurementReportFixture(state: DeliveryFixtureState) {
  return {
    id: deliveryIds.report,
    campaignId: deliveryIds.campaign,
    versionNumber: 1,
    campaignVersion: state.campaignVersion,
    measurementPlan: ['Track verified delivery and sourced response metrics'],
    evidence: [performanceEvidenceFixture({ ...state, evidenceApproved: true })],
    interpretation: {
      executiveSummary: 'The approved source recorded 42 conversions during the booked flight.',
      findings: [{
        title: 'Sourced conversion result',
        summary: 'The platform export contains 42 reviewed conversion records.',
        metricIds: [deliveryIds.metric],
        causalityStatus: 'NOT_ESTABLISHED',
      }],
      limitations: ['The source does not establish causal attribution.'],
      learningProposals: [{
        text: 'Test a separately approved response baseline in a future campaign.',
        requiresNewApproval: true,
      }],
      causalityStatus: 'NOT_ESTABLISHED',
    },
    status: state.reportApproved ? 'APPROVED' : 'REVIEW_REQUIRED',
    approverUserId: deliveryIds.reviewer,
    generatedBy: deliveryIds.user,
    generatedAtUtc: deliveryNow,
    reviewedBy: state.reportApproved ? deliveryIds.reviewer : null,
    reviewedAtUtc: state.reportApproved ? deliveryNow : null,
    reviewReason: state.reportApproved
      ? 'The report retains all sourced facts and limitations.'
      : null,
    version: state.reportApproved ? 2 : 1,
    updatedAtUtc: deliveryNow,
  };
}
