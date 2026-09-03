import { z } from 'zod'

const requiredCode = z.string().trim().min(1)
const timestamp = z.iso.datetime({ offset: true })

export const agentBudgetSchema = z.object({
  agentCode: requiredCode,
  displayLabel: z.string().trim().min(1),
  provider: requiredCode,
  model: requiredCode,
  costCapMinor: z.number().int().nonnegative(),
  usageCount: z.number().int().nonnegative(),
  incrementalCostMinor: z.number().int().nonnegative(),
  lastUsedAtUtc: timestamp.nullable(),
}).strict()

export const agentUsageSchema = z.object({
  id: z.guid(),
  agentCode: requiredCode,
  workType: requiredCode,
  status: requiredCode,
  provider: requiredCode,
  model: requiredCode,
  units: z.number().int().nonnegative().nullable(),
  toolCalls: z.number().int().nonnegative().nullable(),
  incrementalCostMinor: z.number().int().nonnegative(),
  recordedAtUtc: timestamp,
}).strict()

export const agentOperationalRunSchema = z.object({
  id: z.guid(),
  opportunityId: z.guid().nullable(),
  campaignId: z.guid().nullable(),
  runKind: requiredCode,
  status: requiredCode,
  currentStep: requiredCode.nullable(),
  attempts: z.number().int().nonnegative(),
  errorCode: requiredCode.nullable(),
  incrementalCostMinor: z.number().int().nonnegative(),
  updatedAtUtc: timestamp,
}).strict()

export const agentOperationsSchema = z.object({
  currency: requiredCode,
  provider: requiredCode,
  liveProviderEnabled: z.boolean(),
  totalIncrementalCostMinor: z.number().int().nonnegative(),
  durableRunCount: z.number().int().nonnegative(),
  attentionRunCount: z.number().int().nonnegative(),
  agents: z.array(agentBudgetSchema),
  recentUsage: z.array(agentUsageSchema),
  recentRuns: z.array(agentOperationalRunSchema),
}).strict()

export type AgentBudget = z.infer<typeof agentBudgetSchema>
export type AgentUsage = z.infer<typeof agentUsageSchema>
export type AgentOperationalRun = z.infer<typeof agentOperationalRunSchema>
export type AgentOperations = z.infer<typeof agentOperationsSchema>
