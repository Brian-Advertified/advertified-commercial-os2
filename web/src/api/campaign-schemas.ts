import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const date = z.iso.date()
const dateTime = z.iso.datetime({ offset: true })
const nullableGuid = z.guid().nullable()
const nullableDateTime = dateTime.nullable()
const nullableText = z.string().nullable()

export const creativeReviewSchema = z.object({
  reviewType: requiredText,
  decision: requiredText,
  rightsStatus: nullableText,
  evidenceReference: requiredText,
  reason: requiredText,
  reviewedBy: z.guid(),
  reviewerTenantId: z.guid(),
  reviewedAtUtc: dateTime,
}).strict()

export const creativeAssetVersionSchema = z.object({
  id: z.guid(),
  versionNumber: z.number().int().positive(),
  fileName: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().positive(),
  contentSha256: requiredText,
  approvedCopy: requiredText,
  commercialSnapshotJson: requiredText,
  createdBy: z.guid(),
  createdAtUtc: dateTime,
  brandReview: creativeReviewSchema.nullable(),
  supplierReview: creativeReviewSchema.nullable(),
}).strict()

export const creativeAssetSchema = z.object({
  id: z.guid(),
  requirementId: z.guid(),
  version: z.number().int().positive(),
  currentVersion: creativeAssetVersionSchema,
}).strict()

export const creativeRequirementSchema = z.object({
  id: z.guid(),
  campaignId: z.guid(),
  bookingId: z.guid(),
  mediaPlanLineId: z.guid(),
  supplierTenantId: z.guid(),
  channel: requiredText,
  flightStart: date,
  flightEnd: date,
  formatCode: requiredText,
  width: z.number().int().positive(),
  height: z.number().int().positive(),
  requiredMediaType: requiredText,
  maximumBytes: z.number().int().positive(),
  instructions: requiredText,
  asset: creativeAssetSchema.nullable(),
}).strict()

export const creativeWorkspaceSchema = z.object({
  readyForApproval: z.boolean(),
  requirements: z.array(creativeRequirementSchema),
}).strict()

export const supplierCreativeAssetSchema = z.object({
  assetId: z.guid(),
  campaignId: z.guid(),
  requirementId: z.guid(),
  channel: requiredText,
  formatCode: requiredText,
  width: z.number().int().positive(),
  height: z.number().int().positive(),
  requiredMediaType: requiredText,
  maximumBytes: z.number().int().positive(),
  instructions: requiredText,
  versionId: z.guid(),
  versionNumber: z.number().int().positive(),
  fileName: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().positive(),
  contentSha256: requiredText,
  supplierDecision: nullableText,
  version: z.number().int().positive(),
}).strict()

export const deliveryProofRequestSchema = z.object({
  campaignId: z.guid(),
  bookingId: z.guid(),
  supplierName: requiredText,
  productName: requiredText,
  channel: requiredText,
  geography: requiredText,
  flightStart: date,
  flightEnd: date,
  proofRequestedAtUtc: dateTime,
  proofRequestReason: requiredText,
  latestProofId: nullableGuid,
  latestProofStatus: nullableText,
}).strict()

export const deliveryProofRequestsSchema = z.array(deliveryProofRequestSchema)

export const deliveryProofSchema = z.object({
  id: z.guid(),
  campaignId: z.guid(),
  bookingId: z.guid(),
  supplierTenantId: z.guid(),
  proofType: requiredText,
  fileName: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().positive(),
  contentSha256: requiredText,
  signatureValidated: z.boolean(),
  malwareScanStatus: requiredText,
  capturedAtUtc: dateTime,
  locationDescription: requiredText,
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  sourceReference: requiredText,
  submissionReason: requiredText,
  status: requiredText,
  submittedBy: z.guid(),
  submitterTenantId: z.guid(),
  submittedAtUtc: dateTime,
  reviewedBy: nullableGuid,
  reviewedAtUtc: nullableDateTime,
  reviewReason: nullableText,
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
}).strict()

export const performanceMetricSchema = z.object({
  id: z.guid(),
  metricType: requiredText,
  value: z.number(),
  unit: requiredText,
  periodStart: date,
  periodEnd: date,
  sourceLocator: requiredText,
}).strict()

export const performanceEvidenceSchema = z.object({
  id: z.guid(),
  campaignId: z.guid(),
  sourceReference: requiredText,
  fileName: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().positive(),
  contentSha256: requiredText,
  signatureValidated: z.boolean(),
  malwareScanStatus: requiredText,
  capturedAtUtc: dateTime,
  methodology: requiredText,
  limitations: z.array(requiredText),
  qualityStatus: requiredText,
  metrics: z.array(performanceMetricSchema),
  status: requiredText,
  reviewerUserId: z.guid(),
  submittedBy: z.guid(),
  submittedAtUtc: dateTime,
  reviewedBy: nullableGuid,
  reviewedAtUtc: nullableDateTime,
  reviewReason: nullableText,
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
}).strict()

export const measurementFindingSchema = z.object({
  title: requiredText,
  summary: requiredText,
  metricIds: z.array(z.guid()).min(1),
  causalityStatus: requiredText,
}).strict()

export const measurementInterpretationSchema = z.object({
  executiveSummary: requiredText,
  findings: z.array(measurementFindingSchema),
  limitations: z.array(requiredText),
  learningProposals: z.array(z.object({
    text: requiredText,
    requiresNewApproval: z.boolean(),
  }).strict()),
  causalityStatus: requiredText,
}).strict()

export const measurementReportSchema = z.object({
  id: z.guid(),
  campaignId: z.guid(),
  versionNumber: z.number().int().positive(),
  campaignVersion: z.number().int().positive(),
  measurementPlan: z.array(requiredText),
  evidence: z.array(performanceEvidenceSchema),
  interpretation: measurementInterpretationSchema,
  status: requiredText,
  approverUserId: z.guid(),
  generatedBy: z.guid(),
  generatedAtUtc: dateTime,
  reviewedBy: nullableGuid,
  reviewedAtUtc: nullableDateTime,
  reviewReason: nullableText,
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
}).strict()

export const campaignSchema = z.object({
  id: z.guid(),
  briefId: z.guid(),
  briefVersionId: z.guid(),
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  proposalDecisionId: z.guid(),
  planVersionId: z.guid(),
  paymentIntentId: z.guid(),
  fundingStatus: requiredText,
  title: requiredText,
  startDate: date,
  endDate: date,
  ownerUserId: z.guid(),
  measurementPlanJson: z.string(),
  status: requiredText,
  requiredBookingCount: z.number().int().nonnegative(),
  confirmedBookingCount: z.number().int().nonnegative(),
  nextActionPermission: nullableText,
  createdBy: z.guid(),
  createdAtUtc: dateTime,
  bookingsConfirmedBy: nullableGuid,
  bookingsConfirmedAtUtc: nullableDateTime,
  bookingConfirmationReason: nullableText,
  creativeRequestedBy: nullableGuid,
  creativeRequestedAtUtc: nullableDateTime,
  creativeRequestReason: nullableText,
  creativeApprovedBy: nullableGuid,
  creativeApprovedAtUtc: nullableDateTime,
  creativeApprovalReason: nullableText,
  startedBy: nullableGuid,
  startedAtUtc: nullableDateTime,
  startReason: nullableText,
  completedBy: nullableGuid,
  completedAtUtc: nullableDateTime,
  completionReason: nullableText,
  proofRequestedBy: nullableGuid,
  proofRequestedAtUtc: nullableDateTime,
  proofRequestReason: nullableText,
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
  creative: creativeWorkspaceSchema.nullable(),
  deliveryProofs: z.array(deliveryProofSchema),
  performanceEvidence: z.array(performanceEvidenceSchema),
  measurementReports: z.array(measurementReportSchema),
}).strict()

export const campaignsSchema = z.array(campaignSchema)

export const campaignReasonSchema = z.object({
  reason: requiredText.max(1000),
}).strict()

export const completionReasonSchema = z.object({
  completionReason: requiredText.max(1000),
  proofRequestReason: requiredText.max(1000),
}).strict()

export const creativeRequirementInputSchema = z.object({
  bookingId: z.guid(),
  formatCode: requiredText.max(100),
  width: z.number().int().positive().max(100000),
  height: z.number().int().positive().max(100000),
  requiredMediaType: requiredText.max(200),
  maximumBytes: z.number().int().positive().max(104857600),
  instructions: requiredText.max(5000),
}).strict()

export const creativeRequestInputSchema = z.object({
  reason: requiredText.max(1000),
  requirements: z.array(creativeRequirementInputSchema).min(1),
}).strict()

export const deliveryProofInputSchema = z.object({
  bookingId: z.guid(),
  proofType: requiredText,
  capturedAtUtc: dateTime,
  locationDescription: requiredText.max(1000),
  latitude: z.number().min(-90).max(90).nullable(),
  longitude: z.number().min(-180).max(180).nullable(),
  sourceReference: requiredText.max(1000),
  reason: requiredText.max(1000),
}).strict()

export const performanceMetricInputSchema = z.object({
  metricType: requiredText,
  value: z.number(),
  unit: requiredText,
  periodStart: date,
  periodEnd: date,
  sourceLocator: requiredText.max(1000),
}).strict().refine(value => value.periodEnd >= value.periodStart, {
  message: 'The metric period end must be on or after its start.',
})

export const performanceEvidenceInputSchema = z.object({
  sourceReference: requiredText.max(1000),
  capturedAtUtc: dateTime,
  methodology: requiredText.max(5000),
  limitations: z.array(requiredText.max(2000)).min(1),
  qualityStatus: requiredText,
  reviewerUserId: z.guid(),
  metrics: z.array(performanceMetricInputSchema).min(1),
}).strict()

export type Campaign = z.infer<typeof campaignSchema>
export type CreativeRequirement = z.infer<typeof creativeRequirementSchema>
export type CreativeAsset = z.infer<typeof creativeAssetSchema>
export type SupplierCreativeAsset = z.infer<typeof supplierCreativeAssetSchema>
export type DeliveryProofRequest = z.infer<typeof deliveryProofRequestSchema>
export type DeliveryProof = z.infer<typeof deliveryProofSchema>
export type PerformanceEvidence = z.infer<typeof performanceEvidenceSchema>
export type MeasurementReport = z.infer<typeof measurementReportSchema>
export type CreativeRequestInput = z.infer<typeof creativeRequestInputSchema>
export type DeliveryProofInput = z.infer<typeof deliveryProofInputSchema>
export type PerformanceEvidenceInput = z.infer<typeof performanceEvidenceInputSchema>
