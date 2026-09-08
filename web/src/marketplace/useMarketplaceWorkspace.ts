import { useCallback, useEffect, useState } from 'react'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProductSummary } from '../api/inventory-schemas'
import { marketplaceApi, type MarketplaceFilters } from '../api/marketplace-client'
import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { notifications } from '../notifications/notifications'

export type RfqValues = { subject: string; requestedStart: string; requestedEnd: string;
  quantity: number; dueAtUtc: string }
export type ResponseValues = { amountMinor: number; currency: string; availability: string;
  terms: string; validUntilUtc: string; evidenceReferences: string[] }

export function useMarketplaceData(
  tenantId: string, canBuy: boolean, canSupply: boolean,
) {
  const [listings, setListings] = useState<MarketplaceListing[] | null>(null)
  const [requests, setRequests] = useState<MarketplaceRfq[]>([])
  const [products, setProducts] = useState<InventoryProductSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<MarketplaceFilters>({})
  const [cursor, setCursor] = useState<string | undefined>()
  const [cursorHistory, setCursorHistory] = useState<Array<string | undefined>>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const loadPage = useCallback(async (
    requestedFilters: MarketplaceFilters, requestedCursor?: string,
  ) => {
    try {
      const [listingPage, rfqPage, inventoryPage] = await fetchWorkspaceData(
        tenantId, canBuy, canSupply, requestedFilters, requestedCursor)
      setListings(listingPage.items)
      setNextCursor(listingPage.nextCursor)
      setRequests(rfqPage?.items ?? [])
      setProducts(inventoryPage?.items ?? [])
      setError(null)
    } catch (failure) { setError(humanMessage(failure)) }
  }, [canBuy, canSupply, tenantId])
  const search = useCallback(async (requestedFilters: MarketplaceFilters) => {
    setFilters(requestedFilters); setCursor(undefined); setCursorHistory([])
    await loadPage(requestedFilters)
  }, [loadPage])
  const next = useCallback(async () => {
    if (!nextCursor) return
    setCursorHistory(previous => [...previous, cursor])
    setCursor(nextCursor)
    await loadPage(filters, nextCursor)
  }, [cursor, filters, loadPage, nextCursor])
  const previous = useCallback(async () => {
    if (cursorHistory.length === 0) return
    const prior = cursorHistory[cursorHistory.length - 1]
    setCursorHistory(previousCursors => previousCursors.slice(0, -1))
    setCursor(prior)
    await loadPage(filters, prior)
  }, [cursorHistory, filters, loadPage])
  const reload = useCallback(() => loadPage(filters, cursor), [cursor, filters, loadPage])
  useEffect(() => {
    let active = true
    void fetchWorkspaceData(tenantId, canBuy, canSupply, {}, undefined).then(
      ([listingPage, rfqPage, inventoryPage]) => {
        if (!active) return
        setListings(listingPage.items); setRequests(rfqPage?.items ?? [])
        setProducts(inventoryPage?.items ?? []); setNextCursor(listingPage.nextCursor)
        setError(null)
      },
      (failure: unknown) => { if (active) setError(humanMessage(failure)) },
    )
    return () => { active = false }
  }, [canBuy, canSupply, tenantId])
  return { listings, requests, products, error, search, next, previous, reload,
    nextCursor, pageNumber: cursorHistory.length + 1 }
}

function fetchWorkspaceData(
  tenantId: string, canBuy: boolean, canSupply: boolean,
  filters: MarketplaceFilters, cursor?: string,
) {
  return Promise.all([
    marketplaceApi.search(tenantId, filters, cursor),
    canBuy || canSupply ? marketplaceApi.listRfqs(tenantId) : Promise.resolve(null),
    canSupply ? inventoryApi.search(tenantId, {}) : Promise.resolve(null),
  ])
}

export function useMarketplaceRunner(load: () => Promise<void>) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const run = useCallback(async (action: () => Promise<void>) => {
    setBusy(true); setError(null)
    try { await action(); await load() }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }, [load])
  return { busy, error, run }
}

export function useMarketplaceListingActions(
  tenantId: string, token: string | undefined,
  run: (action: () => Promise<void>) => Promise<void>,
) {
  async function publish(productId: string, terms: string) {
    if (!token) return
    await run(async () => {
      const draft = await marketplaceApi.createListing(tenantId, productId, terms, token)
      await marketplaceApi.publishListing(tenantId, draft, token)
      notifications.success('The reviewed inventory projection is now published.')
    })
  }
  async function archive(listing: MarketplaceListing) {
    if (!token) return
    await run(async () => {
      await marketplaceApi.archiveListing(tenantId, listing, token)
      notifications.success('The listing is no longer visible to buyers.')
    })
  }
  return { publish, archive }
}

export function useMarketplaceRfqActions(
  tenantId: string, token: string | undefined,
  run: (action: () => Promise<void>) => Promise<void>,
) {
  async function create(listing: MarketplaceListing, values: RfqValues) {
    if (!token || !listing.currentVersion) return
    await run(async () => {
      await marketplaceApi.createRfq(tenantId,
        { ...values, listingVersionId: listing.currentVersion!.id }, token)
      notifications.success('Draft request created. Review it before sending.')
    })
  }
  async function send(rfq: MarketplaceRfq) {
    if (!token) return
    await run(async () => {
      await marketplaceApi.sendRfq(tenantId, rfq, token)
      notifications.success('The request is ready in the supplier workspace.')
    })
  }
  async function respond(rfq: MarketplaceRfq, values: ResponseValues) {
    if (!token) return
    await run(async () => {
      await marketplaceApi.respond(tenantId, rfq.id, values, token)
      notifications.success('The immutable supplier response was submitted.')
    })
  }
  async function accept(rfq: MarketplaceRfq) {
    if (!token) return
    await run(async () => {
      await marketplaceApi.accept(tenantId, rfq, token)
      notifications.success('The exact response was accepted. No booking was created.')
    })
  }
  return { create, send, respond, accept }
}
