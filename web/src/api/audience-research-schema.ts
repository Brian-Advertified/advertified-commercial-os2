import { z } from 'zod'

const optional = (maximum: number) => z.string().trim().min(1).max(maximum).nullable()
export const audienceResearchSchema = z.object({
  audienceName: z.string().trim().min(1).max(300),
  sourceLocator: z.string().trim().min(1).max(2000), sourceExcerpt: z.string().trim().min(1).max(4000),
  measurementPeriod: z.string().trim().min(1).max(200), methodology: z.string().trim().min(1).max(1000),
  language: optional(100), lifeStage: optional(200), lsmSem: optional(100),
  lsmSemTaxonomy: optional(200), lsmSemTaxonomyVersion: optional(100),
  needState: optional(1000), buyingContext: optional(500), messageContext: optional(200), momentContext: optional(200),
}).refine(value => !value.lsmSem || !!(value.lsmSemTaxonomy && value.lsmSemTaxonomyVersion),
  'A segmentation group needs its taxonomy and version.')
export type AudienceResearch = z.infer<typeof audienceResearchSchema>
