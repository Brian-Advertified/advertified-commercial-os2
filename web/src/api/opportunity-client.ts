import type { ZodType } from 'zod'
import {
  clientAccountPageSchema,
  evidenceItemSchema,
  evidenceSetSchema,
  evidenceSourceSchema,
  humanTaskPageSchema,
  interpretationSchema,
  opportunityAngleSchema,
  opportunityDetailSchema,
  opportunityPageSchema,
  opportunitySchema,
  strategySchema,
  agentRunSchema,
  criticObjectionSchema,
  type ClientAccount,
  type HumanTask,
  type Opportunity,
  type OpportunityDetail,
  type Strategy,
  type AgentRun,
} from './schemas'
import { request } from './client'
import { opportunityCodes } from './opportunity-constants'

type CreateOpportunity = {
  clientId: string
  title: string
  sourceType: string
  sourceRef: string | null
  ownerUserId: string
  problemSummary: string | null
  objectiveSummary: string | null
}

type RegisterSource = {
  opportunityId: string
  type: typeof opportunityCodes.sourceType.suppliedText
  locator: string
  title: string
  policyBasis: typeof opportunityCodes.policyBasis.ownerSupplied
  content: string
  reviewerUserId: string
  claims: Array<{
    locator: string
    claimType: string
    structuredValueJson: string
    excerpt: string
    confidence: number
  }>
}

async function command<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  antiforgeryToken: string,
  expectedVersion?: number,
): Promise<T> {
  return (await request(
    path,
    schema,
    { method: 'POST', body: JSON.stringify(body) },
    {
      antiforgeryToken,
      expectedVersion,
      idempotencyKey: crypto.randomUUID(),
    },
  )).data
}

export const opportunityApi = {
  async list(tenantId: string): Promise<Opportunity[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/opportunities`,
      opportunityPageSchema,
    )).data.items
  },

  async get(tenantId: string, id: string): Promise<OpportunityDetail> {
    return (await request(
      `/api/v1/tenants/${tenantId}/opportunities/${id}`,
      opportunityDetailSchema,
    )).data
  },

  async listClients(tenantId: string): Promise<ClientAccount[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/client-accounts`,
      clientAccountPageSchema,
    )).data.items
  },

  async listTasks(tenantId: string): Promise<HumanTask[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/human-tasks`,
      humanTaskPageSchema,
    )).data.items
  },

  async getStrategy(tenantId: string, id: string): Promise<Strategy> {
    return (await request(
      `/api/v1/tenants/${tenantId}/strategies/${id}`,
      strategySchema,
    )).data
  },

  async getRun(tenantId: string, id: string): Promise<AgentRun> {
    return (await request(
      `/api/v1/tenants/${tenantId}/agent-runs/${id}`,
      agentRunSchema,
    )).data
  },

  create(tenantId: string, body: CreateOpportunity, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunities`, opportunitySchema,
      { ...body, expectedValueMinor: null, currency: null, deadline: null }, token)
  },

  registerSource(tenantId: string, opportunityId: string, body: RegisterSource, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunities/${opportunityId}/evidence-sources`,
      evidenceSourceSchema, body, token)
  },

  startQualification(tenantId: string, opportunityId: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunities/${opportunityId}/qualification:start`,
      opportunitySchema, { comment: null }, token, version)
  },

  reviewEvidence(
    tenantId: string,
    itemId: string,
    version: number,
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/evidence-items/${itemId}/review`,
      evidenceItemSchema,
      { decision: opportunityCodes.reviewDecision.approve,
        structuredValueJson: null, reason: null },
      token,
      version,
    )
  },

  submitEvidence(
    tenantId: string,
    opportunityId: string,
    approverUserId: string,
    version: number,
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunities/${opportunityId}/evidence:submit`,
      evidenceSetSchema,
      { gaps: [], approverUserId },
      token,
      version,
    )
  },

  approveEvidence(tenantId: string, setId: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/evidence-sets/${setId}:approve`,
      evidenceSetSchema, { reason: 'Reviewed against the retained source.' }, token, version)
  },

  queue(
    tenantId: string,
    opportunityId: string,
    action: 'interpret' | 'angles:generate' | 'strategies:generate',
    token: string,
    approverUserId?: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunities/${opportunityId}/${action}`,
      agentRunSchema, { approverUserId: approverUserId ?? null }, token)
  },

  confirmInterpretation(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/business-interpretations/${id}:confirm`,
      interpretationSchema, { comment: null }, token, version)
  },

  selectAngle(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/opportunity-angles/${id}:select`,
      opportunityAngleSchema, { reason: 'Selected after evidence review.' }, token, version)
  },

  resolveObjection(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/critic-objections/${id}:resolve`,
      criticObjectionSchema,
      { resolution: opportunityCodes.objectionResolution.addressed,
        reason: 'Recorded as an explicit planning constraint.' },
      token,
      version,
    )
  },

  submitStrategy(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/strategy-versions/${id}:submit`,
      strategySchema, { comment: null }, token, version)
  },

  approveStrategy(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/strategy-versions/${id}:approve`,
      strategySchema, { reason: 'Approved for Brief drafting.' }, token, version)
  },

  rejectStrategy(tenantId: string, id: string, version: number, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/strategy-versions/${id}:reject`,
      strategySchema, { reason: 'Returned for revision after assigned review.' }, token, version)
  },
}
