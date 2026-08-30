import type { ZodType } from 'zod'
import { request } from './client'
import { inventoryCodes, type InventoryDecision } from './inventory-constants'
import {
  inventoryBenchmarkSchema,
  inventoryCandidateSchema,
  inventoryImportSchema,
  inventoryProductPageSchema,
  inventoryProductSchema,
  type InventoryImport,
  type InventoryProductPage,
  type InventoryValues,
} from './inventory-schemas'

async function command<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  version: number,
): Promise<T> {
  return (await request(path, schema,
    { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, expectedVersion: version,
      idempotencyKey: crypto.randomUUID() })).data
}

export const inventoryApi = {
  async upload(
    tenantId: string,
    supplierName: string,
    source: File,
    token: string,
  ): Promise<InventoryImport> {
    const body = new FormData()
    body.set('supplierName', supplierName)
    body.set('source', source)
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-imports`, inventoryImportSchema,
      { method: 'POST', body },
      { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() },
    )).data
  },

  async getImport(
    tenantId: string,
    importId: string,
    cursor?: string,
  ): Promise<InventoryImport> {
    const query = new URLSearchParams({ pageSize: '100' })
    if (cursor) query.set('cursor', cursor)
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-imports/${importId}?${query}`,
      inventoryImportSchema,
    )).data
  },

  execute(tenantId: string, record: InventoryImport, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:execute`,
      inventoryImportSchema, {}, token, record.version)
  },

  review(
    tenantId: string,
    candidateId: string,
    candidateVersion: number,
    token: string,
    decision: InventoryDecision,
    correctedValues: InventoryValues | null,
    rejectionReason: string | null,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-candidates/${candidateId}:review`,
      inventoryCandidateSchema,
      { decision, correctedValues, rejectionReason,
        notes: decision === inventoryCodes.decision.reject
          ? 'Rejected during source review.' : 'Source checked.' },
      token, candidateVersion)
  },

  publish(tenantId: string, record: InventoryImport, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:publish`,
      inventoryImportSchema, {}, token, record.version)
  },

  async search(
    tenantId: string,
    filters: { search?: string; channel?: string; geography?: string; cursor?: string },
  ): Promise<InventoryProductPage> {
    const query = new URLSearchParams()
    if (filters.search) query.set('search', filters.search)
    if (filters.channel) query.set('channel', filters.channel)
    if (filters.geography) query.set('geography', filters.geography)
    if (filters.cursor) query.set('cursor', filters.cursor)
    query.set('pageSize', '24')
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-products?${query}`,
      inventoryProductPageSchema,
    )).data
  },

  async getProduct(tenantId: string, productId: string) {
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}`,
      inventoryProductSchema,
    )).data
  },

  async getBenchmark(tenantId: string, productId: string) {
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/benchmark`,
      inventoryBenchmarkSchema,
    )).data
  },
}
