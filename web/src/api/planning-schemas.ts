import { z } from 'zod'
import { buyAssessmentSchema } from './buy-assessment-schema'
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

export const inventoryPurchaseQuantitySchema = z.object({
  inventoryTenantId: z.guid(), inventoryProductId: z.guid(), productVersionId: z.guid(),
  rateId: z.guid(), rateType: z.string().min(1), quantity: z.number().int().positive(),
  denominator: z.number().int().positive().nullish(),
})

export const mediaGeographyAllocationSchema = z.object({
  geography: z.string().min(1),
  budgetMinor: z.number().int().nonnegative(),
})

export const mediaScheduleSchema = z.object({
  weekdays: z.array(z.string()),
  dayparts: z.array(z.string()),
})

export const mediaAllocationSchema = z.object({
  channel: z.string().min(1),
  budgetMinor: z.number().int().nonnegative(),
  role: z.string().min(1),
  runningPeriods: z.array(runningPeriodSchema),
  purchases: z.array(inventoryPurchaseQuantitySchema).nullish(),
  geographyAllocations: z.array(mediaGeographyAllocationSchema).nullish().transform(value => value ?? []),
  schedule: mediaScheduleSchema.nullish().transform(value => value ?? null),
})

export const audienceSegmentSchema = z.object({
  id: z.guid(),
  name: z.string(),
  description: z.string(),
  needState: z.string().nullable(),
  buyingContext: z.string().nullable(),
  geographies: z.array(z.string()),
  language: z.string().nullable(),
  lifeStage: z.string().nullable(),
  lsmSem: z.string().nullable(),
  lsmSemTaxonomy: z.string().nullable(),
  lsmSemTaxonomyVersion: z.string().nullable(),
  classification: z.string(),
  exclusions: z.array(z.string()),
  evidenceItemIds: z.array(z.guid()),
  referenceObservationIds: z.array(z.guid()),
  confidence: z.number().min(0).max(1).nullable(),
  status: z.string(),
  lsmSemMandatory: z.boolean(),
})

export const audienceStrategySchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  targetAudienceIds: z.array(z.guid()).min(1),
  targetingRationale: z.string().nullable(),
  positioningStatement: z.string().nullable(),
  inputHash: z.string(),
  status: z.string(),
  definitions: z.array(audienceSegmentSchema),
  createdBy: z.guid(),
  approvedBy: z.guid().nullable(),
  version: z.number().int().positive(),
  approvedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  createdAtUtc: z.iso.datetime({ offset: true }),
})

export const audienceResearchObservationSchema = z.object({
  observationId: z.guid(),
  sourceTitle: z.string().min(1),
  measurementPeriod: z.string().min(1),
  geographyLevel: z.string().min(1),
  geographyCode: z.string().min(1),
  geographyName: z.string().min(1),
  dimensions: z.record(z.string(), z.string()),
  metricCode: z.string().min(1),
  metricValue: z.number(),
  metricUnit: z.string().min(1),
  stabilityCode: z.string().min(1),
  activationPolicy: z.string().min(1),
})

export const audienceResearchContextSchema = z.object({
  requestedGeographies: z.array(z.string()),
  resolvedGeographies: z.array(z.string()),
  observations: z.array(audienceResearchObservationSchema),
})

export const inventoryIntelligenceInterpretationSchema = z.object({
  candidateId: z.guid(),
  rationale: z.string().min(1).max(1000),
  classification: z.literal(masterDataCodes.evidenceClassifications.aiRecommendation),
})

export const inventoryIntelligenceArtifactSchema = z.object({
  id: z.guid(),
  subjectId: z.guid(),
  serviceCode: z.literal(masterDataCodes.agentTypes.inventoryIntelligence),
  artifactJson: z.string(),
  unknowns: z.array(z.string()),
  totalIncrementalCostMinor: z.number().int().nonnegative(),
  status: z.string(),
  version: z.number().int().positive(),
})

export const inventoryIntelligencePayloadSchema = z.object({
  interpretations: z.array(inventoryIntelligenceInterpretationSchema),
})

export const mediaImpactEstimateSchema = z.object({
  estimatedReach: z.number().positive().nullable(),
  averageFrequency: z.number().positive().nullable(),
  estimatedRoiPercent: z.number().min(-100).nullable(),
  source: z.string().min(1),
  measurementPeriod: z.string().min(1).nullable(),
  methodology: z.string().min(1),
})

export const mediaMixSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  audienceArtifactId: z.guid(),
  mediaStrategyArtifactId: z.guid().nullable(),
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
  impactEstimate: mediaImpactEstimateSchema.nullish().transform(value => value ?? null),
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
  buyAssessment: buyAssessmentSchema.nullish(),
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
  supplierId: z.guid().nullish().transform(value => value ?? null),
  supplierName: z.string().trim().min(1).nullish().transform(value => value ?? null),
  latitude: z.number().min(-90).max(90).nullish().transform(value => value ?? null),
  longitude: z.number().min(-180).max(180).nullish().transform(value => value ?? null),
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
    rateType: z.string().nullish(),
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
  campaignCombinations: z.object({
    alternatives: z.array(z.object({
      candidateIds: z.array(z.guid()), campaignSupplierCostMinor: z.number().nonnegative(),
      currency: z.string(), channelCosts: z.array(z.object({
        channel: z.string(), supplierCostMinor: z.number().nonnegative(), budgetMinor: z.number().nonnegative(),
      })), coveredRequirementIds: z.array(z.guid()), evidenceGaps: z.array(z.string()),
      comparison: z.object({
        supplierCostDeltaMinor: z.number().int(), addedCandidateIds: z.array(z.guid()), removedCandidateIds: z.array(z.guid()),
        measuredTargetCandidateCount: z.number().int().nonnegative(), missingDeliveryCandidateCount: z.number().int().nonnegative(),
        distinctInventoryWorkspaceCount: z.number().int().nonnegative(), plannedChannelRoles: z.array(z.string()),
      }).nullable().optional(),
      audienceForecast: z.object({
        grossReach: z.number().nonnegative().nullable(), deduplicatedReach: z.number().nonnegative().nullable(),
        duplicatedReach: z.number().nonnegative().nullable(), totalImpressions: z.number().nonnegative().nullable(),
        averageFrequency: z.number().nonnegative().nullable(), universe: z.string().nullable(),
        measurementPeriod: z.string().nullable(), measurementSource: z.string().nullable(), methodology: z.string().nullable(),
        incrementalReach: z.array(z.object({
          candidateId: z.guid(), incrementalReach: z.number().nonnegative().nullable(), evidenceGap: z.string().nullable(),
        })), evidenceGaps: z.array(z.string()),
      }).nullable().optional(),
      scenario: z.object({
        code: z.enum(['RECOMMENDED', 'MAX_MEASURED_REACH', 'HIGHER_FREQUENCY', 'LOWER_SUPPLIER_COST', 'ALTERNATIVE']),
        recommended: z.boolean(), supplierCostDeltaMinor: z.number().int(),
        deduplicatedReachDelta: z.number().nullable(), averageFrequencyDelta: z.number().nullable(),
      }).nullable().optional(),
    })),
    searchTruncated: z.boolean(), candidatesConsidered: z.number().int().nonnegative(),
    missingCostCandidateCount: z.number().int().nonnegative(),
  }).nullish(),
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
  supplierName: z.string().trim().min(1).nullish().transform(value => value ?? null),
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
  purchase: inventoryPurchaseQuantitySchema.nullish(),
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
  audience: audienceStrategySchema.nullable(),
  mediaMix: mediaMixSchema.nullable(),
  shortlist: shortlistSchema.nullable(),
  mediaPlan: mediaPlanSchema.nullable(),
  decisionContext: z.object({
    businessProblem: z.string(), objective: z.string(), successMeasures: z.array(z.string()),
    targetingRationale: z.string().nullable(), positioningStatement: z.string().nullable(),
    mediaJobs: z.array(z.object({
      channel: z.string(), role: z.string(), budgetMinor: z.number().int().nonnegative(), currency: z.string(),
    })), evidenceGaps: z.array(z.string()),
  }).nullable().optional(),
  audienceResearch: audienceResearchContextSchema.nullish().transform(value => value ?? null),
})

export type AudienceStrategy = z.infer<typeof audienceStrategySchema>
export type AudienceResearchContext = z.infer<typeof audienceResearchContextSchema>
export type AudienceResearchObservation = z.infer<typeof audienceResearchObservationSchema>
export type InventoryIntelligenceArtifact = z.infer<typeof inventoryIntelligenceArtifactSchema>
export type InventoryIntelligenceInterpretation = z.infer<typeof inventoryIntelligenceInterpretationSchema>
export type RunningPeriod = z.infer<typeof runningPeriodSchema>
export type MediaGeographyAllocation = z.infer<typeof mediaGeographyAllocationSchema>
export type MediaAllocation = z.infer<typeof mediaAllocationSchema>
export type MediaImpactEstimate = z.infer<typeof mediaImpactEstimateSchema>
export type MediaMix = z.infer<typeof mediaMixSchema>
export type ShortlistCandidate = z.infer<typeof shortlistCandidateSchema>
export type Shortlist = z.infer<typeof shortlistSchema>
export type MediaPlan = z.infer<typeof mediaPlanSchema>
export type CampaignMode = z.infer<typeof campaignModeSchema>
export type PlanningSummary = z.infer<typeof planningSummarySchema>
export type PlanningWorkspace = z.infer<typeof planningWorkspaceSchema>
