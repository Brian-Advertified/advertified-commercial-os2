import { z } from 'zod'
import { request } from './client'

const decisionSchema = z.object({
  eventId: z.guid(), previousEventId: z.guid().nullable(), productId: z.guid(),
  productVersionId: z.guid(), previousProductVersionId: z.guid().nullable(),
  productName: z.string(), isSelected: z.boolean(), wasSelected: z.boolean().nullable(),
  presentInCurrentShortlist: z.boolean(),
  decidedAtUtc: z.iso.datetime({ offset: true }), actorId: z.guid().nullable(),
  reason: z.string().nullable(), briefVersionId: z.guid().nullable(),
  shortlistVersionId: z.guid().nullable(),
}).strict()

const reportSchema = z.object({
  items: z.array(decisionSchema), hasMore: z.boolean(), supplierSafe: z.boolean(),
  nextCursor: z.string().nullable(),
}).strict()

export type InventoryDecision = z.infer<typeof decisionSchema>
export type InventoryDecisionReport = z.infer<typeof reportSchema>

export async function getInventoryDecisions(tenantId: string, scope: {
  briefVersionId?: string; inventoryProductId?: string; cursor?: string
}): Promise<InventoryDecisionReport> {
  const query = new URLSearchParams()
  if (scope.briefVersionId) query.set('briefVersionId', scope.briefVersionId)
  if (scope.inventoryProductId) query.set('inventoryProductId', scope.inventoryProductId)
  if (scope.cursor) query.set('cursor', scope.cursor)
  return (await request(`/api/v1/tenants/${tenantId}/reporting/inventory-decisions?${query}`, reportSchema)).data
}
