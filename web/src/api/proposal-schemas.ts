import { z } from 'zod'
import { proposalPolicy } from '../proposal/proposal-policy'
import {
  inventoryCommercialTermsSchema,
  inventoryDeliverableSchema,
  inventorySpatialSchema,
  inventorySupplierCommercialSchema,
} from './inventory-schemas'

const requiredText = z.string().trim().min(1)

export const proposalRunningPeriodSchema = z.object({
  channel: requiredText,
  start: z.iso.date(),
  end: z.iso.date(),
}).strict()

export const approvedPlanChoiceSchema = z.object({
  id: z.guid(),
  briefVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  totalMinor: z.number().int().nonnegative(),
  currency: requiredText,
  channels: z.array(requiredText).min(1),
  runningPeriods: z.array(proposalRunningPeriodSchema),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const proposalSummarySchema = z.object({
  id: z.guid(),
  briefId: z.guid(),
  versionNumber: z.number().int().positive(),
  title: requiredText,
  status: requiredText,
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const proposalInventoryLineSchema = z.object({
  inventoryTenantId: z.guid(),
  marketplaceListingVersionId: z.guid().nullable(),
  inventoryProductId: z.guid(),
  productVersionId: z.guid(),
  rateId: z.guid(),
  availabilityId: z.guid().nullable(),
  name: requiredText,
  channel: requiredText,
  geography: requiredText,
  runningPeriods: z.array(proposalRunningPeriodSchema),
  quantity: z.number().int().positive(),
  clientPriceMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(),
  availability: requiredText,
  rateFreshness: requiredText,
  supplyConfidence: requiredText,
  supplySource: requiredText,
  lastConfirmedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  uncertainties: z.array(requiredText),
  supplierCommercial: inventorySupplierCommercialSchema.nullish().transform(value => value ?? null),
  commercialTerms: inventoryCommercialTermsSchema.nullish().transform(value => value ?? null),
  deliverable: inventoryDeliverableSchema.nullish().transform(value => value ?? null),
  spatial: inventorySpatialSchema.nullish().transform(value => value ?? null),
  logoAssetId: z.guid().nullish().transform(value => value ?? null),
}).strict()

export const proposalRecipientSchema = z.object({
  userId: z.guid(),
  displayName: requiredText,
  email: z.email(),
  role: requiredText,
}).strict()

export const proposalApproverSchema = proposalRecipientSchema

export const proposalOptionSchema = z.object({
  id: z.guid(),
  label: requiredText,
  outcome: requiredText,
  planVersionId: z.guid(),
  planVersionNumber: z.number().int().positive(),
  budgetMinor: z.number().int().nonnegative(),
  currency: requiredText,
  displayOrder: z.number().int().positive(),
  channels: z.array(requiredText).min(1),
  runningPeriods: z.array(proposalRunningPeriodSchema),
  inventoryNames: z.array(requiredText),
  inventory: z.array(proposalInventoryLineSchema),
}).strict()

const proposalDocumentSchema = z.object({
  id: z.guid(),
  mediaType: z.literal('application/pdf'),
  contentHash: requiredText,
  sizeBytes: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const proposalDecisionSchema = z.object({
  decision: requiredText,
  optionId: z.guid().nullable(),
  reason: z.string().nullable(),
  decidedBy: z.guid(),
  decidedAtUtc: z.iso.datetime({ offset: true }),
  recordedForExternalParty: z.boolean(),
  externalPartyEmail: z.email().nullable(),
  evidenceReference: z.string().nullable(),
}).strict()

export const proposalSchema = z.object({
  id: z.guid(),
  briefId: z.guid(),
  briefVersionId: z.guid(),
  versionNumber: z.number().int().positive(),
  title: requiredText,
  executiveSummary: requiredText,
  terms: requiredText,
  expiryAtUtc: z.iso.datetime({ offset: true }),
  status: requiredText,
  options: z.array(proposalOptionSchema).min(proposalPolicy.minimumOptions).max(proposalPolicy.maximumOptions),
  document: proposalDocumentSchema.nullable(),
  recipientUserId: z.guid().nullable(),
  decision: proposalDecisionSchema.nullable(),
  createdBy: z.guid(),
  approvedBy: z.guid().nullable(),
  approvalMode: requiredText.nullable(),
  approvalAssigneeUserId: z.guid().nullable(),
  approvalRequestedBy: z.guid().nullable(),
  approvalRequestedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  approvalRejectedBy: z.guid().nullable(),
  approvalRejectionReason: z.string().nullable(),
  approvalRejectedAtUtc: z.iso.datetime({ offset: true }).nullable(),
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const proposalSummariesSchema = z.array(proposalSummarySchema)
export const approvedPlanChoicesSchema = z.array(approvedPlanChoiceSchema)
export const proposalRecipientsSchema = z.array(proposalRecipientSchema)
export const proposalApproversSchema = z.array(proposalApproverSchema)

export const proposalUpdateInputSchema = z.object({
  title: requiredText.max(300),
  executiveSummary: requiredText.max(5_000),
  terms: requiredText.max(10_000),
  expiryAtUtc: z.iso.datetime({ offset: true }),
  options: z.array(z.object({
    id: z.guid(),
    label: requiredText.max(200),
    outcome: requiredText.max(2_000),
  }).strict()).min(proposalPolicy.minimumOptions).max(proposalPolicy.maximumOptions),
}).strict()

export const proposalDraftInputSchema = z.object({
  title: requiredText.max(300),
  terms: requiredText.max(10_000),
  expiryAtUtc: z.iso.datetime({ offset: true }),
  options: z.array(z.object({
    planVersionId: z.guid(),
    label: requiredText.max(200),
    outcome: requiredText.max(2_000),
  }).strict()).min(proposalPolicy.minimumOptions).max(proposalPolicy.maximumOptions),
}).strict().refine(
  value => new Set(value.options.map(item => item.planVersionId)).size === value.options.length,
  { message: 'Each proposal choice must use a different approved plan.' },
)

export type ProposalSummary = z.infer<typeof proposalSummarySchema>
export type ApprovedPlanChoice = z.infer<typeof approvedPlanChoiceSchema>
export type ProposalRecipient = z.infer<typeof proposalRecipientSchema>
export type ProposalApprover = z.infer<typeof proposalApproverSchema>
export type ProposalOption = z.infer<typeof proposalOptionSchema>
export type Proposal = z.infer<typeof proposalSchema>
export type ProposalDraftInput = z.infer<typeof proposalDraftInputSchema>
export type ProposalUpdateInput = z.infer<typeof proposalUpdateInputSchema>
