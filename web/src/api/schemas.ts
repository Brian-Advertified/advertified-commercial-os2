import { z } from 'zod'

const requiredText = z.string().trim().min(1)
const nullableText = z.string().nullable()

const localBrowserPath = z.string().regex(/^\/(?!\/)/u)

export const sessionSchema = z.object({
  authenticated: z.boolean(),
  antiforgeryToken: requiredText,
  expiresAtUtc: z.iso.datetime({ offset: true }).nullable(),
  signInPath: localBrowserPath.nullable(),
  signOutPath: localBrowserPath.nullable(),
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

export const opportunitySchema = z.object({
  id: z.guid(),
  tenantId: z.guid(),
  clientId: z.guid(),
  title: requiredText,
  sourceType: requiredText,
  sourceRef: nullableText,
  ownerUserId: z.guid(),
  stage: requiredText,
  expectedValueMinor: z.number().int().nonnegative().nullable(),
  currency: nullableText,
  deadline: z.iso.date().nullable(),
  problemSummary: nullableText,
  objectiveSummary: nullableText,
  version: z.number().int().positive(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const evidenceSourceSchema = z.object({
  id: z.guid(), opportunityId: z.guid(), type: requiredText, locator: requiredText,
  title: requiredText, contentHash: requiredText, policyBasis: requiredText,
  captureStatus: requiredText, version: z.number().int().positive(),
  capturedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const evidenceItemSchema = z.object({
  id: z.guid(), sourceId: z.guid(), locator: requiredText, claimType: requiredText,
  originalValueJson: requiredText, reviewedValueJson: nullableText, excerpt: requiredText,
  confidence: z.number().min(0).max(1), reviewStatus: requiredText,
  decision: nullableText, reviewReason: nullableText, createdBy: z.guid(),
  reviewedBy: z.guid().nullable(), version: z.number().int().positive(),
}).strict()

export const evidenceSetSchema = z.object({
  id: z.guid(), opportunityId: z.guid(), versionNumber: z.number().int().positive(),
  evidenceItemIds: z.array(z.guid()), gaps: z.array(z.string()), status: requiredText,
  createdBy: z.guid(), approvedBy: z.guid().nullable(), version: z.number().int().positive(),
}).strict()

export const interpretationSchema = z.object({
  id: z.guid(), opportunityId: z.guid(), evidenceSetId: z.guid(),
  versionNumber: z.number().int().positive(), artifactJson: requiredText,
  evidenceBindingsJson: requiredText, unknownsJson: requiredText,
  assumptionsJson: requiredText, status: requiredText, createdBy: z.guid(),
  confirmedBy: z.guid().nullable(), version: z.number().int().positive(),
}).strict()

export const opportunityAngleSchema = z.object({
  id: z.guid(), angleSetId: z.guid(), rank: z.number().int().positive(),
  title: requiredText, rationale: requiredText, evidenceItemIdsJson: requiredText,
  confidence: z.number().min(0).max(1), status: requiredText,
  selectedBy: z.guid().nullable(), version: z.number().int().positive(),
}).strict()

export const criticObjectionSchema = z.object({
  id: z.guid(), severity: requiredText, fieldPath: requiredText, evidenceGap: requiredText,
  recommendedResolution: requiredText, resolution: nullableText,
  resolutionReason: nullableText, resolvedBy: z.guid().nullable(),
  version: z.number().int().positive(),
}).strict()

export const strategySchema = z.object({
  id: z.guid(), opportunityId: z.guid(), versionNumber: z.number().int().positive(),
  artifactJson: requiredText, evidenceBindingsJson: requiredText, unknownsJson: requiredText,
  assumptionsJson: requiredText, status: requiredText, createdBy: z.guid(),
  submittedBy: z.guid().nullable(), approvedBy: z.guid().nullable(),
  rejectedBy: z.guid().nullable(), rejectionReason: nullableText,
  version: z.number().int().positive(), objections: z.array(criticObjectionSchema),
}).strict()

export const agentRunSchema = z.object({
  id: z.guid(), opportunityId: z.guid(), runKind: requiredText, status: requiredText,
  currentStep: nullableText, attempts: z.number().int().nonnegative(), errorCode: nullableText,
  recoveryAction: nullableText, incrementalCostMinor: z.number().int().nonnegative(),
  version: z.number().int().positive(), updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const humanTaskSchema = z.object({
  id: z.guid(), opportunityId: z.guid().nullable(), briefId: z.guid().nullable(),
  taskType: requiredText, status: requiredText,
  title: requiredText, whyItMatters: requiredText, resourceType: requiredText,
  resourceId: z.guid(), resourceVersion: z.number().int().positive(),
  assigneeUserId: z.guid(), version: z.number().int().positive(),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const opportunityDetailSchema = z.object({
  opportunity: opportunitySchema,
  sources: z.array(evidenceSourceSchema),
  evidenceItems: z.array(evidenceItemSchema),
  evidenceSet: evidenceSetSchema.nullable(),
  interpretation: interpretationSchema.nullable(),
  angles: z.array(opportunityAngleSchema),
  strategy: strategySchema.nullable(),
  briefId: z.guid().nullable(),
  runs: z.array(agentRunSchema),
  nextAction: requiredText,
}).strict()

export const opportunityPageSchema = cursorPage(opportunitySchema)
export const humanTaskPageSchema = cursorPage(humanTaskSchema)

export const campaignBriefSummarySchema = z.object({
  id: z.guid(), tenantId: z.guid(), clientId: z.guid(), clientName: requiredText,
  opportunityId: z.guid().nullable(), title: requiredText, ownerUserId: z.guid(), status: requiredText,
  currentDraftVersionId: z.guid().nullable(), readyVersionId: z.guid().nullable(),
  approvedVersionId: z.guid().nullable(),
  version: z.number().int().positive(), updatedAtUtc: z.iso.datetime({ offset: true }),
}).strict()
export const campaignBriefSummaryListSchema = z.array(campaignBriefSummarySchema)

export const briefSourceSchema = z.object({
  id: z.guid(), sourceType: requiredText, locator: requiredText, title: requiredText,
  content: requiredText, contentHash: requiredText, createdBy: z.guid(),
  createdAtUtc: z.iso.datetime({ offset: true }),
}).strict()

export const briefUnknownSchema = z.object({
  fieldPath: requiredText, question: requiredText, isBlocking: z.boolean(),
}).strict()

export const briefAssumptionSchema = z.object({
  fieldPath: requiredText, value: requiredText, impact: requiredText,
  validationNeeded: requiredText,
}).strict()

export const briefConflictSchema = z.object({
  fieldPath: requiredText, description: requiredText, severity: requiredText,
  resolved: z.boolean(), resolution: nullableText,
}).strict()

export const briefSpatialRequirementSchema = z.object({
  id: z.guid(), type: requiredText, priority: requiredText, label: requiredText,
  geoJson: requiredText, radiusMetres: z.number().positive().nullable(),
  coverageThreshold: z.number().positive().max(1).nullable(),
  bufferInferred: z.boolean(), boundarySource: nullableText,
  boundaryVersion: nullableText, sourceLocator: requiredText, isVerified: z.boolean(),
}).strict()

export const briefVersionSchema = z.object({
  id: z.guid(), briefId: z.guid(), baseVersionId: z.guid().nullable(), sourceId: z.guid(),
  versionNumber: z.number().int().positive(), businessProblem: requiredText,
  objective: requiredText, audiences: z.array(z.string()), geographies: z.array(z.string()),
  timing: requiredText, budgetMinor: z.number().int().nonnegative().nullable(),
  budgetUnknown: z.boolean(), currency: nullableText, vatStatus: nullableText,
  feesMinor: z.number().int().nonnegative().nullable(), constraints: z.array(z.string()),
  measurement: z.array(z.string()), facts: z.array(z.string()),
  unknowns: z.array(briefUnknownSchema), assumptions: z.array(briefAssumptionSchema),
  conflicts: z.array(briefConflictSchema), evidenceItemIds: z.array(z.guid()),
  status: requiredText, createdBy: z.guid(), submittedBy: z.guid().nullable(),
  approvedBy: z.guid().nullable(), approvalMode: nullableText,
  rejectedBy: z.guid().nullable(),
  rejectionReason: nullableText, requestedChanges: nullableText,
  version: z.number().int().positive(), createdAtUtc: z.iso.datetime({ offset: true }),
  spatialRequirements: z.array(briefSpatialRequirementSchema),
}).strict()

export const campaignBriefSchema = z.object({
  brief: campaignBriefSummarySchema,
  sources: z.array(briefSourceSchema),
  versions: z.array(briefVersionSchema),
}).strict()

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
export type ClientAccount = z.infer<typeof clientAccountSchema>
export type Opportunity = z.infer<typeof opportunitySchema>
export type OpportunityDetail = z.infer<typeof opportunityDetailSchema>
export type HumanTask = z.infer<typeof humanTaskSchema>
export type Strategy = z.infer<typeof strategySchema>
export type AgentRun = z.infer<typeof agentRunSchema>
export type CampaignBriefSummary = z.infer<typeof campaignBriefSummarySchema>
export type CampaignBrief = z.infer<typeof campaignBriefSchema>
export type BriefVersion = z.infer<typeof briefVersionSchema>
