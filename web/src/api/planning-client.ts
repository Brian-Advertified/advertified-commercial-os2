import type { ZodType } from 'zod'
import { masterDataCodes } from '../generated/master-data-codes'
import { request } from './client'
import {
  audienceSetSchema,
  mediaMixSchema,
  mediaPlanSchema,
  planningWorkspaceSchema,
  shortlistSchema,
  type MediaAllocation,
  type MediaMix,
  type MediaPlan,
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
  async getWorkspace(tenantId: string, briefVersionId: string): Promise<PlanningWorkspace> {
    return (await request(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/planning`,
      planningWorkspaceSchema,
    )).data
  },

  generateAudiences(tenantId: string, briefVersionId: string, token: string) {
    return create(
      `/api/v1/tenants/${tenantId}/brief-versions/${briefVersionId}/audiences:generate`,
      audienceSetSchema, token)
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
  ): Promise<MediaMix> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-mix-versions/${mix.id}:update`,
      mediaMixSchema,
      { allocations, reason: 'Planner adjusted channel allocation and running periods.' },
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
  ): Promise<Shortlist> {
    return mutate(
      `/api/v1/tenants/${tenantId}/shortlist-versions/${shortlist.id}:select`,
      shortlistSchema,
      { selectedCandidateIds, reason: 'Planner confirmed selected inventory.' },
      token, shortlist.version)
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
  ): Promise<MediaPlan> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}/objections/${objectionCode}:resolve`,
      mediaPlanSchema,
      { resolution: masterDataCodes.objectionResolutions.acceptedWithReason,
        reason: 'Planner reviewed the visible uncertainty and accepts it for internal planning.' },
      token, plan.version)
  },

  approvePlan(tenantId: string, plan: MediaPlan, token: string): Promise<MediaPlan> {
    return mutate(
      `/api/v1/tenants/${tenantId}/media-plan-versions/${plan.id}:approve`,
      mediaPlanSchema, { reason: 'Media plan reconciled and confirmed.' },
      token, plan.version)
  },
}
