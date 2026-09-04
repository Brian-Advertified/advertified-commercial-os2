import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableText = z.string().nullable()

const inventorySemanticSourcePreflightSchema = z.object({
  importId: z.guid(),
  inputArtifactId: z.guid(),
  fileName: requiredText,
  sourceHash: requiredText,
  documentClass: z.string(),
  importStatus: requiredText,
  safeToReproject: z.boolean(),
  packetCount: z.number().int().nonnegative(),
  imageCount: z.number().int().nonnegative(),
  sourceItemCount: z.number().int().nonnegative(),
  maximumCostUsdMicros: z.number().int().nonnegative(),
  newMaximumCostUsdMicros: z.number().int().nonnegative(),
  largestPacketCostUsdMicros: z.number().int().nonnegative(),
  blocker: nullableText,
}).strict()

export const inventorySemanticPreflightSchema = z.object({
  projectionVersion: requiredText,
  provider: z.string(),
  model: z.string(),
  promptVersion: requiredText,
  budgetScope: z.string(),
  inputPricePerMillionTokensUsdMicros:
    z.number().int().nonnegative(),
  outputPricePerMillionTokensUsdMicros:
    z.number().int().nonnegative(),
  perCallCostCapUsdMicros:
    z.number().int().nonnegative(),
  certificationBudgetUsdMicros:
    z.number().int().positive(),
  existingCommittedCostUsdMicros:
    z.number().int().nonnegative(),
  newMaximumCostUsdMicros:
    z.number().int().nonnegative(),
  worstCaseTotalCostUsdMicros:
    z.number().int().nonnegative(),
  liveExecutionEnabled: z.boolean(),
  readyToActivate: z.boolean(),
  blockers: z.array(requiredText),
  sources: z.array(
    inventorySemanticSourcePreflightSchema),
}).strict()

export type InventorySemanticPreflight = z.infer<
  typeof inventorySemanticPreflightSchema>
