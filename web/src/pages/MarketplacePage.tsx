import { useState, type Dispatch, type SetStateAction } from 'react'
import { Navigate } from 'react-router-dom'
import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { MarketplaceSearchForm, PublishProductForm, ResponseForm, RfqForm }
  from '../marketplace/MarketplaceForms'
import { MarketplaceListings, MarketplaceRequests } from '../marketplace/MarketplaceRecords'
import { marketplaceBuyerRoles, marketplaceSupplierRoles, marketplaceViewerRoles }
  from '../marketplace/marketplace-roles'
import { useMarketplaceData, useMarketplaceListingActions, useMarketplaceRfqActions,
  useMarketplaceRunner } from '../marketplace/useMarketplaceWorkspace'

export function MarketplacePage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!marketplaceViewerRoles.has(selected.roleCode)) return <MessageState
    title="Marketplace is not available"
    message="This workspace role cannot view marketplace supply." />
  return <MarketplaceWorkspace key={selected.tenantId} tenantId={selected.tenantId}
    canBuy={marketplaceBuyerRoles.has(selected.roleCode)}
    canSupply={marketplaceSupplierRoles.has(selected.roleCode)} />
}

function MarketplaceWorkspace({ tenantId, canBuy, canSupply }: {
  tenantId: string; canBuy: boolean; canSupply: boolean
}) {
  const { session } = useSession()
  const [selectedListing, setSelectedListing] = useState<MarketplaceListing | null>(null)
  const [selectedRfq, setSelectedRfq] = useState<MarketplaceRfq | null>(null)
  const data = useMarketplaceData(tenantId, canBuy, canSupply)
  const runner = useMarketplaceRunner(() => data.load())
  const listings = useMarketplaceListingActions(
    tenantId, session?.antiforgeryToken, runner.run)
  const rfqs = useMarketplaceRfqActions(tenantId, session?.antiforgeryToken, runner.run)
  const error = runner.error ?? data.error

  if (error && !data.listings) return <MessageState title="Marketplace could not be loaded" message={error} />
  if (!data.listings) return <LoadingState label="Loading marketplace" />
  return <MarketplaceExperience tenantId={tenantId} canBuy={canBuy} canSupply={canSupply}
    data={data} runner={runner} listingActions={listings} rfqActions={rfqs}
    selectedListing={selectedListing} setSelectedListing={setSelectedListing}
    selectedRfq={selectedRfq} setSelectedRfq={setSelectedRfq} error={error} />
}

function MarketplaceExperience({ tenantId, canBuy, canSupply, data, runner, listingActions,
  rfqActions, selectedListing, setSelectedListing, selectedRfq, setSelectedRfq, error }: {
  tenantId: string; canBuy: boolean; canSupply: boolean
  data: ReturnType<typeof useMarketplaceData>; runner: ReturnType<typeof useMarketplaceRunner>
  listingActions: ReturnType<typeof useMarketplaceListingActions>
  rfqActions: ReturnType<typeof useMarketplaceRfqActions>
  selectedListing: MarketplaceListing | null
  setSelectedListing: Dispatch<SetStateAction<MarketplaceListing | null>>
  selectedRfq: MarketplaceRfq | null
  setSelectedRfq: Dispatch<SetStateAction<MarketplaceRfq | null>>
  error: string | null
}) {
  const published = data.listings ?? []
  return <section aria-labelledby="marketplace-title">
    <header className="page-heading page-heading-split"><div><p className="eyebrow">
      Current reviewed media supply</p><h1 id="marketplace-title">Supplier marketplace</h1>
      <p>Request and accept exact supplier responses. Acceptance never creates a booking.</p></div>
      <span className="status-chip">{published.length} listings</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <MarketplaceSearchForm search={(filters) => void data.load(filters)} />
    {canSupply && <PublishProductForm products={data.products} busy={runner.busy}
      publish={listingActions.publish} />}
    {selectedListing && <RfqForm listing={selectedListing} busy={runner.busy}
      close={() => setSelectedListing(null)} create={async (values) => {
        await rfqActions.create(selectedListing, values); setSelectedListing(null) }} />}
    {selectedRfq && <ResponseForm rfq={selectedRfq} busy={runner.busy}
      close={() => setSelectedRfq(null)} respond={async (values) => {
        await rfqActions.respond(selectedRfq, values); setSelectedRfq(null) }} />}
    <MarketplaceListings listings={published} tenantId={tenantId} canBuy={canBuy}
      canSupply={canSupply} request={setSelectedListing}
      archive={(listing) => void listingActions.archive(listing)} />
    {(canBuy || canSupply) && <MarketplaceRequests items={data.requests} tenantId={tenantId}
      busy={runner.busy} send={(rfq) => void rfqActions.send(rfq)} respond={setSelectedRfq}
      accept={(rfq) => void rfqActions.accept(rfq)} />}
  </section>
}
