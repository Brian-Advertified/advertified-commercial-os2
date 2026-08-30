import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableText = z.string().nullable()

export const inventoryValuesSchema = z.object({
  productCode: nullableText,
  name: nullableText,
  channel: nullableText,
  productType: nullableText,
  geography: nullableText,
  address: nullableText,
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  rateType: nullableText,
  currency: nullableText,
  rateAmountMinor: z.number().int().nonnegative().nullable(),
  availability: nullableText,
  extension: z.record(z.string(), z.string()).nullable(),
}).strict()

const inventoryEvidenceSchema = z.object({
  fieldName: requiredText,
  rawValue: nullableText,
  normalizedValue: nullableText,
  transformation: requiredText,
  sourceLocator: requiredText,
  sourceHash: requiredText,
}).strict()

const inventoryValidationSchema = z.object({
  fieldName: requiredText,
  code: requiredText,
  message: requiredText,
  isBlocking: z.boolean(),
}).strict()

export const inventoryCandidateSchema = z.object({
  id: z.guid(),
  importId: z.guid(),
  rowNumber: z.number().int().positive(),
  status: requiredText,
  values: inventoryValuesSchema,
  validation: z.array(inventoryValidationSchema),
  evidence: z.array(inventoryEvidenceSchema),
  sourceLocator: requiredText,
  reviewedBy: z.guid().nullable(),
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const inventoryStepSchema = z.object({
  stepType: requiredText,
  status: requiredText,
  startedAtUtc: z.iso.datetime({ offset: true }),
  completedAtUtc: z.iso.datetime({ offset: true }).nullable(),
}).strict()

export const inventoryImportSchema = z.object({
  id: z.guid(),
  supplierId: z.guid(),
  supplierName: requiredText,
  fileName: requiredText,
  declaredMediaType: requiredText,
  documentClass: nullableText,
  status: requiredText,
  scanStatus: requiredText,
  sourceHash: requiredText,
  sourceSize: z.number().int().positive(),
  failureCode: nullableText,
  steps: z.array(inventoryStepSchema),
  candidates: z.array(inventoryCandidateSchema),
  candidateCounts: z.object({
    total: z.number().int().nonnegative(),
    reviewRequired: z.number().int().nonnegative(),
    approved: z.number().int().nonnegative(),
    rejected: z.number().int().nonnegative(),
    blocking: z.number().int().nonnegative(),
  }).strict(),
  nextCandidateCursor: z.string().nullable(),
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const inventoryProductSummarySchema = z.object({
  id: z.guid(), supplierId: z.guid(), supplierName: requiredText,
  productCode: requiredText, name: requiredText, channel: requiredText,
  productType: requiredText, geography: requiredText, verification: requiredText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const inventoryProductPageSchema = z.object({
  items: z.array(inventoryProductSummarySchema),
  nextCursor: z.string().nullable(),
  maximumSourceBytes: z.number().int().positive(),
}).strict()

const inventoryRateSchema = z.object({
  rateType: requiredText, currency: requiredText,
  amountMinor: z.number().int().nonnegative(), sourceLocator: requiredText,
}).strict()

const inventoryAvailabilitySchema = z.object({
  status: requiredText,
  observedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  validUntilUtc: z.iso.datetime({ offset: true }).nullable(),
  sourceLocator: requiredText,
}).strict()

const inventoryAssetSchema = z.object({
  assetType: requiredText, mediaType: requiredText,
  contentHash: requiredText, sourceReference: requiredText,
}).strict()

export const inventoryBenchmarkSchema = z.object({
  productId: z.guid(),
  productVersionId: z.guid(),
  rateId: z.guid(),
  rateType: requiredText,
  rateAmountMinor: z.number().int().nonnegative(),
  currency: requiredText,
  policyVersion: requiredText,
  geographyBasis: requiredText,
  cohortSize: z.number().int().nonnegative(),
  medianMinor: z.number().int().nonnegative().nullable(),
  lowerQuartileMinor: z.number().int().nonnegative().nullable(),
  upperQuartileMinor: z.number().int().nonnegative().nullable(),
  percentile: z.number().min(0).max(100).nullable(),
  differenceFromMedianMinor: z.number().int().nullable(),
  differenceFromMedianPercent: z.number().nullable(),
  position: requiredText,
  confidence: z.number().min(0).max(1),
  comparables: z.array(z.object({
    productId: z.guid(), productVersionId: z.guid(), name: requiredText,
    geography: requiredText, rateAmountMinor: z.number().int().nonnegative(),
    currency: requiredText, distanceKilometres: z.number().nonnegative().nullable(),
  }).strict()),
  exclusions: z.array(requiredText),
}).strict()

export const inventoryProductSchema = z.object({
  product: inventoryProductSummarySchema,
  address: nullableText,
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  extension: z.record(z.string(), z.string()),
  rate: inventoryRateSchema,
  availability: inventoryAvailabilitySchema,
  assets: z.array(inventoryAssetSchema),
  sourceImportId: z.guid(), sourceCandidateId: z.guid(),
  versionNumber: z.number().int().positive(),
  publishedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export type InventoryValues = z.infer<typeof inventoryValuesSchema>
export type InventoryCandidate = z.infer<typeof inventoryCandidateSchema>
export type InventoryImport = z.infer<typeof inventoryImportSchema>
export type InventoryProductSummary = z.infer<typeof inventoryProductSummarySchema>
export type InventoryProductPage = z.infer<typeof inventoryProductPageSchema>
export type InventoryProduct = z.infer<typeof inventoryProductSchema>
export type InventoryBenchmark = z.infer<typeof inventoryBenchmarkSchema>
