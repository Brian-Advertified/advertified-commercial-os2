import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { MarketplaceSearchForm, PublishProductForm, RfqForm }
  from '../marketplace/MarketplaceForms'
import { MarketplaceListingInspector, MarketplaceListings, MarketplaceRequests }
  from '../marketplace/MarketplaceRecords'
import { marketplaceBuyerRoles, marketplaceSupplierRoles, marketplaceViewerRoles }
  from '../marketplace/marketplace-roles'
import { useMarketplaceData, useMarketplaceListingActions, useMarketplaceRfqActions,
  useMarketplaceRunner, type ResponseValues, type RfqValues }
  from '../marketplace/useMarketplaceWorkspace'

type MarketplaceTab = 'supply' | 'requests' | 'publish'

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
  const [tab, setTab] = useState<MarketplaceTab>('supply')
  const [selectedListing, setSelectedListing] = useState<MarketplaceListing | null>(null)
  const [rfqTarget, setRfqTarget] = useState<MarketplaceListing | null>(null)
  const [selectedRfqId, setSelectedRfqId] = useState<string | null>(null)
  const [responseTarget, setResponseTarget] = useState<MarketplaceRfq | null>(null)
  const data = useMarketplaceData(tenantId, canBuy, canSupply)
  const runner = useMarketplaceRunner(data.reload)
  const listings = useMarketplaceListingActions(tenantId, session?.antiforgeryToken, runner.run)
  const rfqs = useMarketplaceRfqActions(tenantId, session?.antiforgeryToken, runner.run)
  const error = runner.error ?? data.error

  if (error && !data.listings) return <MessageState title="Marketplace could not be loaded" message={error} />
  if (!data.listings) return <LoadingState label="Loading marketplace" />
  async function createRequest(values: RfqValues) {
    if (!rfqTarget) return
    await rfqs.create(rfqTarget, values)
    setRfqTarget(null); setSelectedListing(null); setTab('requests')
  }
  async function submitResponse(values: ResponseValues) {
    if (!responseTarget) return
    await rfqs.respond(responseTarget, values); setResponseTarget(null)
  }
  function selectRequest(rfq: MarketplaceRfq) {
    setSelectedRfqId(rfq.id); setResponseTarget(null)
  }
  return <MarketplaceExperience tenantId={tenantId} canBuy={canBuy} canSupply={canSupply}
    listings={data.listings} requests={data.requests} products={data.products} error={error}
    busy={runner.busy} tab={tab} setTab={setTab} selectedListing={selectedListing}
    setSelectedListing={setSelectedListing} rfqTarget={rfqTarget} setRfqTarget={setRfqTarget}
    selectedRfqId={selectedRfqId}
    selectRequest={selectRequest} responseTarget={responseTarget}
    prepareResponse={(rfq) => { setSelectedRfqId(rfq.id); setResponseTarget(rfq) }}
    closeResponse={() => setResponseTarget(null)} search={data.search}
    next={data.next} previous={data.previous} nextCursor={data.nextCursor}
    pageNumber={data.pageNumber}
    publish={listings.publish} archive={listings.archive} createRequest={createRequest}
    send={rfqs.send} accept={rfqs.accept} submitResponse={submitResponse} />
}

type ExperienceProps = {
  tenantId: string; canBuy: boolean; canSupply: boolean; busy: boolean; error: string | null
  listings: MarketplaceListing[]; requests: MarketplaceRfq[]
  products: ReturnType<typeof useMarketplaceData>['products']; tab: MarketplaceTab
  setTab: (tab: MarketplaceTab) => void; selectedListing: MarketplaceListing | null
  setSelectedListing: (listing: MarketplaceListing | null) => void
  rfqTarget: MarketplaceListing | null
  setRfqTarget: (listing: MarketplaceListing | null) => void
  selectedRfqId: string | null; selectRequest: (rfq: MarketplaceRfq) => void
  responseTarget: MarketplaceRfq | null; prepareResponse: (rfq: MarketplaceRfq) => void
  closeResponse: () => void; search: ReturnType<typeof useMarketplaceData>['search']
  next: () => Promise<void>; previous: () => Promise<void>
  nextCursor: string | null; pageNumber: number
  publish: (productId: string, terms: string) => Promise<void>
  archive: (listing: MarketplaceListing) => Promise<void>
  createRequest: (values: RfqValues) => Promise<void>; send: (rfq: MarketplaceRfq) => Promise<void>
  accept: (rfq: MarketplaceRfq) => Promise<void>
  submitResponse: (values: ResponseValues) => Promise<void>
}

function MarketplaceExperience(props: ExperienceProps) {
  const { listings, requests, canBuy, canSupply, tab, setTab } = props
  return <section className="marketplace-workbench" aria-labelledby="marketplace-title">
    <MarketplaceHeader tenantId={props.tenantId} listings={listings} requests={requests}
      canBuy={canBuy} canSupply={canSupply} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <MarketplaceTabs tab={tab} setTab={setTab} listingCount={listings.length}
      requestCount={requests.length} canExchange={canBuy || canSupply} canSupply={canSupply} />
    {tab === 'supply' && <SupplyPanel {...props} />}
    {tab === 'requests' && (canBuy || canSupply) && <MarketplaceRequests items={requests}
      tenantId={props.tenantId} busy={props.busy} selectedId={props.selectedRfqId}
      select={props.selectRequest} send={(rfq) => void props.send(rfq)}
      prepareResponse={props.prepareResponse} accept={(rfq) => void props.accept(rfq)}
      responseTarget={props.responseTarget} closeResponse={props.closeResponse}
      submitResponse={props.submitResponse} />}
    {tab === 'publish' && canSupply && <PublishProductForm products={props.products}
      busy={props.busy} publish={props.publish} />}
  </section>
}

function SupplyPanel(props: ExperienceProps) {
  const { listings, selectedListing, rfqTarget } = props
  return <div id="marketplace-supply-panel" aria-label="Published supply">
    <MarketplaceSearchForm search={(filters) => { props.setSelectedListing(null)
      props.setRfqTarget(null); void props.search(filters) }} />
    <div className={selectedListing || rfqTarget ? 'marketplace-master-detail' : undefined}>
      <MarketplaceListings listings={listings} tenantId={props.tenantId}
        canSupply={props.canSupply} selectedId={selectedListing?.id ?? null}
        open={(listing) => { props.setRfqTarget(null); props.setSelectedListing(listing) }}
        archive={(listing) => void props.archive(listing)} />
      {rfqTarget && <RfqForm listing={rfqTarget} busy={props.busy}
        close={() => props.setRfqTarget(null)} create={props.createRequest} />}
      {!rfqTarget && selectedListing && <MarketplaceListingInspector listing={selectedListing}
        tenantId={props.tenantId} canBuy={props.canBuy}
        close={() => props.setSelectedListing(null)}
        request={(listing) => props.setRfqTarget(listing)} />}
    </div>
    <nav className="marketplace-pagination" aria-label="Marketplace pages">
      <button className="secondary-button" type="button" disabled={props.pageNumber === 1}
        onClick={() => void props.previous()}>Previous</button>
      <span>Page {props.pageNumber}</span>
      <button className="secondary-button" type="button" disabled={!props.nextCursor}
        onClick={() => void props.next()}>Next</button>
    </nav>
  </div>
}

function MarketplaceHeader({ tenantId, listings, requests, canBuy, canSupply }: {
  tenantId: string; listings: MarketplaceListing[]; requests: MarketplaceRfq[]
  canBuy: boolean; canSupply: boolean
}) {
  const markedAvailable = listings.filter(item => item.currentVersion?.availability ===
    masterDataCodes.availabilityStatuses.available).length
  const needsAction = requests.filter(rfq => requestNeedsAction(rfq, tenantId)).length
  return <><header className="marketplace-command-header"><span className="marketplace-command-icon">
    <Icon name="marketplace" /></span><div><p className="eyebrow">Published media supply</p>
      <h1 id="marketplace-title">Marketplace</h1>
      <p>Find supplier-reviewed media currently published for campaign buying. Canonical records and source evidence remain in Inventory. Acceptance never creates a booking.</p>
    </div><div className="marketplace-access"><span>Workspace access</span>
      <strong>{accessLabel(canBuy, canSupply)}</strong></div></header>
    <dl className="marketplace-metric-strip">
      <Metric label="Shown on page" value={listings.length} detail="Current filtered page" />
      <Metric label="Available on page" value={markedAvailable} detail="Current supplier status" />
      <Metric label="Exchange records" value={canBuy || canSupply ? requests.length : '—'}
        detail={canBuy || canSupply ? 'Retained for this workspace' : 'View-only role'} />
      <Metric label="Needs your action" value={canBuy || canSupply ? needsAction : '—'}
        detail={canBuy || canSupply ? 'Based on role and status' : 'No RFQ permission'} />
    </dl></>
}

function MarketplaceTabs({ tab, setTab, listingCount, requestCount, canExchange, canSupply }: {
  tab: MarketplaceTab; setTab: (tab: MarketplaceTab) => void
  listingCount: number; requestCount: number; canExchange: boolean; canSupply: boolean
}) {
  const tabs: Array<{ id: MarketplaceTab; label: string; count?: number }> = [
    { id: 'supply', label: 'Published supply', count: listingCount },
    ...(canExchange ? [{ id: 'requests' as const, label: 'Requests', count: requestCount }] : []),
    ...(canSupply ? [{ id: 'publish' as const, label: 'Publish supply' }] : []),
  ]
  return <div className="marketplace-tabs" role="group" aria-label="Marketplace views">
    {tabs.map(item => <button key={item.id} type="button"
      aria-pressed={tab === item.id} onClick={() => setTab(item.id)}>
      {item.label}{item.count !== undefined && <span>{item.count}</span>}</button>)}
  </div>
}

function Metric({ label, value, detail }: { label: string; value: string | number; detail: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{detail}</small></div>
}

function requestNeedsAction(rfq: MarketplaceRfq, tenantId: string) {
  const buyer = rfq.buyerTenantId === tenantId
  return (buyer && (rfq.status === masterDataCodes.marketplaceRfqStatuses.draft ||
    rfq.status === masterDataCodes.marketplaceRfqStatuses.responded)) ||
    (!buyer && rfq.status === masterDataCodes.marketplaceRfqStatuses.sent)
}

function accessLabel(canBuy: boolean, canSupply: boolean) {
  if (canBuy && canSupply) return 'Buyer and supplier'
  if (canBuy) return 'Buyer'
  if (canSupply) return 'Supplier'
  return 'View only'
}
