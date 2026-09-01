import {
  creativeWorkspaceFixture,
  deliveryProofFixture,
  measurementReportFixture,
  performanceEvidenceFixture,
} from './campaign-delivery-evidence-data';
import {
  deliveryIds,
  deliveryNow,
  type DeliveryFixtureState,
} from './campaign-delivery-model';

export {
  creativeAssetFixture,
  deliveryProofFixture,
  deliveryProofRequestFixture,
  measurementReportFixture,
  performanceEvidenceFixture,
  supplierCreativeFixture,
} from './campaign-delivery-evidence-data';
export {
  createDeliveryState,
  deliveryIds,
  reviewerFixture,
  sessionFixture,
  workspaceFixture,
  type DeliveryFixtureState,
} from './campaign-delivery-model';

export function fundingWorkspaceFixture(state: DeliveryFixtureState) {
  return {
    purchaseOrders: state.purchaseOrderStatus ? [purchaseOrderFixture(state)] : [],
    invoices: state.invoiceCreated ? [invoiceFixture()] : [],
    payments: state.paymentStatus ? [paymentFixture(state)] : [],
  };
}

export function purchaseOrderFixture(state: DeliveryFixtureState) {
  const approved = state.purchaseOrderStatus === 'APPROVED';
  return {
    id: deliveryIds.purchaseOrder,
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    proposalDecisionId: deliveryIds.decision,
    purchaseOrderNumber: 'PO-DELIVERY-001',
    contentSha256: '1'.repeat(64),
    mediaType: 'application/pdf',
    sizeBytes: 256,
    amountMinor: 10000000,
    currency: 'ZAR',
    status: state.purchaseOrderStatus,
    submittedBy: deliveryIds.user,
    submittedAtUtc: deliveryNow,
    approvedBy: approved ? deliveryIds.user : null,
    approvedAtUtc: approved ? deliveryNow : null,
    reconciliationReason: approved
      ? 'The signed PO matches the client-selected option.'
      : null,
    version: approved ? 2 : 1,
  };
}

export function invoiceFixture() {
  return {
    id: deliveryIds.invoice,
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    purchaseOrderId: deliveryIds.purchaseOrder,
    invoiceNumber: 'INV-DELIVERY-001',
    subtotalMinor: 8695652,
    feesMinor: 0,
    vatMinor: 1304348,
    totalMinor: 10000000,
    currency: 'ZAR',
    status: 'ISSUED',
    issuedBy: deliveryIds.user,
    issuedAtUtc: deliveryNow,
    version: 1,
  };
}

export function paymentFixture(state: DeliveryFixtureState) {
  const confirmed = state.paymentStatus === 'CONFIRMED';
  return {
    id: deliveryIds.payment,
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    purchaseOrderId: deliveryIds.purchaseOrder,
    invoiceId: deliveryIds.invoice,
    methodCode: 'MANUAL_EFT',
    amountMinor: 10000000,
    currency: 'ZAR',
    status: state.paymentStatus,
    startedBy: deliveryIds.user,
    startedAtUtc: deliveryNow,
    reconciledBy: confirmed ? deliveryIds.user : null,
    reconciledAtUtc: confirmed ? deliveryNow : null,
    reconciliationReference: confirmed ? 'BANK-REF-001' : null,
    reconciliationReason: confirmed
      ? 'Receipt reconciled to the issued invoice.'
      : null,
    receiptSha256: confirmed ? '2'.repeat(64) : null,
    version: confirmed ? 2 : 1,
  };
}

export function bookingFixture() {
  return {
    id: deliveryIds.booking,
    buyerTenantId: deliveryIds.tenant,
    supplierTenantId: deliveryIds.supplierTenant,
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    proposalDecisionId: deliveryIds.decision,
    planVersionId: deliveryIds.plan,
    mediaPlanLineId: deliveryIds.mediaLine,
    marketplaceListingVersionId: deliveryIds.listing,
    supplierName: 'Metro Media Owner',
    productName: 'N1 Digital Billboard',
    channel: 'DOOH',
    geography: 'Johannesburg',
    flightStart: '2026-08-01',
    flightEnd: '2026-08-31',
    runningPeriods: 1,
    quantity: 1,
    clientPriceMinor: 10000000,
    feesMinor: 0,
    vatMinor: 1304348,
    currency: 'ZAR',
    terms: 'Exact booked line and current supplier confirmation.',
    status: 'CONFIRMED',
    createdBy: deliveryIds.user,
    createdAtUtc: deliveryNow,
    requestedBy: deliveryIds.user,
    requestedAtUtc: deliveryNow,
    requestReason: 'Confirm exact selected line.',
    confirmedBy: deliveryIds.user,
    confirmedAtUtc: deliveryNow,
    confirmationReason: 'Rate, dates and availability confirmed.',
    termsAccepted: true,
    version: 3,
    updatedAtUtc: deliveryNow,
  };
}

export function campaignFixture(state: DeliveryFixtureState) {
  return {
    ...campaignIdentity(state),
    ...bookingAudit(state),
    ...creativeAudit(state),
    ...deliveryAudit(state),
    creative: state.creativeRequested ? creativeWorkspaceFixture(state) : null,
    deliveryProofs: state.proofSubmitted ? [deliveryProofFixture(state)] : [],
    performanceEvidence: state.evidenceSubmitted
      ? [performanceEvidenceFixture(state)]
      : [],
    measurementReports: state.reportGenerated
      ? [measurementReportFixture(state)]
      : [],
  };
}

function campaignIdentity(state: DeliveryFixtureState) {
  return {
    id: deliveryIds.campaign,
    briefId: deliveryIds.brief,
    briefVersionId: deliveryIds.briefVersion,
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    proposalDecisionId: deliveryIds.decision,
    planVersionId: deliveryIds.plan,
    paymentIntentId: deliveryIds.payment,
    fundingStatus: 'CONFIRMED',
    title: 'Gauteng Growth Campaign',
    startDate: '2026-08-01',
    endDate: '2026-08-31',
    ownerUserId: deliveryIds.user,
    measurementPlanJson: '["Track verified delivery and sourced response metrics"]',
    status: state.campaignStatus,
    requiredBookingCount: 1,
    confirmedBookingCount: 1,
    nextActionPermission: null,
    createdBy: deliveryIds.user,
    createdAtUtc: deliveryNow,
    version: state.campaignVersion,
    updatedAtUtc: deliveryNow,
  };
}

function bookingAudit(state: DeliveryFixtureState) {
  const confirmed = state.campaignStatus !== 'PLANNED';
  return {
    bookingsConfirmedBy: confirmed ? deliveryIds.user : null,
    bookingsConfirmedAtUtc: confirmed ? deliveryNow : null,
    bookingConfirmationReason: confirmed ? 'Exact Booking coverage confirmed.' : null,
  };
}

function creativeAudit(state: DeliveryFixtureState) {
  const approved = ['READY', 'LIVE', 'COMPLETED'].includes(state.campaignStatus);
  return {
    creativeRequestedBy: state.creativeRequested ? deliveryIds.user : null,
    creativeRequestedAtUtc: state.creativeRequested ? deliveryNow : null,
    creativeRequestReason: state.creativeRequested
      ? 'Booked format production requested.'
      : null,
    creativeApprovedBy: approved ? deliveryIds.user : null,
    creativeApprovedAtUtc: approved ? deliveryNow : null,
    creativeApprovalReason: approved ? 'Current reviewed creative is ready.' : null,
  };
}

function deliveryAudit(state: DeliveryFixtureState) {
  const started = ['LIVE', 'COMPLETED'].includes(state.campaignStatus);
  const completed = state.campaignStatus === 'COMPLETED';
  return {
    startedBy: started ? deliveryIds.user : null,
    startedAtUtc: started ? deliveryNow : null,
    startReason: started ? 'Booked window opened and all readiness checks passed.' : null,
    completedBy: completed ? deliveryIds.user : null,
    completedAtUtc: completed ? deliveryNow : null,
    completionReason: completed ? 'Booked delivery window closed.' : null,
    proofRequestedBy: completed ? deliveryIds.user : null,
    proofRequestedAtUtc: completed ? deliveryNow : null,
    proofRequestReason: completed
      ? 'Submit proof for the exact confirmed Booking.'
      : null,
  };
}
