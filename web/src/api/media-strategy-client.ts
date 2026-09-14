import { z } from 'zod'
import { ApiFailure, request } from './client'
import { masterDataCodes } from '../generated/master-data-codes'

export const mediaStrategyPayloadSchema = z.object({
  summary: z.string().min(1),
  channelRecommendations: z.array(z.object({
    channel: z.string().min(1), role: z.string().min(1), rationale: z.string().min(1),
    objectiveContribution: z.string(), geographyRole: z.string().nullable(),
    classification: z.string().min(1), budgetGuidancePercent: z.number().min(0).max(100).nullable(),
    tradeOffs: z.array(z.string()), evidenceGaps: z.array(z.string()),
  })),
  strategicPrinciples: z.array(z.string()), excludedChannels: z.array(z.string()),
  evidenceGaps: z.array(z.string()),
})

const mediaStrategyRecordSchema = z.object({
  id: z.guid(), subjectId: z.guid(), subjectVersion: z.number().int().positive(),
  serviceCode: z.literal(masterDataCodes.agentTypes.mediaStrategy),
  artifactJson: z.string(), status: z.string(), version: z.number().int().positive(),
  unknowns: z.array(z.string()), assumptions: z.array(z.string()),
})

export type MediaStrategyPayload = z.infer<typeof mediaStrategyPayloadSchema>
export type MediaStrategyRecord = z.infer<typeof mediaStrategyRecordSchema> & { details: MediaStrategyPayload }

function decode(record: z.infer<typeof mediaStrategyRecordSchema>): MediaStrategyRecord {
  try {
    return { ...record, details: mediaStrategyPayloadSchema.parse(JSON.parse(record.artifactJson)) }
  } catch {
    throw new ApiFailure('INVALID_API_RESPONSE', 200)
  }
}

function route(tenantId: string, briefVersionId: string) {
  return `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/intelligence/media-strategy`
}

export const mediaStrategyApi = {
  async getLatest(tenantId: string, briefVersionId: string): Promise<MediaStrategyRecord | null> {
    try {
      return decode((await request(route(tenantId, briefVersionId), mediaStrategyRecordSchema)).data)
    } catch (failure) {
      // The canonical endpoint uses 404 for a Brief that has no strategy yet.
      if (failure instanceof ApiFailure && failure.status === 404) return null
      throw failure
    }
  },
  async analyse(tenantId: string, briefVersionId: string, token: string): Promise<MediaStrategyRecord> {
    return decode((await request(route(tenantId, briefVersionId), mediaStrategyRecordSchema,
      { method: 'POST' }, { antiforgeryToken: token })).data)
  },
  async approve(tenantId: string, briefVersionId: string, strategy: MediaStrategyRecord,
    token: string): Promise<MediaStrategyRecord> {
    return decode((await request(`${route(tenantId, briefVersionId)}/${strategy.id}/approve`,
      mediaStrategyRecordSchema,
      { method: 'POST', body: JSON.stringify({ expectedVersion: strategy.version }) },
      { antiforgeryToken: token })).data)
  },
}
