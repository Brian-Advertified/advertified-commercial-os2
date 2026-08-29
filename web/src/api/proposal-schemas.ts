import { z } from 'zod'
import { proposalPolicy } from '../proposal/proposal-policy'

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

export const proposalRecipientSchema = z.object({
  userId: z.guid(),
  displayName: requiredText,
  email: z.email(),
  role: requiredText,
}).strict()

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
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const approvedPlanChoicesSchema = z.array(approvedPlanChoiceSchema)
export const proposalRecipientsSchema = z.array(proposalRecipientSchema)

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

export type ApprovedPlanChoice = z.infer<typeof approvedPlanChoiceSchema>
export type ProposalRecipient = z.infer<typeof proposalRecipientSchema>
export type ProposalOption = z.infer<typeof proposalOptionSchema>
export type Proposal = z.infer<typeof proposalSchema>
export type ProposalDraftInput = z.infer<typeof proposalDraftInputSchema>
export type ProposalUpdateInput = z.infer<typeof proposalUpdateInputSchema>
