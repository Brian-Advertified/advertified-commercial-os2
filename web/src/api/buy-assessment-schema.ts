import { z } from 'zod'

const measurement = z.number().nonnegative().nullable()

export const buyAssessmentSchema = z.object({
  campaignSupplierCostMinor: measurement,
  currency: z.string().nullable(),
  reach: measurement,
  impressions: measurement,
  averageFrequency: measurement,
  costPerThousandImpressionsMinor: measurement,
  costPerPersonReachedMinor: measurement,
  universe: z.string().nullable(),
  measurementPeriod: z.string().nullable(),
  measurementSource: z.string().nullable(),
  methodology: z.string().nullable(),
  isTargetAudience: z.boolean(),
  digitalExposure: z.object({
    spotLengthSeconds: measurement, slotLengthSeconds: measurement,
    loopLengthSeconds: measurement, playsPerLoop: measurement,
    loopSharePercent: z.number().min(0).max(100).nullable(),
  }).nullable(),
  evidenceGaps: z.array(z.string()),
  plannerReasoning: z.object({
    plannedChannelRole: z.string().nullable(),
    targetContexts: z.array(z.object({ name: z.string(), needState: z.string().nullable(), buyingContext: z.string().nullable() })),
    requiredPlacesMatched: z.number().int().nonnegative(), requiredPlacesTotal: z.number().int().nonnegative(),
    hasMeasuredTargetAudience: z.boolean(), reviewQuestions: z.array(z.string()),
    supportedReasons: z.array(z.string()), buyingWarnings: z.array(z.string()),
  }).nullable().optional(),
  decision: z.object({
    code: z.enum(['BUY', 'NEEDS_REVIEW', 'DO_NOT_BUY']),
    supportedReasons: z.array(z.string()), blockingReasons: z.array(z.string()),
  }).nullable().optional(),
})

export type BuyAssessment = z.infer<typeof buyAssessmentSchema>
