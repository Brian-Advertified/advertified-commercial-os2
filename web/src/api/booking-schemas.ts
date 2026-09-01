import { z } from 'zod'

const nullableInteger = z.number().int().nullable()

export const bookablePlanLineSchema = z.object({
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  proposalDecisionId: z.guid(),
  planVersionId: z.guid(),
  mediaPlanLineId: z.guid(),
  supplierName: z.string(),
  productName: z.string(),
  channel: z.string(),
  geography: z.string(),
  flightStart: z.iso.date(),
  flightEnd: z.iso.date(),
  runningPeriods: z.number().int().positive(),
  quantity: z.number().int().positive(),
  clientPriceMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  currency: z.string(),
  alreadyBooked: z.boolean(),
}).strict()

export const bookingSchema = z.object({
  id: z.guid(),
  buyerTenantId: z.guid(),
  supplierTenantId: z.guid(),
  proposalVersionId: z.guid().nullable(),
  proposalOptionId: z.guid().nullable(),
  proposalDecisionId: z.guid().nullable(),
  planVersionId: z.guid().nullable(),
  mediaPlanLineId: z.guid().nullable(),
  marketplaceListingVersionId: z.guid(),
  supplierName: z.string(),
  productName: z.string(),
  channel: z.string(),
  geography: z.string(),
  flightStart: z.iso.date(),
  flightEnd: z.iso.date(),
  runningPeriods: z.number().int().positive(),
  quantity: z.number().int().positive(),
  supplierCostMinor: z.number().int().nonnegative().nullable().optional(),
  clientPriceMinor: nullableInteger,
  feesMinor: nullableInteger,
  vatMinor: nullableInteger,
  currency: z.string(),
  terms: z.string(),
  status: z.string(),
  createdBy: z.guid(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  requestedBy: z.guid().nullable(),
  requestedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  requestReason: z.string().nullable(),
  confirmedBy: z.guid().nullable(),
  confirmedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  confirmationReason: z.string().nullable(),
  supplierNote: z.string().nullable().optional(),
  termsAccepted: z.boolean(),
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const bookablePlanLinesSchema = z.array(bookablePlanLineSchema)
export const bookingsSchema = z.array(bookingSchema)

export type BookablePlanLine = z.infer<typeof bookablePlanLineSchema>
export type Booking = z.infer<typeof bookingSchema>
