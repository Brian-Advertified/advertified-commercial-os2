import type { ZodType } from 'zod'
import { request } from './client'
import { inventoryCodes, type InventoryDecision } from './inventory-constants'
import {
  inventorySemanticPreflightSchema,
  type InventorySemanticPreflight,
} from './inventory-semantic-preflight-schemas'
import {
  inventoryBenchmarkSchema,
  inventoryAssetRightsReviewSchema,
  inventoryAssetSchema,
  inventoryAvailabilityExceptionSchema,
  inventoryDuplicateCandidateSchema,
  inventoryDuplicateCandidatesSchema,
  inventoryEmbeddingSchema,
  inventoryCandidateSchema,
  inventoryImportSchema,
  inventoryProductPageSchema,
  inventoryProductSchema,
  inventorySemanticRecallsSchema,
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

  async semanticPreflight(
    tenantId: string,
    importId?: string,
  ): Promise<InventorySemanticPreflight> {
    const query = new URLSearchParams()
    if (importId) query.set('importId', importId)
    const suffix = query.size > 0 ? `?${query}` : ''
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-semantic-preflight${suffix}`,
      inventorySemanticPreflightSchema,
    )).data
  },

  async uploadAsset(
    tenantId: string,
    productId: string,
    productVersionId: string,
    productVersion: number,
    assetType: string,
    source: File,
    token: string,
  ) {
    const body = new FormData()
    body.set('productVersionId', productVersionId)
    body.set('assetType', assetType)
    body.set('source', source)
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/assets`,
      inventoryAssetSchema, { method: 'POST', body }, {
        antiforgeryToken: token, expectedVersion: productVersion,
        idempotencyKey: crypto.randomUUID(),
      })).data
  },

  execute(tenantId: string, record: InventoryImport, token: string) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:execute`,
      inventoryImportSchema, {}, token, record.version)
  },

  retryExtraction(tenantId: string, record: InventoryImport, token: string, reason: string) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:retry-extraction`,
      inventoryImportSchema, { reason }, token, record.version)
  },

  reprojectExtraction(
    tenantId: string, record: InventoryImport,
    token: string, reason: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:reproject-extraction`,
      inventoryImportSchema, { reason }, token, record.version)
  },

  cancelExtraction(tenantId: string, record: InventoryImport, token: string, reason: string) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:cancel-extraction`,
      inventoryImportSchema, { reason }, token, record.version)
  },

  reconcileExtraction(
    tenantId: string, record: InventoryImport, token: string,
    reason: string, externalTaskId: string | null,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-imports/${record.id}:reconcile-extraction`,
      inventoryImportSchema, { reason, externalTaskId }, token, record.version)
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

  reviewAssetRights(
    tenantId: string,
    assetId: string,
    version: number,
    input: { rightsStatus: string; rightsBasis: string | null; licensedUntil: string | null
      scopeCodes: string[]; territoryCode: string; effectiveOn: string | null
      untilRevoked: boolean; attestorRole: string; evidenceReference: string
      evidenceHash: string },
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-assets/${assetId}:review-rights`,
      inventoryAssetRightsReviewSchema, input, token, version)
  },

  recordAvailabilityException(
    tenantId: string,
    productId: string,
    version: number,
    input: { productVersionId: string; exceptionType: string; startsOn: string
      endsOn: string; sourceLocator: string; evidenceHash: string },
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/availability-exceptions`,
      inventoryAvailabilityExceptionSchema, input, token, version)
  },

  async semanticRecall(tenantId: string, productId: string) {
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/semantic-recall?limit=10`,
      inventorySemanticRecallsSchema,
    )).data
  },

  submitEmbedding(
    tenantId: string,
    productId: string,
    productVersionId: string,
    productVersion: number,
    forceBackfill: boolean,
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/embedding`,
      inventoryEmbeddingSchema, { productVersionId, forceBackfill },
      token, productVersion)
  },

  nominateSemanticDuplicate(
    tenantId: string,
    productId: string,
    version: number,
    input: {
      productVersionId: string
      peerProductId: string
      peerProductVersionId: string
      reason: string
    },
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-products/${productId}/semantic-duplicate-candidates`,
      inventoryDuplicateCandidateSchema, input, token, version)
  },

  async listDuplicateCandidates(
    tenantId: string,
    status = inventoryCodes.duplicateStatus.open,
  ) {
    return (await request(
      `/api/v1/tenants/${tenantId}/inventory-duplicate-candidates?status=${encodeURIComponent(status)}`,
      inventoryDuplicateCandidatesSchema,
    )).data
  },

  reviewDuplicate(
    tenantId: string,
    candidateId: string,
    version: number,
    input: { decision: string; canonicalProductId: string | null; reason: string },
    token: string,
  ) {
    return command(
      `/api/v1/tenants/${tenantId}/inventory-duplicate-candidates/${candidateId}:review`,
      inventoryDuplicateCandidateSchema, input, token, version)
  },
}
