import type { ZodType } from 'zod'
import { masterDataCodes } from '../generated/master-data-codes'
import {
  agencyPageSchema,
  clientAccountPageSchema,
  contactPageSchema,
  currentUserSchema,
  problemSchema,
  sessionSchema,
  tenantSchema,
  workspaceListSchema,
  type BrowserSession,
  type CurrentUser,
  type ProfileUpdate,
  type Tenant,
  type Workspace,
} from './schemas'

const safeMessages: Readonly<Record<string, string>> = {
  AUTHENTICATION_REQUIRED: 'Your session has ended. Sign in again to continue.',
  APPROVAL_REQUIRED: 'This action belongs to the assigned operator or reviewer.',
  [masterDataCodes.agentFailureReasons.evidenceRequired]:
    'Complete and approve the required evidence first.',
  CSRF_VALIDATION_FAILED: 'Refresh the page and try that action again.',
  IDEMPOTENCY_CONFLICT: 'That request key was already used. Try the action again.',
  ORIGIN_NOT_ALLOWED: 'Open Advertified from its configured local address.',
  TENANT_FORBIDDEN: 'You do not have access to this workspace or action.',
  VALIDATION_FAILED: 'Review the information and try again.',
  VERSION_CONFLICT: 'This information changed. Refresh it before saving again.',
  INVALID_LIFECYCLE_TRANSITION: 'Complete the current workflow step first.',
  INVENTORY_PUBLISH_BLOCKED: 'Resolve the blocking inventory fields before publishing.',
  INVENTORY_PROTECTION_UNAVAILABLE: 'File protection is unavailable. Try again shortly.',
  PROPOSAL_INPUT_STALE: 'The approved plan changed. Prepare a new proposal from the current plan.',
  PROPOSAL_APPROVAL_BLOCKED: 'Review the proposal choices, pricing and validity before continuing.',
  PROPOSAL_EXPIRED: 'This proposal has expired. Prepare a current version before continuing.',
  PROPOSAL_DOCUMENT_REQUIRED: 'Generate the approved proposal PDF before sharing it.',
  PROPOSAL_DECISION_RECORDED: 'A client decision has already been recorded for this proposal.',
  CAMPAIGN_MODE_REQUIRED: 'Choose out-of-home only or full campaign before planning.',
  CAMPAIGN_MODE_LOCKED: 'This campaign type is locked. Start a new campaign to change it.',
  OOH_SUPPLY_CONFIRMATION_REQUIRED:
    'Confirm current rates and availability, then rebuild the out-of-home shortlist.',
  INVENTORY_BENCHMARK_UNAVAILABLE: 'There is not enough current comparable OOH data yet.',
  INBOUND_MAILBOX_NOT_CONFIGURED: 'Connect the OOH proposal mailbox before receiving requests.',
  EMAIL_AUTOMATION_NOT_RETRYABLE: 'Only a request that needs review can be checked again.',
  EMAIL_ATTACHMENT_BLOCKED: 'Review the attachment before continuing this request.',
  EMAIL_PAYLOAD_UNAVAILABLE: 'The complete incoming email could not be retrieved.',
  EMAIL_PROVIDER_UNAVAILABLE: 'The email service is unavailable. Try again shortly.',
  [masterDataCodes.automationFailureReasons.invalidRecipient]:
    'A safe reply address could not be confirmed.',
  [masterDataCodes.automationFailureReasons.clientNotResolved]:
    'The client could not be identified from the email or mailbox setup.',
  [masterDataCodes.automationFailureReasons.nonOohRequest]:
    'This request includes media beyond OOH. Start a new full campaign from the beginning.',
  [masterDataCodes.automationFailureReasons.incompleteBrief]:
    'The email needs a clear client, objective, audience, geography, dates, budget and VAT status.',
  [masterDataCodes.automationFailureReasons.attachmentReviewRequired]:
    'An attachment requires review before it can be used.',
  [masterDataCodes.automationFailureReasons.stpUnready]:
    'The segmentation, targeting or positioning evidence is not ready.',
  [masterDataCodes.automationFailureReasons.supplyUnready]:
    'Confirmed inventory, rates or availability are not ready for every OOH selection.',
  [masterDataCodes.automationFailureReasons.planUnready]:
    'The media plan has an unresolved commercial issue.',
  [masterDataCodes.automationFailureReasons.proposalUnready]:
    'The proposal is not ready to be sent.',
  [masterDataCodes.automationFailureReasons.deliveryFailed]:
    'The proposal was prepared, but the email could not be delivered.',
}

export const sessionExpiredEvent = 'advertified:session-expired'

export class ApiFailure extends Error {
  public readonly code: string
  public readonly status: number
  public readonly correlationId?: string

  public constructor(
    code: string,
    status: number,
    correlationId?: string,
  ) {
    super(safeMessages[code] ?? 'Something went wrong. Try again in a moment.')
    this.name = 'ApiFailure'
    this.code = code
    this.status = status
    this.correlationId = correlationId
  }
}

type RequestOptions = {
  antiforgeryToken?: string
  expectedVersion?: number
  idempotencyKey?: string
}

export async function request<T>(
  path: string,
  schema: ZodType<T>,
  init: RequestInit = {},
  options: RequestOptions = {},
): Promise<{ data: T; etag?: string }> {
  const headers = createHeaders(init, options)
  const response = await safeFetch(path, { ...init, credentials: 'same-origin', headers })
  const payload = await readJson(response)
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new Event(sessionExpiredEvent))
    throw toFailure(response.status, payload)
  }
  const parsed = schema.safeParse(payload)
  if (!parsed.success) throw new ApiFailure('INVALID_API_RESPONSE', response.status)
  return { data: parsed.data, etag: response.headers.get('ETag') ?? undefined }
}

function createHeaders(init: RequestInit, options: RequestOptions): Headers {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  headers.set('X-Correlation-ID', crypto.randomUUID())
  if (options.antiforgeryToken) headers.set('X-CSRF-TOKEN', options.antiforgeryToken)
  if (options.expectedVersion) headers.set('If-Match', `"${options.expectedVersion}"`)
  if (options.idempotencyKey) headers.set('Idempotency-Key', options.idempotencyKey)
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  return headers
}

async function safeFetch(path: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(path, init)
  } catch {
    throw new ApiFailure('NETWORK_UNAVAILABLE', 0)
  }
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json()
  } catch {
    throw new ApiFailure('INVALID_API_RESPONSE', response.status)
  }
}

function toFailure(status: number, payload: unknown): ApiFailure {
  const problem = problemSchema.safeParse(payload)
  if (!problem.success || !problem.data.code) return new ApiFailure('UNEXPECTED_ERROR', status)
  return new ApiFailure(
    problem.data.code,
    status,
    problem.data.correlationId ?? undefined,
  )
}

export const api = {
  async getSession(): Promise<BrowserSession> {
    return (await request('/api/v1/session', sessionSchema)).data
  },

  async signIn(antiforgeryToken: string): Promise<BrowserSession> {
    return (await request(
      '/api/v1/session',
      sessionSchema,
      { method: 'POST' },
      { antiforgeryToken },
    )).data
  },

  async signOut(antiforgeryToken: string): Promise<void> {
    const response = await fetch('/api/v1/session', {
      method: 'DELETE',
      credentials: 'same-origin',
      headers: {
        'X-Correlation-ID': crypto.randomUUID(),
        'X-CSRF-TOKEN': antiforgeryToken,
      },
    })
    if (!response.ok) throw toFailure(response.status, await readJson(response))
  },

  async getCurrentUser(): Promise<{ user: CurrentUser; etag: string }> {
    const response = await request('/api/v1/me', currentUserSchema)
    if (!response.etag) throw new ApiFailure('INVALID_API_RESPONSE', 200)
    return { user: response.data, etag: response.etag }
  },

  async listWorkspaces(): Promise<Workspace[]> {
    return (await request('/api/v1/workspaces', workspaceListSchema)).data
  },

  async getTenant(tenantId: string): Promise<Tenant> {
    return (await request(`/api/v1/tenants/${tenantId}`, tenantSchema)).data
  },

  async getFoundationCounts(tenantId: string) {
    const results = await Promise.all([
      visibleItemCount(
        `/api/v1/tenants/${tenantId}/client-accounts`,
        clientAccountPageSchema,
      ),
      visibleItemCount(`/api/v1/tenants/${tenantId}/agencies`, agencyPageSchema),
      visibleItemCount(`/api/v1/tenants/${tenantId}/contacts`, contactPageSchema),
    ])
    return { clientAccounts: results[0], agencies: results[1], contacts: results[2] }
  },

  async updateProfile(
    tenantId: string,
    update: ProfileUpdate,
    version: number,
    antiforgeryToken: string,
  ): Promise<CurrentUser> {
    return (await request(
      `/api/v1/tenants/${tenantId}/me`,
      currentUserSchema,
      { method: 'PUT', body: JSON.stringify({ ...update, phone: update.phone || null }) },
      {
        antiforgeryToken,
        expectedVersion: version,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },
}

async function visibleItemCount<T extends { items: unknown[] }>(
  path: string,
  schema: ZodType<T>,
): Promise<number | null> {
  try {
    return (await request(path, schema)).data.items.length
  } catch (error) {
    if (error instanceof ApiFailure && error.status === 403) return null
    throw error
  }
}

export function humanMessage(error: unknown): string {
  return error instanceof ApiFailure
    ? error.message
    : 'Something went wrong. Try again in a moment.'
}
