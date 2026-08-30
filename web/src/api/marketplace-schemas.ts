import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const dateTime = z.iso.datetime({ offset: true })

export const marketplaceListingVersionSchema = z.object({
  id: z.guid(),
  versionNumber: z.number().int().positive(),
  productVersionId: z.guid(),
  rateId: z.guid(),
  availabilityId: z.guid(),
  supplierName: requiredText,
  productName: requiredText,
  channel: requiredText,
  productType: requiredText,
  geography: requiredText,
  rateType: requiredText,
  amountMinor: z.number().int().nonnegative(),
  currency: requiredText,
  availability: requiredText,
  availabilityValidUntilUtc: dateTime.nullable(),
  terms: requiredText,
  publishedBy: z.guid(),
  publishedAtUtc: dateTime,
}).strict()

export const marketplaceListingSchema = z.object({
  id: z.guid(),
  supplierTenantId: z.guid(),
  productId: z.guid(),
  status: requiredText,
  currentVersion: marketplaceListingVersionSchema.nullable(),
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
}).strict()

export const marketplaceListingPageSchema = z.object({
  items: z.array(marketplaceListingSchema),
  nextCursor: z.string().nullable(),
}).strict()

export const marketplaceResponseSchema = z.object({
  id: z.guid(),
  rfqId: z.guid(),
  responseVersion: z.number().int().positive(),
  amountMinor: z.number().int().nonnegative(),
  currency: requiredText,
  availability: requiredText,
  terms: requiredText,
  validUntilUtc: dateTime,
  evidenceReferences: z.array(requiredText),
  submittedBy: z.guid(),
  submittedAtUtc: dateTime,
  acceptedBy: z.guid().nullable(),
  acceptedAtUtc: dateTime.nullable(),
}).strict()

export const marketplaceRfqSchema = z.object({
  id: z.guid(),
  buyerTenantId: z.guid(),
  supplierTenantId: z.guid(),
  listingVersionId: z.guid(),
  supplierName: requiredText,
  productName: requiredText,
  subject: requiredText,
  requestedStart: z.iso.date(),
  requestedEnd: z.iso.date(),
  quantity: z.number().int().positive(),
  dueAtUtc: dateTime,
  status: requiredText,
  response: marketplaceResponseSchema.nullable(),
  createdBy: z.guid(),
  sentBy: z.guid().nullable(),
  sentAtUtc: dateTime.nullable(),
  version: z.number().int().positive(),
  updatedAtUtc: dateTime,
}).strict()

export const marketplaceRfqPageSchema = z.object({
  items: z.array(marketplaceRfqSchema),
  nextCursor: z.string().nullable(),
}).strict()

export type MarketplaceListing = z.infer<typeof marketplaceListingSchema>
export type MarketplaceListingPage = z.infer<typeof marketplaceListingPageSchema>
export type MarketplaceRfq = z.infer<typeof marketplaceRfqSchema>
export type MarketplaceRfqPage = z.infer<typeof marketplaceRfqPageSchema>
