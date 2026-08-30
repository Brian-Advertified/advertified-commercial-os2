import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableGuid = z.guid().nullable()

export const inboundMailboxSchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  address: z.email(),
  provider: requiredText,
  ownerUserId: z.guid(),
  defaultClientAccountId: nullableGuid,
  autoSendEnabled: z.boolean(),
  allowedSenderDomains: z.array(requiredText),
  isEnabled: z.boolean(),
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const inboundAttachmentSchema = z.object({
  providerAttachmentId: requiredText,
  fileName: requiredText,
  mediaType: requiredText,
  sizeBytes: z.number().int().nonnegative(),
}).strict()

export const inboundCampaignEmailSchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  mailboxId: z.guid(),
  providerEmailId: requiredText,
  providerMessageId: requiredText,
  senderEmail: z.email(),
  senderName: z.string().nullable(),
  replyToEmail: z.email(),
  subject: requiredText,
  sourceHash: requiredText,
  attachments: z.array(inboundAttachmentSchema),
  status: requiredText,
  failureCode: z.string().nullable(),
  receivedAtUtc: z.iso.datetime({ offset: true }),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const emailAutomationRunSchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  inboundEmailId: z.guid(),
  campaignMode: requiredText,
  status: requiredText,
  checkpoint: requiredText,
  clientAccountId: nullableGuid,
  briefId: nullableGuid,
  briefVersionId: nullableGuid,
  stpVersionId: nullableGuid,
  mediaMixVersionId: nullableGuid,
  shortlistVersionId: nullableGuid,
  mediaPlanVersionId: nullableGuid,
  proposalVersionId: nullableGuid,
  documentId: nullableGuid,
  failureCode: z.string().nullable(),
  failureMessage: z.string().nullable(),
  deliveryProviderId: z.string().nullable(),
  incrementalAiCostMinor: z.number().int().nonnegative(),
  version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const emailAutomationQuestionSchema = z.object({
  fieldPath: requiredText,
  question: requiredText,
  options: z.array(requiredText),
}).strict()

export const inboundEmailDetailSchema = z.object({
  email: inboundCampaignEmailSchema,
  run: emailAutomationRunSchema,
  sourceContent: requiredText,
  questions: z.array(emailAutomationQuestionSchema),
}).strict()

export const inboundEmailPageSchema = z.object({
  items: z.array(inboundCampaignEmailSchema),
  nextCursor: z.string().nullable(),
}).passthrough()

export type InboundMailbox = z.infer<typeof inboundMailboxSchema>
export type InboundCampaignEmail = z.infer<typeof inboundCampaignEmailSchema>
export type EmailAutomationRun = z.infer<typeof emailAutomationRunSchema>
export type EmailAutomationQuestion = z.infer<typeof emailAutomationQuestionSchema>
export type InboundEmailDetail = z.infer<typeof inboundEmailDetailSchema>
export type InboundEmailPage = z.infer<typeof inboundEmailPageSchema>

export type InboundMailboxInput = {
  address: string
  provider: string
  ownerUserId: string
  defaultClientAccountId: string | null
  autoSendEnabled: boolean
  allowedSenderDomains: string[]
}

export type EmailAutomationClarification = {
  fieldPath: string
  value: string
}
