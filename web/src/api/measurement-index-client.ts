import { z } from 'zod'
import { request } from './client'

const summary = z.object({
  id: z.guid(), status: z.string().min(1),
  evidenceCount: z.number().int().nonnegative(),
  updatedAtUtc: z.iso.datetime({ offset: true }),
})
const campaignSummary = summary.extend({
  title: z.string().min(1), reportCount: z.number().int().nonnegative(),
}).strict()
const reportSummary = summary.extend({
  campaignId: z.guid(), campaignTitle: z.string().min(1),
  versionNumber: z.number().int().positive(),
}).strict()
const campaigns = z.object({
  items: z.array(campaignSummary), nextCursor: z.guid().nullable(),
}).strict()
const reports = z.object({
  items: z.array(reportSummary), nextCursor: z.guid().nullable(),
}).strict()

export type MeasurementCampaignSummary = z.infer<typeof campaignSummary>
export type MeasurementReportSummary = z.infer<typeof reportSummary>
export type IndexPage<T> = { items: T[]; nextCursor: string | null }

function query(cursor: string | null) {
  const parameters = new URLSearchParams({ pageSize: '50' })
  if (cursor) parameters.set('cursor', cursor)
  return parameters.toString()
}

export const measurementIndexApi = {
  async campaigns(tenantId: string, cursor: string | null) {
    return (await request(
      `/api/v1/tenants/${tenantId}/campaigns/measurement-summaries?${query(cursor)}`,
      campaigns,
    )).data
  },
  async reports(tenantId: string, cursor: string | null) {
    return (await request(
      `/api/v1/tenants/${tenantId}/measurement-reports?${query(cursor)}`, reports,
    )).data
  },
}
