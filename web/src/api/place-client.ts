import { z } from 'zod'
import { request } from './client'

export const inventoryPlaceSchema = z.object({
  id: z.string().uuid(), name: z.string().min(1), category: z.string().nullable(),
  context: z.string(), latitude: z.number().min(-90).max(90),
  longitude: z.number().min(-180).max(180), productVersionId: z.string().uuid(),
  sourceImportId: z.string().uuid(), sourceLocator: z.string().min(1),
})
export type InventoryPlace = z.infer<typeof inventoryPlaceSchema>

export async function searchInventoryPlaces(tenantId: string, search: string) {
  const query = new URLSearchParams({ search })
  return (await request(`/api/v1/tenants/${tenantId}/inventory-places?${query}`,
    z.array(inventoryPlaceSchema))).data
}

export function inventoryPlaceSource(place: InventoryPlace) {
  return `inventory:product-version:${place.productVersionId}:poi:${place.id}; import:${place.sourceImportId}; ${place.sourceLocator}`
}

export const discoveredPlaceSchema = z.object({
  id: z.string().min(1), name: z.string().min(1), address: z.string().min(1),
  latitude: z.number().min(-90).max(90), longitude: z.number().min(-180).max(180),
  sourceLocator: z.url(), attribution: z.string().min(1), retrievedAtUtc: z.string().datetime({ offset: true }),
  geometryBasis: z.string().min(1),
})
export type DiscoveredPlace = z.infer<typeof discoveredPlaceSchema>
export async function discoverPlaces(tenantId: string, search: string) {
  return (await request(`/api/v1/tenants/${tenantId}/place-discovery?${new URLSearchParams({ search })}`,
    z.object({ available: z.boolean(), places: z.array(discoveredPlaceSchema) }))).data
}
