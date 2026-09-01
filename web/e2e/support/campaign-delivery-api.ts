import { expect, type Page, type Route } from '@playwright/test';
import {
  bookingFixture,
  campaignFixture,
  createDeliveryState,
  creativeAssetFixture,
  deliveryIds,
  deliveryProofFixture,
  deliveryProofRequestFixture,
  fundingWorkspaceFixture,
  invoiceFixture,
  measurementReportFixture,
  paymentFixture,
  performanceEvidenceFixture,
  purchaseOrderFixture,
  reviewerFixture,
  sessionFixture,
  supplierCreativeFixture,
  workspaceFixture,
  type DeliveryFixtureState,
} from './campaign-delivery-data';

const tenantRoot = `/api/v1/tenants/${deliveryIds.tenant}`;

export async function installCampaignDeliveryApi(page: Page) {
  const state = createDeliveryState();
  await page.route('**/api/v1/**', async (route) => handleApi(route, state));
  return state;
}

async function handleApi(route: Route, state: DeliveryFixtureState) {
  const request = route.request();
  const path = new URL(request.url()).pathname;
  if (request.method() === 'GET') return handleRead(route, path, state);
  assertMutation(route);
  if (path.startsWith(`${tenantRoot}/purchase-orders`) ||
      path === `${tenantRoot}/invoices:issue` ||
      path.startsWith(`${tenantRoot}/payment-intents`)) {
    return handleFundingMutation(route, path, state);
  }
  if (path.includes('/creative-assets/')) {
    return handleSupplierCreativeMutation(route, path, state);
  }
  return handleCampaignMutation(route, path, state);
}

async function handleRead(route: Route, path: string, state: DeliveryFixtureState) {
  if (path === '/api/v1/session') return json(route, sessionFixture());
  if (path === '/api/v1/workspaces') return json(route, [workspaceFixture()]);
  if (path === `${tenantRoot}/funding`) return json(route, fundingWorkspaceFixture(state));
  if (path === `${tenantRoot}/bookings`) return json(route, [bookingFixture()]);
  if (path === `${tenantRoot}/bookings/bookable-lines`) return json(route, []);
  if (path === `${tenantRoot}/proposal-recipients`) return json(route, [reviewerFixture()]);
  if (path === `${tenantRoot}/campaigns`) return json(route, campaignList(state));
  return handleResourceRead(route, path, state);
}

async function handleResourceRead(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}`) {
    return json(route, campaignFixture(state), 200, versionHeader(state.campaignVersion));
  }
  if (path === `${tenantRoot}/delivery-proof-requests`) {
    return json(route, state.campaignStatus === 'COMPLETED'
      ? [deliveryProofRequestFixture(state)] : []);
  }
  if (path === `${tenantRoot}/creative-assets/${deliveryIds.asset}`) {
    return handleCreativeRead(route, state);
  }
  return handleEvidenceRead(route, path, state);
}

async function handleCreativeRead(route: Route, state: DeliveryFixtureState) {
  if (!state.creativeAssetCreated) return json(route, safeProblem('NOT_FOUND'), 404);
  const asset = supplierCreativeFixture(state);
  return json(route, asset, 200, versionHeader(asset.version));
}

async function handleEvidenceRead(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/delivery-proofs/${deliveryIds.proof}`) {
    return state.proofSubmitted
      ? json(route, deliveryProofFixture(state), 200,
          versionHeader(deliveryProofFixture(state).version))
      : json(route, safeProblem('NOT_FOUND'), 404);
  }
  if (path === `${tenantRoot}/performance-evidence/${deliveryIds.evidence}`) {
    return state.evidenceSubmitted
      ? json(route, performanceEvidenceFixture(state), 200,
          versionHeader(performanceEvidenceFixture(state).version))
      : json(route, safeProblem('NOT_FOUND'), 404);
  }
  if (path === `${tenantRoot}/measurement-reports/${deliveryIds.report}`) {
    return state.reportGenerated
      ? json(route, measurementReportFixture(state), 200,
          versionHeader(measurementReportFixture(state).version))
      : json(route, safeProblem('NOT_FOUND'), 404);
  }
  return json(route, safeProblem('NOT_FOUND'), 404);
}

async function handleFundingMutation(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/purchase-orders`) {
    state.purchaseOrderStatus = 'SUBMITTED';
    return json(route, purchaseOrderFixture(state), 201);
  }
  if (path === `${tenantRoot}/purchase-orders/${deliveryIds.purchaseOrder}:approve`) {
    state.purchaseOrderStatus = 'APPROVED';
    return json(route, purchaseOrderFixture(state));
  }
  if (path === `${tenantRoot}/invoices:issue`) {
    state.invoiceCreated = true;
    return json(route, invoiceFixture(), 201);
  }
  if (path === `${tenantRoot}/payment-intents`) {
    state.paymentStatus = 'PENDING';
    return json(route, paymentFixture(state), 201);
  }
  if (path === `${tenantRoot}/payment-intents/${deliveryIds.payment}:reconcile`) {
    state.paymentStatus = 'CONFIRMED';
    return json(route, paymentFixture(state));
  }
  return json(route, safeProblem('NOT_FOUND'), 404);
}

async function handleSupplierCreativeMutation(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/creative-assets/${deliveryIds.asset}:supplier-review`) {
    state.supplierApproved = true;
    return json(route, supplierCreativeFixture(state), 200,
      versionHeader(supplierCreativeFixture(state).version));
  }
  return json(route, safeProblem('NOT_FOUND'), 404);
}

async function handleCampaignMutation(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}:confirm-bookings`) {
    advanceCampaign(state, 'BOOKED');
    return json(route, campaignFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}:request-creative`) {
    state.creativeRequested = true;
    advanceCampaign(state, 'CREATIVE_PENDING');
    return json(route, campaignFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/creative`) {
    state.creativeAssetCreated = true;
    return json(route, creativeAssetFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/creative/${deliveryIds.asset}:brand-review`) {
    state.brandApproved = true;
    return json(route, creativeAssetFixture(state));
  }
  return handleDeliveryMutation(route, path, state);
}

async function handleDeliveryMutation(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}:approve-creative`) {
    advanceCampaign(state, 'READY');
    return json(route, campaignFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}:start`) {
    advanceCampaign(state, 'LIVE');
    return json(route, campaignFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}:complete`) {
    advanceCampaign(state, 'COMPLETED');
    return json(route, campaignFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/delivery-proofs`) {
    state.proofSubmitted = true;
    return json(route, deliveryProofFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/delivery-proofs/${deliveryIds.proof}:review`) {
    state.proofApproved = true;
    return json(route, deliveryProofFixture(state));
  }
  return handleMeasurementMutation(route, path, state);
}

async function handleMeasurementMutation(
  route: Route,
  path: string,
  state: DeliveryFixtureState,
) {
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/performance-evidence`) {
    state.evidenceSubmitted = true;
    return json(route, performanceEvidenceFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/performance-evidence/${deliveryIds.evidence}:review`) {
    state.evidenceApproved = true;
    return json(route, performanceEvidenceFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/measurement-reports:generate`) {
    state.reportGenerated = true;
    return json(route, measurementReportFixture(state));
  }
  if (path === `${tenantRoot}/campaigns/${deliveryIds.campaign}/measurement-reports/${deliveryIds.report}:review`) {
    state.reportApproved = true;
    return json(route, measurementReportFixture(state));
  }
  return json(route, safeProblem('NOT_FOUND'), 404);
}

function campaignList(state: DeliveryFixtureState) {
  return state.paymentStatus === 'CONFIRMED' ? [campaignFixture(state)] : [];
}

function advanceCampaign(state: DeliveryFixtureState, status: string) {
  state.campaignStatus = status;
  state.campaignVersion += 1;
}

function assertMutation(route: Route) {
  const headers = route.request().headers();
  expect(headers['x-csrf-token']).toBe('csrf-delivery');
  expect(headers['idempotency-key']).toBeTruthy();
}

function versionHeader(version: number) {
  return { ETag: `"${version}"` };
}

function safeProblem(code: string) {
  return {
    type: null,
    title: 'Request failed',
    status: 404,
    detail: null,
    instance: null,
    code,
    correlationId: 'b2000000-0000-0000-0000-000000000001',
    fieldErrors: null,
  };
}

async function json(
  route: Route,
  body: unknown,
  status = 200,
  headers?: Record<string, string>,
) {
  await route.fulfill({
    status,
    headers,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}
