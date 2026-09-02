import { z } from 'zod'
import { masterDataCodes } from '../generated/master-data-codes'
import {
  inventoryCommercialTermsSchema,
  inventoryDeliverableSchema,
  inventorySpatialSchema,
  inventorySupplierCommercialSchema,
} from './inventory-schemas'

export const runningPeriodSchema = z.object({
  start: z.iso.date(),
  end: z.iso.date(),
})

export const mediaAllocationSchema = z.object({
  channel: z.string().min(1),
  budgetMinor: z.number().int().nonnegative(),
  role: z.string().min(1),
  runningPeriods: z.array(runningPeriodSchema),
})

export const audienceDefinitionSchema = z.object({
  id: z.guid(),
  name: z.string(),
  description: z.string(),
  needState: z.string(),
  buyingContext: z.string(),
  geographies: z.array(z.string()),
  language: z.string().nullable(),
  lifeStage: z.string().nullable(),
  lsmSem: z.string().nullable(),
  lsmSemTaxonomy: z.string().nullable(),
  lsmSemTaxonomyVersion: z.string().nullable(),
  classification: z.string(),
  exclusions: z.array(z.string()),
  evidenceItemIds: z.array(z.guid()),
  confidence: z.number(),
  status: z.string(),
  lsmSemMandatory: z.boolean(),
})

export const audienceSetSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  targetAudienceIds: z.array(z.guid()).min(1),
  targetingRationale: z.string(),
  positioningStatement: z.string(),
  inputHash: z.string(),
  status: z.string(),
  definitions: z.array(audienceDefinitionSchema),
  createdAtUtc: z.iso.datetime({ offset: true }),
})

export const mediaMixSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  audienceSetId: z.guid(),
  versionNumber: z.number().int().positive(),
  totalBudgetMinor: z.number().int().nonnegative(),
  currency: z.string(),
  allocations: z.array(mediaAllocationSchema),
  assumptions: z.array(z.string()),
  inputHash: z.string(),
  status: z.string(),
  createdBy: z.guid(),
  approvedBy: z.guid().nullable(),
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
})

export const benchmarkSchema = z.object({
  id: z.guid(),
  policyVersion: z.string(),
  geographyBasis: z.string(),
  cohortSize: z.number().int().nonnegative(),
  medianMinor: z.number().int().nullable(),
  lowerQuartileMinor: z.number().int().nullable(),
  upperQuartileMinor: z.number().int().nullable(),
  percentile: z.number().nullable(),
  position: z.string(),
  confidence: z.number(),
  exclusions: z.array(z.string()),
})

export const deliveryMeasurementSchema = z.object({
  metricType: z.string().min(1),
  value: z.number().nonnegative().nullable(),
  unit: z.string().nullable(),
  universe: z.string().nullable(),
  measurementSource: z.string().nullable(),
  measurementPeriod: z.string().nullable(),
  methodology: z.string().nullable(),
  limitations: z.string().nullable(),
})

export const audienceFitSchema = z.object({
  languageScore: z.number().min(0).max(1).nullable(),
  lifeStageScore: z.number().min(0).max(1).nullable(),
  lsmSemScore: z.number().min(0).max(1).nullable(),
  evidenceGaps: z.array(z.string()),
  measurementSource: z.string().nullable(),
  measurementPeriod: z.string().nullable(),
  methodology: z.string().nullable(),
  taxonomyName: z.string().nullable(),
  taxonomyVersion: z.string().nullable(),
  deliveryMeasurements: z.array(deliveryMeasurementSchema).nullish()
    .transform(value => value ?? []),
  deliveryEvidenceGaps: z.array(z.string()).nullish()
    .transform(value => value ?? []),
  lsmSemMandatory: z.boolean().default(false),
})

const spatialMatchSchema = z.object({
  hasRequirements: z.boolean(), requiredRequirementIds: z.array(z.guid()),
  matchedRequiredRequirementIds: z.array(z.guid()),
  preferredRequirementIds: z.array(z.guid()),
  matchedPreferredRequirementIds: z.array(z.guid()),
  excludedRequirementIds: z.array(z.guid()),
  matchedExcludedRequirementIds: z.array(z.guid()),
  geographyScore: z.number().min(0).max(1), evidenceGaps: z.array(z.string()),
})

const suitabilitySchema = z.object({
  policyVersion: z.string().min(1), geography: z.number().min(0).max(1),
  audienceContext: z.number().min(0).max(1),
  objectiveFormat: z.number().min(0).max(1),
  budgetEfficiency: z.number().min(0).max(1),
  evidenceQualityFreshness: z.number().min(0).max(1),
  portfolioCoverageDiversity: z.number().min(0).max(1),
  total: z.number().min(0).max(1), evidenceGaps: z.array(z.string()),
})

export const shortlistCandidateSchema = z.object({
  id: z.guid(),
  inventoryTenantId: z.guid(),
  marketplaceListingVersionId: z.guid().nullable(),
  inventoryProductId: z.guid(),
  productVersionId: z.guid(),
  rateId: z.guid().nullable(),
  availabilityId: z.guid().nullable(),
  name: z.string(),
  channel: z.string(),
  geography: z.string(),
  rateAmountMinor: z.number().int().nullable(),
  currency: z.string().nullable(),
  isEligible: z.boolean(),
  rejectionReason: z.string().nullable(),
  rejectionDetail: z.string().nullable(),
  score: z.number().nullable(),
  audienceFit: audienceFitSchema,
  rationale: z.string().nullable(),
  isSelected: z.boolean().nullable(),
  benchmark: benchmarkSchema.nullable(),
  logoAssetId: z.guid().nullish().transform(value => value ?? null),
  commercialReadiness: z.object({
    supplierVatStatus: z.string().nullable(),
    vatTreatment: z.string().nullable(),
    evidenceGaps: z.array(z.string()),
    supplierVatNumber: z.string().nullish().transform(value => value ?? null),
  }).nullish().transform(value => value ?? {
    supplierVatStatus: null,
    vatTreatment: null,
    evidenceGaps: ['inventory.supplierCommercial.vatStatus', 'inventory.rate.vatTreatment'],
    supplierVatNumber: null,
  }),
  supplierCommercial: inventorySupplierCommercialSchema.nullish().transform(value => value ?? null),
  commercialTerms: inventoryCommercialTermsSchema.nullish().transform(value => value ?? null),
  deliverable: inventoryDeliverableSchema.nullish().transform(value => value ?? null),
  spatial: inventorySpatialSchema.nullish().transform(value => value ?? null),
  spatialMatch: spatialMatchSchema.nullish().transform(value => value ?? null),
  suitability: suitabilitySchema.nullish().transform(value => value ?? null),
})

export const shortlistSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  mixVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  inputHash: z.string(),
  status: z.string(),
  assumptions: z.array(z.string()),
  candidates: z.array(shortlistCandidateSchema),
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
})

export const planLineSchema = z.object({
  id: z.guid(),
  inventoryTenantId: z.guid(),
  marketplaceListingVersionId: z.guid().nullable(),
  inventoryProductId: z.guid(),
  productVersionId: z.guid(),
  rateId: z.guid(),
  availabilityId: z.guid().nullable(),
  name: z.string(),
  channel: z.string(),
  geography: z.string(),
  runningPeriods: z.array(runningPeriodSchema).min(1),
  quantity: z.number().int().positive(),
  clientPriceMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  availability: z.string(),
  rateFreshness: z.string(),
  supplySource: z.string(),
  lastConfirmedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  supplyConfidence: z.string(),
  supplierCommercial: inventorySupplierCommercialSchema.nullish().transform(value => value ?? null),
  commercialTerms: inventoryCommercialTermsSchema.nullish().transform(value => value ?? null),
  deliverable: inventoryDeliverableSchema.nullish().transform(value => value ?? null),
  spatial: inventorySpatialSchema.nullish().transform(value => value ?? null),
  logoAssetId: z.guid().nullish().transform(value => value ?? null),
})

export const planObjectionSchema = z.object({
  code: z.string(),
  severity: z.string(),
  affectedField: z.string(),
  evidenceGap: z.string(),
  recommendedResolution: z.string(),
  resolution: z.string().nullable(),
  resolutionReason: z.string().nullable(),
  resolvedBy: z.guid().nullable(),
})

export const mediaPlanSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  mixVersionId: z.guid(),
  shortlistVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  totalMinor: z.number().int().nonnegative(),
  currency: z.string(),
  supplyConfidence: z.string(),
  inputHash: z.string(),
  status: z.string(),
  assumptions: z.array(z.string()),
  lines: z.array(planLineSchema),
  objections: z.array(planObjectionSchema),
  createdBy: z.guid(),
  approvedBy: z.guid().nullable(),
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  commercialPolicyVersionId: z.guid().nullish().transform(value => value ?? null),
})

export const campaignModeSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  mode: z.enum([
    masterDataCodes.campaignModes.fullCampaign,
    masterDataCodes.campaignModes.oohOnly,
  ]),
  allowedChannels: z.array(z.string().min(1)),
  isLocked: z.boolean(),
  decisionSource: z.string().min(1),
  confidence: z.number().min(0).max(1),
  reason: z.string().nullable(),
  selectedBy: z.guid(),
  selectedAtUtc: z.iso.datetime({ offset: true }),
})

export const planningSummarySchema = z.object({
  briefId: z.guid(),
  briefVersionId: z.guid(),
  clientName: z.string().trim().min(1),
  briefTitle: z.string().trim().min(1),
  audienceStatus: z.string().trim().min(1),
  mediaMixStatus: z.string().nullable(),
  mediaPlanStatus: z.string().nullable(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()
export const planningSummariesSchema = z.array(planningSummarySchema)

export const planningWorkspaceSchema = z.object({
  briefId: z.guid(),
  briefVersionId: z.guid(),
  clientName: z.string().trim().min(1),
  campaignMode: campaignModeSchema.nullable(),
  audience: audienceSetSchema.nullable(),
  mediaMix: mediaMixSchema.nullable(),
  shortlist: shortlistSchema.nullable(),
  mediaPlan: mediaPlanSchema.nullable(),
})

export type AudienceSet = z.infer<typeof audienceSetSchema>
export type RunningPeriod = z.infer<typeof runningPeriodSchema>
export type MediaAllocation = z.infer<typeof mediaAllocationSchema>
export type MediaMix = z.infer<typeof mediaMixSchema>
export type ShortlistCandidate = z.infer<typeof shortlistCandidateSchema>
export type Shortlist = z.infer<typeof shortlistSchema>
export type MediaPlan = z.infer<typeof mediaPlanSchema>
export type CampaignMode = z.infer<typeof campaignModeSchema>
export type PlanningSummary = z.infer<typeof planningSummarySchema>
export type PlanningWorkspace = z.infer<typeof planningWorkspaceSchema>
