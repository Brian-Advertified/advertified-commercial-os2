import type { ZodType } from 'zod'
import { request } from './client'
import {
  marketplaceListingPageSchema,
  marketplaceListingSchema,
  marketplaceRfqPageSchema,
  marketplaceRfqSchema,
  type MarketplaceListing,
  type MarketplaceListingPage,
  type MarketplaceRfq,
  type MarketplaceRfqPage,
} from './marketplace-schemas'

async function command<T>(
  path: string,
  schema: ZodType<T>,
  body: unknown,
  token: string,
  expectedVersion?: number,
): Promise<T> {
  return (await request(
    path, schema, { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, expectedVersion, idempotencyKey: crypto.randomUUID() },
  )).data
}

const tenantPath = (tenantId: string, suffix: string) =>
  `/api/v1/tenants/${tenantId}/${suffix}`

export const marketplaceApi = {
  async search(
    tenantId: string,
    filters: { search?: string; channel?: string; geography?: string },
  ): Promise<MarketplaceListingPage> {
    const query = new URLSearchParams({ pageSize: '24' })
    if (filters.search) query.set('search', filters.search)
    if (filters.channel) query.set('channel', filters.channel)
    if (filters.geography) query.set('geography', filters.geography)
    return (await request(
      `${tenantPath(tenantId, 'marketplace-listings')}?${query}`,
      marketplaceListingPageSchema,
    )).data
  },

  async listRfqs(tenantId: string): Promise<MarketplaceRfqPage> {
    return (await request(
      `${tenantPath(tenantId, 'marketplace-rfqs')}?pageSize=50`,
      marketplaceRfqPageSchema,
    )).data
  },

  createListing(
    tenantId: string, productId: string, terms: string, token: string,
  ): Promise<MarketplaceListing> {
    return command(tenantPath(tenantId, 'marketplace-listings'), marketplaceListingSchema,
      { productId, terms }, token)
  },

  publishListing(
    tenantId: string, listing: MarketplaceListing, token: string,
  ): Promise<MarketplaceListing> {
    return command(tenantPath(tenantId, `marketplace-listings/${listing.id}:publish`),
      marketplaceListingSchema, {}, token, listing.version)
  },

  archiveListing(
    tenantId: string, listing: MarketplaceListing, token: string,
  ): Promise<MarketplaceListing> {
    return command(tenantPath(tenantId, `marketplace-listings/${listing.id}:archive`),
      marketplaceListingSchema, { reason: 'Supplier withdrew this listing.' },
      token, listing.version)
  },

  createRfq(
    tenantId: string,
    values: { listingVersionId: string; subject: string; requestedStart: string;
      requestedEnd: string; quantity: number; dueAtUtc: string },
    token: string,
  ): Promise<MarketplaceRfq> {
    return command(tenantPath(tenantId, 'marketplace-rfqs'), marketplaceRfqSchema,
      values, token)
  },

  sendRfq(tenantId: string, rfq: MarketplaceRfq, token: string) {
    return command(tenantPath(tenantId, `marketplace-rfqs/${rfq.id}:send`),
      marketplaceRfqSchema, { reason: 'Buyer approved this request for supplier review.' },
      token, rfq.version)
  },

  respond(
    tenantId: string, rfqId: string,
    values: { amountMinor: number; currency: string; availability: string;
      terms: string; validUntilUtc: string; evidenceReferences: string[] },
    token: string,
  ) {
    return command(tenantPath(tenantId, `marketplace-rfqs/${rfqId}/responses`),
      marketplaceRfqSchema, values, token)
  },

  accept(tenantId: string, rfq: MarketplaceRfq, token: string) {
    if (!rfq.response) throw new Error('A supplier response is required.')
    return command(tenantPath(
      tenantId, `marketplace-responses/${rfq.response.id}:accept`),
    marketplaceRfqSchema, { reason: 'Buyer accepted this exact supplier response.' },
    token, rfq.response.responseVersion)
  },
}
