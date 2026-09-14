import type { ZodType } from 'zod'
import { masterDataCodes } from '../generated/master-data-codes'
import { request } from './client'
import {
  audienceStrategySchema,
  inventoryIntelligenceArtifactSchema,
  inventoryIntelligencePayloadSchema,
  mediaMixSchema,
  mediaPlanSchema,
  campaignModeSchema,
  planningSummariesSchema,
  planningWorkspaceSchema,
  shortlistSchema,
  type AudienceStrategy,
  type MediaAllocation,
  type MediaImpactEstimate,
  type MediaMix,
  type MediaPlan,
  type CampaignMode,
  type PlanningSummary,
  type PlanningWorkspace,
  type Shortlist,
} from './planning-schemas'

async function create<T>(
  path: string,
  schema: ZodType<T>,
  token: string,
): Promise<T> {
  return (await request(path, schema,
    { method: 'POST', body: JSON.stringify({}) },
    { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() })).data
}

async function mutate<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  version: number,
): Promise<T> {
  return (await request(path, schema,
    { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, expectedVersion: version,
      idempotencyKey: crypto.randomUUID() })).data
}

export const planningApi = {
  async list(tenantId: string): Promise<PlanningSummary[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/planning`,
      planningSummariesSchema,
    )).data
  },

  async getWorkspace(tenantId: string, briefVersionId: string): Promise<PlanningWorkspace> {
    return (await request(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`,
      planningWorkspaceSchema,
    )).data
  },

  async selectCampaignMode(
    tenantId: string,
    briefVersionId: string,
    mode: string,
    token: string,
    decision?: { source: string; confidence: number; reason: string },
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<CampaignMode> {
    const resolved = decision ?? {
      source: masterDataCodes.campaignModeDecisionSources.humanSelection,
      confidence: 1,
      reason: 'Campaign media selection confirmed before planning began.',
    }
    return (await request(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/campaign-mode:select`,
      campaignModeSchema,
      { method: 'POST', body: JSON.stringify({
        mode,
        decisionSource: resolved.source,
        confidence: resolved.confidence,
        reason: resolved.reason,
      }) },
      { antiforgeryToken: token, idempotencyKey },
    )).data
  },

  generateAudiences(tenantId: string, briefVersionId: string, token: string) {
    return create(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/audiences:generate`,
      audienceStrategySchema, token)
  },

  approveAudience(
    tenantId: string,
    audience: AudienceStrategy,
    targetAudienceIds: string[],
    targetingRationale: string,
    positioningStatement: string,
    token: string,
  ) {
    return mutate(
      `/api/v1/tenants/${tenantId}/audience-strategies/${audience.id}:approve`,
      audienceStrategySchema,
      { targetAudienceIds, targetingRationale, positioningStatement,
        reason: 'Audience strategy reviewed and approved for media planning.' },
      token, audience.version)
  },

  generateMix(tenantId: string, briefVersionId: string, token: string): Promise<MediaMix> {
    return create(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/media-mixes:generate`,
      mediaMixSchema, token)
  },

  updateMix(
    tenantId: string,
    mix: MediaMix,
    allocations: MediaAllocation[],
    token: string,
    impactEstimate: MediaImpactEstimate | null = mix.impactEstimate,
  ): Promise<MediaMix> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-mix-versions/${mix.id}:update`,
      mediaMixSchema,
      { allocations, impactEstimate,
        reason: 'Planner adjusted channel allocation, geography, running periods or planning impact.' },
      token, mix.version)
  },

  approveMix(tenantId: string, mix: MediaMix, token: string): Promise<MediaMix> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-mix-versions/${mix.id}:approve`,
      mediaMixSchema, { reason: 'Media mix confirmed for inventory planning.' },
      token, mix.version)
  },

  generateShortlist(tenantId: string, briefVersionId: string, token: string): Promise<Shortlist> {
    return create(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/shortlists:generate`,
      shortlistSchema, token)
  },

  selectShortlist(
    tenantId: string,
    shortlist: Shortlist,
    selectedCandidateIds: string[],
    token: string,
    reason: string,
  ): Promise<Shortlist> {
    return mutate(
      `/api/v1/tenants/${tenantId}/shortlist-versions/${shortlist.id}:select`,
      shortlistSchema,
      { selectedCandidateIds, reason },
      token, shortlist.version)
  },

  async explainShortlist(tenantId: string, shortlistId: string, token: string) {
    const artifact = (await request(
      `/api/v1/tenants/${tenantId}/shortlist-versions/${shortlistId}/intelligence/inventory`,
      inventoryIntelligenceArtifactSchema,
      { method: 'POST' },
      { antiforgeryToken: token },
    )).data
    const payload = inventoryIntelligencePayloadSchema.parse(JSON.parse(artifact.artifactJson))
    return { artifact, interpretations: payload.interpretations }
  },

  generatePlan(tenantId: string, briefVersionId: string, token: string): Promise<MediaPlan> {
    return create(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/media-plans:generate`,
      mediaPlanSchema, token)
  },

  resolveObjection(
    tenantId: string,
    plan: MediaPlan,
    objectionCode: string,
    token: string,
    reason: string,
  ): Promise<MediaPlan> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}/objections/${objectionCode}:resolve`,
      mediaPlanSchema,
      { resolution: masterDataCodes.objectionResolutions.acceptedWithReason,
        reason: reason.trim() },
      token, plan.version)
  },

  approvePlan(tenantId: string, plan: MediaPlan, token: string): Promise<MediaPlan> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}:approve`,
      mediaPlanSchema, { reason: 'Media plan reconciled and confirmed.' },
      token, plan.version)
  },
}
