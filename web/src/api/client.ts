import type { ZodType } from 'zod'
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
  APPROVAL_REQUIRED: 'This action belongs to a different assigned reviewer or approver.',
  EVIDENCE_REQUIRED: 'Complete and approve the required evidence first.',
  CSRF_VALIDATION_FAILED: 'Refresh the page and try that action again.',
  IDEMPOTENCY_CONFLICT: 'That request key was already used. Try the action again.',
  ORIGIN_NOT_ALLOWED: 'Open Advertified from its configured local address.',
  TENANT_FORBIDDEN: 'You do not have access to this workspace or action.',
  VALIDATION_FAILED: 'Review the information and try again.',
  VERSION_CONFLICT: 'This information changed. Refresh it before saving again.',
  INVALID_LIFECYCLE_TRANSITION: 'Complete the current opportunity step first.',
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
  if (init.body) headers.set('Content-Type', 'application/json')
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
