import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const dateTime = z.iso.datetime({ offset: true })
const nullableGuid = z.guid().nullable()
const nullableDateTime = dateTime.nullable()
const nullableText = z.string().nullable()

export const purchaseOrderSchema = z.object({
  id: z.guid(),
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  proposalDecisionId: z.guid(),
  purchaseOrderNumber: requiredText,
  contentSha256: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().positive(),
  amountMinor: z.number().int().nonnegative(),
  currency: requiredText,
  status: requiredText,
  submittedBy: z.guid(),
  submittedAtUtc: dateTime,
  approvedBy: nullableGuid,
  approvedAtUtc: nullableDateTime,
  reconciliationReason: nullableText,
  version: z.number().int().positive(),
}).strict()

export const invoiceSchema = z.object({
  id: z.guid(),
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  purchaseOrderId: z.guid(),
  invoiceNumber: requiredText,
  subtotalMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  totalMinor: z.number().int().nonnegative(),
  currency: requiredText,
  status: requiredText,
  issuedBy: z.guid(),
  issuedAtUtc: dateTime,
  version: z.number().int().positive(),
}).strict()

export const paymentIntentSchema = z.object({
  id: z.guid(),
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  purchaseOrderId: z.guid(),
  invoiceId: z.guid(),
  methodCode: requiredText,
  amountMinor: z.number().int().nonnegative(),
  currency: requiredText,
  status: requiredText,
  startedBy: z.guid(),
  startedAtUtc: dateTime,
  reconciledBy: nullableGuid,
  reconciledAtUtc: nullableDateTime,
  reconciliationReference: nullableText,
  reconciliationReason: nullableText,
  receiptSha256: nullableText,
  version: z.number().int().positive(),
}).strict()

export const fundingWorkspaceSchema = z.object({
  purchaseOrders: z.array(purchaseOrderSchema),
  invoices: z.array(invoiceSchema),
  payments: z.array(paymentIntentSchema),
}).strict()

export const purchaseOrderInputSchema = z.object({
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  purchaseOrderNumber: requiredText.max(200),
  amountMinor: z.number().int().positive(),
  currency: requiredText.length(3),
}).strict()

export const purchaseOrderApprovalSchema = z.object({
  reconciliationReason: requiredText.max(1000),
}).strict()

export const invoiceInputSchema = z.object({
  invoiceNumber: requiredText.max(200),
}).strict()

export const paymentReconciliationSchema = z.object({
  reconciliationReference: requiredText.max(300),
  reason: requiredText.max(1000),
}).strict()

export type PurchaseOrder = z.infer<typeof purchaseOrderSchema>
export type Invoice = z.infer<typeof invoiceSchema>
export type PaymentIntent = z.infer<typeof paymentIntentSchema>
export type FundingWorkspace = z.infer<typeof fundingWorkspaceSchema>
export type PurchaseOrderInput = z.infer<typeof purchaseOrderInputSchema>
