import { z } from 'zod'
import { request } from './client'

const metricSchema = z.object({
  opportunities: z.number().int().nonnegative(), briefs: z.number().int().nonnegative(),
  approvedAudiences: z.number().int().nonnegative(), pendingApprovals: z.number().int().nonnegative(),
  approvedPlans: z.number().int().nonnegative(), inventoryRecommendations: z.number().int().nonnegative(),
  selectedInventory: z.number().int().nonnegative(), publishedListings: z.number().int().nonnegative(),
  proposals: z.number().int().nonnegative(), bookings: z.number().int().nonnegative(),
  campaigns: z.number().int().nonnegative(), deliveryEvidenceItems: z.number().int().nonnegative(),
  measurementReports: z.number().int().nonnegative(),
}).strict()

const channelSpendSchema = z.object({
  channel: z.string(), planCount: z.number().int().nonnegative(),
  supplierCostMinor: z.number().int().nonnegative(), clientPriceMinor: z.number().int().nonnegative(),
  feesMinor: z.number().int().nonnegative(), vatMinor: z.number().int().nonnegative(),
  currency: z.string(),
}).strict()

const commercialSchema = z.object({
  bookingCount: z.number().int().nonnegative(), supplierCostMinor: z.number().int().nonnegative(),
  markupMinor: z.number().int().nonnegative(), commissionMinor: z.number().int().nonnegative(),
  managementFeeMinor: z.number().int().nonnegative(), feesMinor: z.number().int().nonnegative(),
  vatMinor: z.number().int().nonnegative(), clientTotalMinor: z.number().int().nonnegative(),
  currency: z.string(),
}).strict()

const dimensionSchema = z.object({ id: z.guid(), label: z.string().min(1) }).strict()

export const operationalReportSchema = z.object({
  generatedAtUtc: z.iso.datetime({ offset: true }), metrics: metricSchema,
  channelSpend: z.array(channelSpendSchema), commercialTotals: commercialSchema,
  statuses: z.array(z.object({ area: z.string(), status: z.string(),
    count: z.number().int().nonnegative(), oldestAgeDays: z.number().int().nonnegative() }).strict()),
  exceptions: z.array(z.object({ code: z.string(), label: z.string(),
    count: z.number().int().nonnegative() }).strict()),
  dimensions: z.object({ clients: z.array(dimensionSchema), campaigns: z.array(dimensionSchema),
    suppliers: z.array(dimensionSchema), users: z.array(dimensionSchema) }).strict(),
}).strict()

export type OperationalReport = z.infer<typeof operationalReportSchema>
export type ReportingFilters = {
  from?: string; to?: string; clientAccountId?: string; campaignId?: string
  channel?: string; supplierId?: string; status?: string
  ownerUserId?: string; reviewerUserId?: string
}

export const reportingApi = {
  async get(tenantId: string, filters: ReportingFilters): Promise<OperationalReport> {
    const query = new URLSearchParams()
    Object.entries(filters).forEach(([key, value]) => { if (value) query.set(key, value) })
    return (await request(
      `/api/v1/tenants/${tenantId}/reporting/operations?${query}`,
      operationalReportSchema,
    )).data
  },
}
