import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableText = z.string().nullable()

export const sessionSchema = z.object({
  authenticated: z.boolean(),
  antiforgeryToken: requiredText,
  expiresAtUtc: z.iso.datetime({ offset: true }).nullable(),
}).strict()

export const currentUserSchema = z.object({
  id: z.guid(),
  email: requiredText,
  displayName: requiredText,
  phone: nullableText,
  mfaEnabled: z.boolean(),
  version: z.number().int().positive(),
}).strict()

export const workspaceSchema = z.object({
  membershipId: z.guid(),
  tenantId: z.guid(),
  name: requiredText,
  slug: requiredText,
  roleCode: requiredText,
  version: z.number().int().positive(),
}).strict()

export const workspaceListSchema = z.array(workspaceSchema)

export const tenantSchema = z.object({
  id: z.guid(),
  typeCode: requiredText,
  legalName: requiredText,
  tradingName: requiredText,
  slug: requiredText,
  statusCode: requiredText,
  timeZone: requiredText,
  currencyCode: requiredText,
  vatStatusCode: requiredText,
  vatNumber: nullableText,
  settingsJson: requiredText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const clientAccountSchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  externalReference: requiredText,
  legalName: requiredText,
  tradingName: requiredText,
  website: nullableText,
  industry: nullableText,
  billingProfileJson: requiredText,
  primaryContactId: z.guid().nullable(),
  statusCode: requiredText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const agencySchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  externalReference: requiredText,
  legalName: requiredText,
  tradingName: requiredText,
  website: nullableText,
  statusCode: requiredText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const contactSchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  clientAccountId: z.guid(),
  name: requiredText,
  jobTitle: nullableText,
  email: requiredText,
  phone: nullableText,
  purposeCode: requiredText,
  consentBasis: requiredText,
  retainUntil: z.iso.date().nullable(),
  statusCode: requiredText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

const cursorPage = <T extends z.ZodType>(item: T) => z.object({
  items: z.array(item),
  nextCursor: z.string().nullable(),
}).strict()

export const clientAccountPageSchema = cursorPage(clientAccountSchema)
export const agencyPageSchema = cursorPage(agencySchema)
export const contactPageSchema = cursorPage(contactSchema)

export const problemSchema = z.object({
  type: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  status: z.number().int().nullable().optional(),
  detail: z.string().nullable().optional(),
  instance: z.string().nullable().optional(),
  code: z.string().nullable().optional(),
  correlationId: z.string().nullable().optional(),
  fieldErrors: z.record(z.string(), z.array(z.string())).nullable().optional(),
}).passthrough()

export const selectedWorkspaceStorageSchema = z.object({
  tenantId: z.guid(),
}).strict()

export const profileUpdateSchema = z.object({
  displayName: z.string().trim().min(2, 'Enter at least two characters.').max(120),
  phone: z.string().trim().max(30, 'Use 30 characters or fewer.'),
}).strict()

export type BrowserSession = z.infer<typeof sessionSchema>
export type CurrentUser = z.infer<typeof currentUserSchema>
export type Workspace = z.infer<typeof workspaceSchema>
export type Tenant = z.infer<typeof tenantSchema>
export type ProfileUpdate = z.infer<typeof profileUpdateSchema>
