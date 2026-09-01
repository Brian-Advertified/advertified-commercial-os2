import type { ZodType } from 'zod'
import { suppliedBriefUnderstandingSchema, type BriefClarification, type SuppliedBriefUnderstanding } from './brief-understanding-schemas'
import { request } from './client'
import {
  briefVersionSchema,
  campaignBriefSchema,
  campaignBriefSummarySchema,
  type BriefVersion,
  type CampaignBrief,
} from './schemas'

export type CreateBrief = {
  title: string
  ownerUserId: string
  sourceLocator: string
  sourceTitle: string
  sourceContent: string
  clientId?: string | null
  clientName?: string | null
  sourceType?: string | null
}

export type UnderstandBrief = {
  sourceTitle: string
  sourceContent: string
  clarifications: BriefClarification[]
}

export type CreateBriefVersion = {
  briefId: string
  baseVersionId: string | null
  businessProblem: string
  objective: string
  audiences: string[]
  geographies: string[]
  timing: string
  budgetMinor: number | null
  budgetUnknown: boolean
  currency: string | null
  vatStatus: string | null
  feesMinor: number | null
  constraints: string[]
  measurement: string[]
  facts: string[]
  unknowns: Array<{ fieldPath: string; question: string; isBlocking: boolean }>
  assumptions: Array<{
    fieldPath: string
    value: string
    impact: string
    validationNeeded: string
  }>
  conflicts: Array<{
    fieldPath: string
    description: string
    severity: string
    resolved: boolean
    resolution: string | null
  }>
  evidenceItemIds: string[]
}

async function command<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  expectedVersion?: number,
  idempotencyKey: string = crypto.randomUUID(),
): Promise<T> {
  return (await request(
    path,
    schema,
    { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, expectedVersion, idempotencyKey },
  )).data
}

export const briefApi = {
  async understand(
    tenantId: string,
    body: UnderstandBrief,
    token: string,
  ): Promise<SuppliedBriefUnderstanding> {
    return (await request(
      `/api/v1/tenants/${tenantId}/briefs:understand`,
      suppliedBriefUnderstandingSchema,
      { method: 'POST', body: JSON.stringify(body) },
      { antiforgeryToken: token },
    )).data
  },

  async get(tenantId: string, briefId: string): Promise<CampaignBrief> {
    return (await request(
      `/api/v1/tenants/${tenantId}/briefs/${briefId}`,
      campaignBriefSchema,
    )).data
  },

  create(tenantId: string, body: CreateBrief, token: string, idempotencyKey?: string) {
    return command(
      `/api/v1/tenants/${tenantId}/briefs`, campaignBriefSummarySchema, body, token,
      undefined, idempotencyKey)
  },

  createVersion(
    tenantId: string,
    briefId: string,
    body: CreateBriefVersion,
    token: string,
    idempotencyKey?: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/briefs/${briefId}/versions`,
      briefVersionSchema, body, token, undefined, idempotencyKey)
  },

  submit(
    tenantId: string,
    version: BriefVersion,
    token: string,
    confirmerUserId: string | null = null,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/brief-versions/${version.id}:submit`,
      briefVersionSchema, { confirmerUserId, comment: null }, token, version.version)
  },

  markReady(
    tenantId: string,
    version: BriefVersion,
    token: string,
    idempotencyKey?: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/brief-versions/${version.id}:ready`,
      briefVersionSchema, {}, token, version.version, idempotencyKey)
  },

  approve(tenantId: string, version: BriefVersion, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/brief-versions/${version.id}:approve`,
      briefVersionSchema, { reason: 'Confirmed for planning.' }, token, version.version)
  },

  async confirm(
    tenantId: string,
    version: BriefVersion,
    token: string,
  ) {
    const submitted = await this.submit(tenantId, version, token)
    return this.approve(tenantId, submitted, token)
  },
}
