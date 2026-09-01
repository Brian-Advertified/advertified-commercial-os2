import type { ZodType } from 'zod'
import { request } from './client'
import {
  campaignSchema,
  campaignsSchema,
  creativeAssetSchema,
  deliveryProofRequestsSchema,
  deliveryProofSchema,
  measurementReportSchema,
  performanceEvidenceSchema,
  supplierCreativeAssetSchema,
  type Campaign,
  type CreativeAsset,
  type CreativeRequestInput,
  type DeliveryProofRequest,
  type DeliveryProof,
  type DeliveryProofInput,
  type MeasurementReport,
  type PerformanceEvidence,
  type PerformanceEvidenceInput,
  type SupplierCreativeAsset,
} from './campaign-schemas'
import { filePayload, fileToBase64 } from './file-content'

const campaignRoot = (tenantId: string) =>
  `/api/v1/tenants/${tenantId}/campaigns`

async function command<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  expectedVersion?: number,
): Promise<T> {
  return (await request(
    path,
    schema,
    { method: 'POST', body: JSON.stringify(body) },
    {
      antiforgeryToken: token,
      expectedVersion,
      idempotencyKey: crypto.randomUUID(),
    },
  )).data
}

export const campaignApi = {
  async list(tenantId: string): Promise<Campaign[]> {
    return (await request(campaignRoot(tenantId), campaignsSchema)).data
  },

  async get(tenantId: string, campaignId: string): Promise<Campaign> {
    return (await request(
      `${campaignRoot(tenantId)}/${campaignId}`,
      campaignSchema,
    )).data
  },

  confirmBookings(tenantId: string, campaign: Campaign, reason: string, token: string) {
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}:confirm-bookings`,
      campaignSchema, { reason }, token, campaign.version)
  },

  start(tenantId: string, campaign: Campaign, reason: string, token: string) {
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}:start`,
      campaignSchema, { reason }, token, campaign.version)
  },

  complete(
    tenantId: string,
    campaign: Campaign,
    completionReason: string,
    proofRequestReason: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}:complete`,
      campaignSchema, { completionReason, proofRequestReason }, token, campaign.version)
  },

  requestCreative(
    tenantId: string,
    campaign: Campaign,
    input: CreativeRequestInput,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}:request-creative`,
      campaignSchema, input, token, campaign.version)
  },

  async createCreativeAsset(
    tenantId: string,
    campaign: Campaign,
    requirementId: string,
    approvedCopy: string,
    file: File,
    token: string,
  ): Promise<CreativeAsset> {
    const content = await fileToBase64(file)
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}/creative`,
      creativeAssetSchema,
      {
        campaignVersion: campaign.version,
        requirementId,
        approvedCopy,
        file: filePayload(file, content),
      },
      token,
    )
  },

  async uploadCreativeVersion(
    tenantId: string,
    campaignId: string,
    asset: CreativeAsset,
    approvedCopy: string,
    file: File,
    token: string,
  ): Promise<CreativeAsset> {
    const content = await fileToBase64(file)
    return command(
      `${campaignRoot(tenantId)}/${campaignId}/creative/${asset.id}:upload-version`,
      creativeAssetSchema,
      { approvedCopy, file: filePayload(file, content) },
      token,
      asset.version,
    )
  },

  reviewCreativeBrand(
    tenantId: string,
    campaignId: string,
    asset: CreativeAsset,
    approved: boolean,
    rightsStatus: string,
    evidenceReference: string,
    reason: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${campaignId}/creative/${asset.id}:brand-review`,
      creativeAssetSchema,
      { approved, rightsStatus, evidenceReference, reason },
      token,
      asset.version,
    )
  },

  approveCreative(tenantId: string, campaign: Campaign, reason: string, token: string) {
    return command(
      `${campaignRoot(tenantId)}/${campaign.id}:approve-creative`,
      campaignSchema, { reason }, token, campaign.version)
  },

  async getSupplierCreativeAsset(
    tenantId: string,
    assetId: string,
  ): Promise<SupplierCreativeAsset> {
    return (await request(
      `/api/v1/tenants/${tenantId}/creative-assets/${assetId}`,
      supplierCreativeAssetSchema,
    )).data
  },

  reviewCreativeSupplier(
    tenantId: string,
    asset: SupplierCreativeAsset,
    approved: boolean,
    evidenceReference: string,
    reason: string,
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/creative-assets/${asset.assetId}:supplier-review`,
      supplierCreativeAssetSchema,
      { approved, evidenceReference, reason },
      token,
      asset.version,
    )
  },

  async listDeliveryProofRequests(
    tenantId: string,
  ): Promise<DeliveryProofRequest[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/delivery-proof-requests`,
      deliveryProofRequestsSchema,
    )).data
  },

  async getDeliveryProof(tenantId: string, proofId: string): Promise<DeliveryProof> {
    return (await request(
      `/api/v1/tenants/${tenantId}/delivery-proofs/${proofId}`,
      deliveryProofSchema,
    )).data
  },

  async submitDeliveryProof(
    tenantId: string,
    campaignId: string,
    input: DeliveryProofInput,
    file: File,
    token: string,
  ): Promise<DeliveryProof> {
    const content = await fileToBase64(file)
    return command(
      `${campaignRoot(tenantId)}/${campaignId}/delivery-proofs`,
      deliveryProofSchema,
      { ...input, file: filePayload(file, content) },
      token,
    )
  },

  reviewDeliveryProof(
    tenantId: string,
    proof: DeliveryProof,
    approved: boolean,
    reason: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${proof.campaignId}/delivery-proofs/${proof.id}:review`,
      deliveryProofSchema,
      { approved, reason },
      token,
      proof.version,
    )
  },

  async getPerformanceEvidence(
    tenantId: string,
    evidenceId: string,
  ): Promise<PerformanceEvidence> {
    return (await request(
      `/api/v1/tenants/${tenantId}/performance-evidence/${evidenceId}`,
      performanceEvidenceSchema,
    )).data
  },

  async submitPerformanceEvidence(
    tenantId: string,
    campaignId: string,
    input: PerformanceEvidenceInput,
    file: File,
    token: string,
  ): Promise<PerformanceEvidence> {
    const content = await fileToBase64(file)
    return command(
      `${campaignRoot(tenantId)}/${campaignId}/performance-evidence`,
      performanceEvidenceSchema,
      { ...input, file: filePayload(file, content) },
      token,
    )
  },

  reviewPerformanceEvidence(
    tenantId: string,
    evidence: PerformanceEvidence,
    approved: boolean,
    reason: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${evidence.campaignId}/performance-evidence/${evidence.id}:review`,
      performanceEvidenceSchema,
      { approved, reason },
      token,
      evidence.version,
    )
  },

  async getMeasurementReport(
    tenantId: string,
    reportId: string,
  ): Promise<MeasurementReport> {
    return (await request(
      `/api/v1/tenants/${tenantId}/measurement-reports/${reportId}`,
      measurementReportSchema,
    )).data
  },

  generateMeasurementReport(
    tenantId: string,
    campaignId: string,
    approverUserId: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${campaignId}/measurement-reports:generate`,
      measurementReportSchema,
      { approverUserId },
      token,
    )
  },

  reviewMeasurementReport(
    tenantId: string,
    report: MeasurementReport,
    approved: boolean,
    reason: string,
    token: string,
  ) {
    return command(
      `${campaignRoot(tenantId)}/${report.campaignId}/measurement-reports/${report.id}:review`,
      measurementReportSchema,
      { approved, reason },
      token,
      report.version,
    )
  },
}
