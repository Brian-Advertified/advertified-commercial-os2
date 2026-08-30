import { request } from './client'
import {
  emailAutomationRunSchema,
  inboundEmailDetailSchema,
  inboundEmailPageSchema,
  inboundMailboxSchema,
  type EmailAutomationClarification,
  type EmailAutomationRun,
  type InboundEmailDetail,
  type InboundEmailPage,
  type InboundMailbox,
  type InboundMailboxInput,
} from './email-automation-schemas'

function tenantPath(tenantId: string, suffix: string) {
  return `/api/v1/tenants/${tenantId}/email-automation/${suffix}`
}

export const emailAutomationApi = {
  async getMailbox(tenantId: string): Promise<InboundMailbox | null> {
    return (await request(
      tenantPath(tenantId, 'mailbox'),
      inboundMailboxSchema.nullable(),
    )).data
  },

  async configureMailbox(
    tenantId: string,
    input: InboundMailboxInput,
    antiforgeryToken: string,
    current: InboundMailbox | null,
  ): Promise<InboundMailbox> {
    return (await request(
      tenantPath(tenantId, 'mailbox'),
      inboundMailboxSchema,
      { method: current ? 'PUT' : 'POST', body: JSON.stringify(input) },
      {
        antiforgeryToken,
        expectedVersion: current?.version,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },

  async listMessages(
    tenantId: string,
    cursor?: string,
  ): Promise<InboundEmailPage> {
    const query = new URLSearchParams({ pageSize: '25' })
    if (cursor) query.set('cursor', cursor)
    return (await request(
      `${tenantPath(tenantId, 'messages')}?${query}`,
      inboundEmailPageSchema,
    )).data
  },

  async getMessage(
    tenantId: string,
    inboundEmailId: string,
  ): Promise<InboundEmailDetail> {
    return (await request(
      tenantPath(tenantId, `messages/${inboundEmailId}`),
      inboundEmailDetailSchema,
    )).data
  },

  async processMessage(
    tenantId: string,
    inboundEmailId: string,
    antiforgeryToken: string,
  ): Promise<EmailAutomationRun> {
    return (await request(
      tenantPath(tenantId, `messages/${inboundEmailId}:process`),
      emailAutomationRunSchema,
      { method: 'POST', body: JSON.stringify({}) },
      { antiforgeryToken, idempotencyKey: crypto.randomUUID() },
    )).data
  },

  async retryMessage(
    tenantId: string,
    run: EmailAutomationRun,
    clarifications: EmailAutomationClarification[],
    antiforgeryToken: string,
  ): Promise<EmailAutomationRun> {
    return (await request(
      tenantPath(tenantId, `messages/${run.inboundEmailId}:retry`),
      emailAutomationRunSchema,
      {
        method: 'POST',
        body: JSON.stringify({
          reason: clarifications.length > 0
            ? 'Apply the confirmed unclear Brief details.'
            : 'Retry the prepared proposal request.',
          clarifications,
        }),
      },
      {
        antiforgeryToken,
        expectedVersion: run.version,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },
}
