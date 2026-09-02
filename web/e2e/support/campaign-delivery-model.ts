export const deliveryIds = {
  tenant: '91000000-0000-0000-0000-000000000001',
  user: '92000000-0000-0000-0000-000000000001',
  reviewer: '93000000-0000-0000-0000-000000000001',
  proposal: '94000000-0000-0000-0000-000000000001',
  option: '95000000-0000-0000-0000-000000000001',
  decision: '96000000-0000-0000-0000-000000000001',
  brief: '97000000-0000-0000-0000-000000000001',
  briefVersion: '98000000-0000-0000-0000-000000000001',
  plan: '99000000-0000-0000-0000-000000000001',
  campaign: 'a1000000-0000-0000-0000-000000000001',
  booking: 'a2000000-0000-0000-0000-000000000001',
  supplierTenant: 'a3000000-0000-0000-0000-000000000001',
  listing: 'a4000000-0000-0000-0000-000000000001',
  mediaLine: 'a5000000-0000-0000-0000-000000000001',
  purchaseOrder: 'a6000000-0000-0000-0000-000000000001',
  invoice: 'a7000000-0000-0000-0000-000000000001',
  payment: 'a8000000-0000-0000-0000-000000000001',
  requirement: 'a9000000-0000-0000-0000-000000000001',
  asset: 'aa000000-0000-0000-0000-000000000001',
  assetVersion: 'ab000000-0000-0000-0000-000000000001',
  proof: 'ac000000-0000-0000-0000-000000000001',
  evidence: 'ad000000-0000-0000-0000-000000000001',
  metric: 'ae000000-0000-0000-0000-000000000001',
  report: 'af000000-0000-0000-0000-000000000001',
} as const;

export const deliveryNow = '2026-08-31T10:00:00Z';

export type DeliveryFixtureState = {
  purchaseOrderStatus: string | null;
  invoiceCreated: boolean;
  paymentStatus: string | null;
  campaignStatus: string;
  campaignVersion: number;
  creativeRequested: boolean;
  creativeAssetCreated: boolean;
  brandApproved: boolean;
  supplierApproved: boolean;
  proofSubmitted: boolean;
  proofApproved: boolean;
  evidenceSubmitted: boolean;
  evidenceApproved: boolean;
  reportGenerated: boolean;
  reportApproved: boolean;
};

export function createDeliveryState(): DeliveryFixtureState {
  return {
    purchaseOrderStatus: null,
    invoiceCreated: false,
    paymentStatus: null,
    campaignStatus: 'PLANNED',
    campaignVersion: 1,
    creativeRequested: false,
    creativeAssetCreated: false,
    brandApproved: false,
    supplierApproved: false,
    proofSubmitted: false,
    proofApproved: false,
    evidenceSubmitted: false,
    evidenceApproved: false,
    reportGenerated: false,
    reportApproved: false,
  };
}

export function sessionFixture() {
  return {
    authenticated: true,
    antiforgeryToken: 'csrf-delivery',
    expiresAtUtc: '2026-08-31T20:00:00Z',
    signInPath: null,
    signOutPath: null,
  };
}

export function workspaceFixture() {
  return {
    membershipId: 'b1000000-0000-0000-0000-000000000001',
    tenantId: deliveryIds.tenant,
    name: 'Advertified Operations',
    slug: 'advertified-operations',
    roleCode: 'platform_admin',
    version: 1,
  };
}

export function reviewerFixture() {
  return {
    userId: deliveryIds.reviewer,
    displayName: 'Amina Client Approver',
    email: 'amina@example.test',
    role: 'advertiser_approver',
  };
}
