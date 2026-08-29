import { z } from 'zod'

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
  classification: z.string(),
  exclusions: z.array(z.string()),
  evidenceItemIds: z.array(z.guid()),
  confidence: z.number(),
  status: z.string(),
})

export const audienceSetSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
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

export const shortlistCandidateSchema = z.object({
  id: z.guid(),
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
  isSelected: z.boolean().nullable(),
  benchmark: benchmarkSchema.nullable(),
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
  inventoryProductId: z.guid(),
  productVersionId: z.guid(),
  rateId: z.guid(),
  availabilityId: z.guid().nullable(),
  name: z.string(),
  channel: z.string(),
  geography: z.string(),
  runningPeriods: z.array(runningPeriodSchema).min(1),
  quantity: z.number().int().positive(),
  supplierCostMinor: z.number().int().nonnegative(),
  clientPriceMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  availability: z.string(),
  rateFreshness: z.string(),
  supplySource: z.string(),
  lastConfirmedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  supplyConfidence: z.string(),
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
  subtotalMinor: z.number().int().nonnegative(),
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
})

export const planningWorkspaceSchema = z.object({
  briefVersionId: z.guid(),
  audience: audienceSetSchema.nullable(),
  mediaMix: mediaMixSchema.nullable(),
  shortlist: shortlistSchema.nullable(),
  mediaPlan: mediaPlanSchema.nullable(),
})

export type RunningPeriod = z.infer<typeof runningPeriodSchema>
export type MediaAllocation = z.infer<typeof mediaAllocationSchema>
export type MediaMix = z.infer<typeof mediaMixSchema>
export type ShortlistCandidate = z.infer<typeof shortlistCandidateSchema>
export type Shortlist = z.infer<typeof shortlistSchema>
export type MediaPlan = z.infer<typeof mediaPlanSchema>
export type PlanningWorkspace = z.infer<typeof planningWorkspaceSchema>
