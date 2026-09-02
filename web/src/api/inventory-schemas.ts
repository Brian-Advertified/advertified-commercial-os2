import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableText = z.string().nullable()

const inventoryAudienceSegmentSchema = z.object({
  label: requiredText,
  sharePercent: z.number().min(0).max(100).nullable(),
}).strict()

const inventoryAudienceMeasurementSchema = z.object({
  metricType: requiredText,
  value: z.number().nonnegative().nullable(),
  unit: nullableText,
  universe: nullableText,
  measurementSource: nullableText,
  measurementPeriod: nullableText,
  methodology: nullableText,
  limitations: nullableText,
}).strict()

const inventoryAudienceProfileSchema = z.object({
  spokenLanguages: z.array(inventoryAudienceSegmentSchema),
  understoodLanguages: z.array(inventoryAudienceSegmentSchema),
  lifeStages: z.array(inventoryAudienceSegmentSchema),
  lsmSemSegments: z.array(inventoryAudienceSegmentSchema),
  taxonomyName: nullableText,
  taxonomyVersion: nullableText,
  universe: nullableText,
  measurementSource: nullableText,
  measurementPeriod: nullableText,
  methodology: nullableText,
  limitations: nullableText,
  measurements: z.array(inventoryAudienceMeasurementSchema).nullish().transform(value => value ?? []),
}).strict()

export const inventorySupplierCommercialSchema = z.object({
  vatStatus: nullableText, vatNumber: nullableText,
  commissionTerms: nullableText, paymentTerms: nullableText,
  cancellationTerms: nullableText, bookingDeadlineTerms: nullableText,
}).strict()

const supplierContactSchema = z.object({
  name: nullableText, role: nullableText, region: nullableText,
  email: nullableText, phone: nullableText, website: nullableText,
  socialHandle: nullableText,
}).strict()

export const inventoryCommercialTermsSchema = z.object({
  vatTreatment: nullableText,
  rateValidFrom: z.iso.date().nullable(), rateValidTo: z.iso.date().nullable(),
  productionCostMinor: z.number().int().nonnegative().nullable(),
  installationCostMinor: z.number().int().nonnegative().nullable(),
  minimumOrder: z.number().int().positive().nullable(),
  discountTerms: nullableText,
  inclusions: z.array(requiredText), exclusions: z.array(requiredText),
  conditions: z.array(requiredText),
  bookingLeadTimeDays: z.number().int().nonnegative().nullable(),
  bookingDeadline: z.iso.date().nullable(), materialDeadline: z.iso.date().nullable(),
  cancellationTerms: nullableText,
}).strict()

export const inventoryDeliverableSchema = z.object({
  format: nullableText, buyingUnit: nullableText, dimensions: nullableText,
  placement: nullableText, programme: nullableText, daypart: nullableText,
  spotLengthSeconds: z.number().int().positive().nullable(),
  loopLengthSeconds: z.number().int().positive().nullable(),
  slotLengthSeconds: z.number().int().positive().nullable(),
  playsPerLoop: z.number().int().positive().nullable(),
  quantity: z.number().int().positive().nullable(),
  creativeSpecification: nullableText,
}).strict()

const pointOfInterestSchema = z.object({
  name: requiredText, category: nullableText,
  latitude: z.number().min(-90).max(90).nullish().transform(value => value ?? null),
  longitude: z.number().min(-180).max(180).nullish().transform(value => value ?? null),
}).strict()

export const inventorySpatialSchema = z.object({
  country: nullableText, province: nullableText, municipality: nullableText,
  locality: nullableText, venue: nullableText, road: nullableText, route: nullableText,
  trafficDirection: nullableText,
  facingBearingDegrees: z.number().min(0).max(359.999999).nullable(),
  pointsOfInterest: z.array(pointOfInterestSchema),
  coverageGeoJson: nullableText.nullish().transform(value => value ?? null),
  catchmentGeoJson: nullableText.nullish().transform(value => value ?? null),
  routeGeoJson: nullableText.nullish().transform(value => value ?? null),
  directionGeoJson: nullableText.nullish().transform(value => value ?? null),
}).strict()

const packageSchema = z.object({
  packageCode: nullableText, packageName: nullableText,
  componentProductCodes: z.array(requiredText), discountRule: nullableText,
  conditions: z.array(requiredText),
}).strict()

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
  audienceProfile: inventoryAudienceProfileSchema.nullable(),
  description: nullableText.nullish().transform(value => value ?? null),
  supplierCommercial: inventorySupplierCommercialSchema.nullish().transform(value => value ?? null),
  supplierContacts: z.array(supplierContactSchema).nullish().transform(value => value ?? []),
  commercialTerms: inventoryCommercialTermsSchema.nullish().transform(value => value ?? null),
  deliverable: inventoryDeliverableSchema.nullish().transform(value => value ?? null),
  spatial: inventorySpatialSchema.nullish().transform(value => value ?? null),
  package: packageSchema.nullish().transform(value => value ?? null),
}).strict()

const inventoryEvidenceSchema = z.object({
  fieldName: requiredText,
  rawValue: nullableText,
  normalizedValue: nullableText,
  transformation: requiredText,
  sourceLocator: requiredText,
  sourceHash: requiredText,
  evidenceBasis: requiredText,
  verificationState: requiredText,
  requiredAction: requiredText,
  capturedAtUtc: z.iso.datetime({ offset: true }),
  effectiveOn: z.iso.date().nullable(),
  freshUntil: z.iso.date().nullable(),
  extractionMethod: requiredText,
  extractionConfidence: z.number().min(0).max(1).nullable(),
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
  effectiveFrom: z.iso.date().nullable(), effectiveTo: z.iso.date().nullable(),
  vatTreatment: nullableText,
  commercialTerms: inventoryCommercialTermsSchema.nullable(),
}).strict()

const inventoryAvailabilitySchema = z.object({
  status: requiredText,
  observedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  validUntilUtc: z.iso.datetime({ offset: true }).nullable(),
  sourceLocator: requiredText,
}).strict()

const inventoryPublishedAudienceProfileSchema = inventoryAudienceProfileSchema.extend({
  sourceLocator: requiredText,
}).strict()

export const inventoryAssetSchema = z.object({
  assetType: requiredText, mediaType: requiredText,
  contentHash: requiredText, sourceReference: requiredText,
  assetId: z.guid().nullable(), rightsStatus: nullableText,
  rightsBasis: nullableText, licensedUntil: z.iso.date().nullable(),
  proposalEligible: z.boolean(), rightsVersion: z.number().int().positive(),
  rightsScopes: z.array(requiredText), territoryCode: requiredText,
  effectiveOn: z.iso.date().nullable(), untilRevoked: z.boolean(),
}).strict()

export const inventoryAvailabilityExceptionSchema = z.object({
  id: z.guid(), productId: z.guid(), productVersionId: z.guid(),
  exceptionType: requiredText, startsOn: z.iso.date(), endsOn: z.iso.date(),
  sourceLocator: requiredText, evidenceHash: requiredText, recordedBy: z.guid(),
  recordedAtUtc: z.iso.datetime({ offset: true }), version: z.number().int().positive(),
}).strict()

export const inventoryAssetRightsReviewSchema = z.object({
  assetId: z.guid(), rightsStatus: requiredText, rightsBasis: nullableText,
  licensedUntil: z.iso.date().nullable(), reviewedBy: z.guid(),
  reviewedAtUtc: z.iso.datetime({ offset: true }), version: z.number().int().positive(),
  scopeCodes: z.array(requiredText), territoryCode: requiredText,
  effectiveOn: z.iso.date().nullable(), untilRevoked: z.boolean(),
  attestorRole: nullableText, evidenceReference: nullableText, evidenceHash: nullableText,
}).strict()

export const inventorySemanticRecallSchema = z.object({
  productId: z.guid(), productVersionId: z.guid(), name: requiredText,
  geography: requiredText, similarity: z.number().min(-1).max(1),
}).strict()

export const inventoryEmbeddingSchema = z.object({
  id: z.guid(), productId: z.guid(), productVersionId: z.guid(),
  provider: requiredText, model: requiredText, inputHash: requiredText,
  dimensions: z.number().int().positive(), createdAtUtc: z.iso.datetime({ offset: true }),
  version: z.number().int().positive(), jobId: z.guid().nullable(),
  inputTokens: z.number().int().nonnegative(),
  incrementalCostUsdMicros: z.number().int().nonnegative(),
  monthlyCostUsdMicros: z.number().int().nonnegative(),
  monthlyBudgetUsdMicros: z.number().int().positive(), budgetAlert: z.boolean(),
}).strict()

export const inventoryDuplicateCandidateSchema = z.object({
  id: z.guid(), leftProductId: z.guid(), rightProductId: z.guid(),
  leftProductVersionId: z.guid(), rightProductVersionId: z.guid(),
  leftName: requiredText, rightName: requiredText, method: requiredText,
  similarity: z.number().min(0).max(1).nullable(), evidenceJson: requiredText,
  status: requiredText, canonicalProductId: z.guid().nullable(),
  reviewedBy: z.guid().nullable(), reviewedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  reviewReason: z.string().nullable(), version: z.number().int().positive(),
}).strict()

export const inventoryDuplicateCandidatesSchema = z.array(inventoryDuplicateCandidateSchema)
export const inventorySemanticRecallsSchema = z.array(inventorySemanticRecallSchema)

const supplierCommercialViewSchema = inventorySupplierCommercialSchema.extend({
  versionNumber: z.number().int().positive(), sourceImportId: z.guid(),
  publishedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const supplierContactViewSchema = supplierContactSchema.extend({
  id: z.guid(), observedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const packageViewSchema = z.object({
  id: z.guid(), packageCode: requiredText,
  versionNumber: z.number().int().positive(), name: requiredText,
  discountRule: nullableText, conditions: z.array(requiredText),
  componentProductCodes: z.array(requiredText),
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
  productVersionId: z.guid(),
  address: nullableText,
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  extension: z.record(z.string(), z.string()),
  rate: inventoryRateSchema,
  availability: inventoryAvailabilitySchema,
  audienceProfile: inventoryPublishedAudienceProfileSchema.nullable(),
  assets: z.array(inventoryAssetSchema),
  sourceImportId: z.guid(), sourceCandidateId: z.guid(),
  versionNumber: z.number().int().positive(),
  publishedAtUtc: z.iso.datetime({ offset: true }),
  description: nullableText,
  supplierCommercial: supplierCommercialViewSchema.nullable(),
  supplierContacts: z.array(supplierContactViewSchema),
  deliverable: inventoryDeliverableSchema.nullable(), spatial: inventorySpatialSchema.nullable(),
  packages: z.array(packageViewSchema),
  availabilityExceptions: z.array(inventoryAvailabilityExceptionSchema),
}).strict()

export type InventoryValues = z.infer<typeof inventoryValuesSchema>
export type InventoryCandidate = z.infer<typeof inventoryCandidateSchema>
export type InventoryImport = z.infer<typeof inventoryImportSchema>
export type InventoryProductSummary = z.infer<typeof inventoryProductSummarySchema>
export type InventoryProductPage = z.infer<typeof inventoryProductPageSchema>
export type InventoryProduct = z.infer<typeof inventoryProductSchema>
export type InventoryBenchmark = z.infer<typeof inventoryBenchmarkSchema>
export type InventoryDuplicateCandidate = z.infer<typeof inventoryDuplicateCandidateSchema>
export type InventorySemanticRecall = z.infer<typeof inventorySemanticRecallSchema>
