import type { ZodType } from 'zod'
import { request } from './client'
import {
  approvedPlanChoicesSchema,
  proposalRecipientsSchema,
  proposalSchema,
  type ApprovedPlanChoice,
  type Proposal,
  type ProposalDraftInput,
  type ProposalOption,
  type ProposalRecipient,
} from './proposal-schemas'

async function create<T>(path: string, schema: ZodType<T>, body: unknown, token: string): Promise<T> {
  return (await request(
    path,
    schema,
    { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() },
  )).data
}

async function mutate<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  version: number,
): Promise<T> {
  return (await request(
    path,
    schema,
    { method: 'POST', body: JSON.stringify(body) },
    {
      antiforgeryToken: token,
      expectedVersion: version,
      idempotencyKey: crypto.randomUUID(),
    },
  )).data
}

export const proposalApi = {
  async listApprovedPlans(tenantId: string, briefId: string): Promise<ApprovedPlanChoice[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/briefs/${briefId}/approved-plans`,
      approvedPlanChoicesSchema,
    )).data
  },

  async listRecipients(tenantId: string): Promise<ProposalRecipient[]> {
    return (await request(
      `/api/v1/tenants/${tenantId}/proposal-recipients`,
      proposalRecipientsSchema,
    )).data
  },

  async get(tenantId: string, proposalId: string): Promise<Proposal> {
    return (await request(
      `/api/v1/tenants/${tenantId}/proposals/${proposalId}`,
      proposalSchema,
    )).data
  },

  generate(
    tenantId: string,
    briefId: string,
    input: ProposalDraftInput,
    token: string,
  ): Promise<Proposal> {
    return create(
      `/api/v1/tenants/${tenantId}/briefs/${briefId}/proposals:generate`,
      proposalSchema,
      input,
      token,
    )
  },

  update(
    tenantId: string,
    proposal: Proposal,
    input: {
      title: string
      executiveSummary: string
      terms: string
      expiryAtUtc: string
      options: Pick<ProposalOption, 'id' | 'label' | 'outcome'>[]
    },
    token: string,
  ): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:update`,
      proposalSchema,
      {
        ...input,
        options: input.options.map(item => ({
          optionId: item.id,
          label: item.label,
          outcome: item.outcome,
        })),
      },
      token,
      proposal.version,
    )
  },

  approve(tenantId: string, proposal: Proposal, token: string): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:approve`,
      proposalSchema,
      { reason: 'Client wording, commercial totals and approved plan bindings reviewed.' },
      token,
      proposal.version,
    )
  },

  render(tenantId: string, proposal: Proposal, token: string): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:render`,
      proposalSchema,
      {},
      token,
      proposal.version,
    )
  },

  share(
    tenantId: string,
    proposal: Proposal,
    recipientUserId: string,
    token: string,
  ): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:share`,
      proposalSchema,
      { recipientUserId, reason: 'Share the approved proposal for the client decision.' },
      token,
      proposal.version,
    )
  },

  selectOption(
    tenantId: string,
    proposal: Proposal,
    optionId: string,
    token: string,
  ): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:select-option`,
      proposalSchema,
      { optionId, reason: 'Client selected this proposal route.' },
      token,
      proposal.version,
    )
  },

  decline(tenantId: string, proposal: Proposal, token: string): Promise<Proposal> {
    return mutate(
      `/api/v1/tenants/${tenantId}/proposal-versions/${proposal.id}:decline`,
      proposalSchema,
      { reason: 'Client declined the current proposal.' },
      token,
      proposal.version,
    )
  },

  documentUrl(tenantId: string, documentId: string): string {
    return `/api/v1/tenants/${tenantId}/proposal-documents/${documentId}`
  },
}
