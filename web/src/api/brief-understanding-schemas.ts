import { z } from 'zod'
import { briefAssumptionSchema, briefConflictSchema, briefUnknownSchema } from './schemas'

const requiredText = z.string().trim().min(1)
const optionalText = z.string().trim().min(1).nullable()

export const suppliedBriefQuestionSchema = z.object({
  fieldPath: requiredText,
  question: requiredText,
  isBlocking: z.boolean(),
  options: z.array(requiredText),
}).strict()

export const suppliedBriefEvidenceSchema = z.object({
  fieldPath: requiredText,
  kind: requiredText,
  excerpt: z.string(),
  confidence: z.number().min(0).max(1),
  sourceLocator: requiredText,
}).strict()

export const suppliedBriefUsageSchema = z.object({
  provider: requiredText,
  model: requiredText,
  promptVersion: requiredText,
  researchStatus: requiredText,
  toolCalls: z.number().int().nonnegative(),
  incrementalCostMinor: z.number().int().nonnegative(),
}).strict()

export const suppliedBriefDraftSchema = z.object({
  businessProblem: z.string(),
  objective: z.string(),
  audiences: z.array(requiredText),
  geographies: z.array(requiredText),
  timing: z.string(),
  budgetMinor: z.number().int().nonnegative().nullable(),
  budgetUnknown: z.boolean(),
  currency: optionalText,
  vatStatus: optionalText,
  feesMinor: z.number().int().nonnegative().nullable(),
  mediaRequirements: z.array(requiredText),
  constraints: z.array(requiredText),
  measurement: z.array(requiredText),
  facts: z.array(z.string()),
  unknowns: z.array(briefUnknownSchema),
  assumptions: z.array(briefAssumptionSchema),
  conflicts: z.array(briefConflictSchema),
}).strict()

export const suppliedBriefUnderstandingSchema = z.object({
  clientName: optionalText,
  title: requiredText,
  campaignMode: optionalText,
  campaignModeConfidence: z.number().min(0).max(1),
  requiresHumanClarification: z.boolean(),
  campaignModeRationale: requiredText,
  draft: suppliedBriefDraftSchema,
  questions: z.array(suppliedBriefQuestionSchema),
  evidence: z.array(suppliedBriefEvidenceSchema),
  usage: suppliedBriefUsageSchema,
}).strict()

export type BriefClarification = { fieldPath: string; value: string }
export type SuppliedBriefUnderstanding = z.infer<typeof suppliedBriefUnderstandingSchema>
export type SuppliedBriefQuestion = z.infer<typeof suppliedBriefQuestionSchema>
