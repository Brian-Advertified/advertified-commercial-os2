import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { formatDate, formatDateTime, formatMoney, humanizeCode } from '../presentation/format'
import { ResponseForm } from './MarketplaceForms'
import { mediaLabel } from '../presentation/media-labels'
import type { ResponseValues } from './useMarketplaceWorkspace'

type ListingsProps = {
  listings: MarketplaceListing[]; tenantId: string; canSupply: boolean
  selectedId: string | null; open: (listing: MarketplaceListing) => void
  archive: (listing: MarketplaceListing) => void
}

export function MarketplaceListings(props: ListingsProps) {
  const visible = props.listings.filter(item => item.currentVersion)
  if (visible.length === 0) return <MarketplaceEmpty icon="inventory" title="No published supply found"
    copy="Adjust the filters or ask a supplier to publish current reviewed inventory." />
  return <section className="marketplace-ledger connected-marketplace-catalogue" aria-labelledby="marketplace-supply-ledger-title">
    <header className="marketplace-ledger-heading"><div>
      <h2 id="marketplace-supply-ledger-title">Showing {visible.length} inventory option{visible.length === 1 ? '' : 's'}</h2>
      <p>Rates, availability and supplier identity come from the current published listing version.</p></div>
      <span>Audience fit is shown only when campaign evidence exists</span></header>
    <div className="connected-marketplace-card-grid">{visible.map(listing => <ListingCard key={listing.id} listing={listing}
      tenantId={props.tenantId} canSupply={props.canSupply}
      selected={listing.id === props.selectedId} open={props.open}
      archive={props.archive} />)}</div>
  </section>
}

function ListingCard({ listing, tenantId, canSupply, selected, open, archive }: {
  listing: MarketplaceListing; tenantId: string; canSupply: boolean
  selected: boolean; open: (listing: MarketplaceListing) => void
  archive: (listing: MarketplaceListing) => void
}) {
  const version = listing.currentVersion
  if (!version) return null
  const owned = listing.supplierTenantId === tenantId
  return <article className={`connected-marketplace-card${selected ? ' is-selected' : ''}`}>
    <div className="connected-marketplace-art" style={{ backgroundImage: `url(${channelArtwork(version.channel)})` }}>
      <span>{masterLabel(masterDataDefinitions.channels, version.channel)}</span>
      <MediaTypeIcon channel={version.channel} />
    </div>
    <div className="connected-marketplace-card-copy"><header><div><strong>{version.productName}</strong>
      <small>{version.supplierName}</small></div><button type="button" aria-label={`Open ${version.productName}`}
        onClick={() => open(listing)}>♡</button></header>
      <p><Icon name="globe" />{version.geography}</p>
      <dl><div><dt>Current rate</dt><dd>{formatMoney(version.amountMinor, version.currency)}</dd>
        <small>{humanizeCode(version.rateType, true)}</small></div>
        <div><dt>Availability</dt><dd>{masterLabel(masterDataDefinitions.availabilityStatuses, version.availability)}</dd>
        <small>{version.availabilityValidUntilUtc ? `to ${formatDate(version.availabilityValidUntilUtc)}` : 'No expiry supplied'}</small></div></dl>
      <button className="connected-marketplace-select" type="button" onClick={() => open(listing)}>
        {selected ? 'Selected' : 'View details'}</button>
      {canSupply && owned && <button className="text-action connected-marketplace-archive" type="button"
        onClick={() => archive(listing)}>Archive listing</button>}
    </div>
  </article>
}

function channelArtwork(channel: string) {
  const code = channel.toLowerCase()
  if (code.includes('radio')) return '/assets/media-inventory/radio-real.jpg'
  if (code.includes('tv') || code.includes('television')) return '/assets/media-inventory/television-real.jpg'
  if (code.includes('print')) return '/assets/media-inventory/print-real.jpg'
  if (code.includes('digital') || code.includes('social')) return '/assets/media-inventory/digital-real.jpg'
  if (code.includes('experiential')) return '/assets/media-inventory/experiential-real.jpg'
  return '/assets/media-inventory/out-of-home-real.jpg'
}

export function MarketplaceListingInspector({ listing, tenantId, canBuy, close, request }: {
  listing: MarketplaceListing; tenantId: string; canBuy: boolean
  close: () => void; request: (listing: MarketplaceListing) => void
}) {
  const version = listing.currentVersion
  if (!version) return null
  const canRequest = canBuy && listing.supplierTenantId !== tenantId
  return <aside className="marketplace-inspector" aria-labelledby="marketplace-listing-title">
    <header><div><p className="eyebrow">Published listing</p>
      <h2 id="marketplace-listing-title">{version.productName}</h2>
      <p>{version.supplierName}</p></div>
      <button className="text-action" type="button" onClick={close}>Close</button></header>
    <dl className="marketplace-inspector-facts">
      <Fact label="Channel" value={masterLabel(masterDataDefinitions.channels, version.channel)} />
      <Fact label="Format" value={humanizeCode(version.productType, true)} />
      <Fact label="Geography" value={version.geography} />
      <Fact label="Current rate" value={formatMoney(version.amountMinor, version.currency)} />
      <Fact label="Rate basis" value={humanizeCode(version.rateType, true)} />
      <Fact label="Availability" value={masterLabel(
        masterDataDefinitions.availabilityStatuses, version.availability)} />
      <Fact label="Availability valid until" value={version.availabilityValidUntilUtc
        ? formatDateTime(version.availabilityValidUntilUtc) : 'No expiry supplied'} />
      <Fact label="Published" value={formatDateTime(version.publishedAtUtc)} />
    </dl>
    <section className="marketplace-response"><strong>Published terms</strong>
      <p>{version.terms}</p></section>
    <footer className="marketplace-inspector-actions"><p><Icon name="shield" />This is the exact
      current published version. A request creates a separate reviewed exchange.</p>
      {canRequest && <div><button className="primary-button" type="button"
        onClick={() => request(listing)}>Request availability</button></div>}</footer>
  </aside>
}

type RequestsProps = {
  items: MarketplaceRfq[]; tenantId: string; busy: boolean; selectedId: string | null
  select: (rfq: MarketplaceRfq) => void; send: (rfq: MarketplaceRfq) => void
  prepareResponse: (rfq: MarketplaceRfq) => void; accept: (rfq: MarketplaceRfq) => void
  responseTarget: MarketplaceRfq | null; closeResponse: () => void
  submitResponse: (values: ResponseValues) => Promise<void>
}

export function MarketplaceRequests(props: RequestsProps) {
  const selected = props.items.find(item => item.id === props.selectedId) ?? props.items[0] ?? null
  return <div id="marketplace-requests-panel" aria-label="Requests"
    className="marketplace-master-detail marketplace-request-workbench">
    <RequestLedger items={props.items} selectedId={selected?.id ?? null} select={props.select} />
    {props.responseTarget
      ? <ResponseForm rfq={props.responseTarget} busy={props.busy} close={props.closeResponse}
        respond={props.submitResponse} />
      : <RequestInspector rfq={selected} tenantId={props.tenantId} busy={props.busy}
        send={props.send} respond={props.prepareResponse} accept={props.accept} />}
  </div>
}

function RequestLedger({ items, selectedId, select }: {
  items: MarketplaceRfq[]; selectedId: string | null; select: (rfq: MarketplaceRfq) => void
}) {
  return <section className="marketplace-ledger" aria-labelledby="marketplace-requests-title">
    <header className="marketplace-ledger-heading"><div><p className="eyebrow">Buyer–supplier exchange</p>
      <h2 id="marketplace-requests-title">Requests</h2></div>
      <span>{items.length} open or retained</span></header>
    {items.length === 0 ? <MarketplaceEmpty icon="inbox" title="No requests yet"
      copy="A request appears here after a buyer chooses published inventory." />
      : <div className="marketplace-table-scroll"><table className="marketplace-table marketplace-request-table">
        <thead><tr><th>Request</th><th className="marketplace-secondary-column">Supplier</th>
          <th>Due</th><th>Status</th><th><span className="sr-only">Open</span></th></tr></thead>
        <tbody>{items.map(rfq => <tr key={rfq.id}
          className={rfq.id === selectedId ? 'marketplace-row-selected' : undefined}>
          <td><button className="marketplace-row-select" type="button" onClick={() => select(rfq)}>
            <strong>{rfq.subject}</strong><small>{rfq.productName}</small></button></td>
          <td className="marketplace-secondary-column">{rfq.supplierName}</td>
          <td>{formatDateTime(rfq.dueAtUtc)}</td>
          <td><span className="marketplace-state"><small>{rfq.status}</small>
            {rfqStatusLabel(rfq.status)}</span></td>
          <td><button className="marketplace-open-row" type="button" onClick={() => select(rfq)}
            aria-label={`Open ${rfq.subject}`}><Icon name="arrow" /></button></td>
        </tr>)}</tbody>
      </table></div>}
  </section>
}

function RequestInspector({ rfq, tenantId, busy, send, respond, accept }: {
  rfq: MarketplaceRfq | null; tenantId: string; busy: boolean
  send: (rfq: MarketplaceRfq) => void; respond: (rfq: MarketplaceRfq) => void
  accept: (rfq: MarketplaceRfq) => void
}) {
  if (!rfq) return <aside className="marketplace-inspector marketplace-inspector-empty">
    <Icon name="inbox" /><div><h2>No request selected</h2>
      <p>Select a retained request to inspect its exact terms and next action.</p></div></aside>
  const buyer = rfq.buyerTenantId === tenantId
  return <aside className="marketplace-inspector" aria-labelledby="marketplace-request-subject">
    <header><div><p className="eyebrow">Selected request</p>
      <h2 id="marketplace-request-subject">{rfq.subject}</h2><p>{rfq.productName}</p></div>
      <span className="marketplace-inspector-status"><small>Status</small>
        <strong>{rfqStatusLabel(rfq.status)}</strong></span></header>
    <dl className="marketplace-inspector-facts">
      <Fact label="Supplier" value={rfq.supplierName} />
      <Fact label="Quantity" value={String(rfq.quantity)} />
      <Fact label="Requested start" value={formatDate(rfq.requestedStart)} />
      <Fact label="Requested end" value={formatDate(rfq.requestedEnd)} />
      <Fact label="Response due" value={formatDateTime(rfq.dueAtUtc)} />
      <Fact label="Last updated" value={formatDateTime(rfq.updatedAtUtc)} />
    </dl>
    {rfq.response ? <SupplierResponse rfq={rfq} />
      : <p className="marketplace-awaiting-response">No supplier response is retained yet.</p>}
    <RequestActions rfq={rfq} buyer={buyer} busy={busy} send={send}
      respond={respond} accept={accept} />
  </aside>
}

function SupplierResponse({ rfq }: { rfq: MarketplaceRfq }) {
  const response = rfq.response
  if (!response) return null
  return <section className="marketplace-response" aria-labelledby="supplier-response-title">
    <header><div><p className="eyebrow">Immutable supplier response</p>
      <h3 id="supplier-response-title">{formatMoney(response.amountMinor, response.currency)}</h3></div>
      <span>{masterLabel(masterDataDefinitions.availabilityStatuses, response.availability)}</span></header>
    <dl><Fact label="Valid until" value={formatDateTime(response.validUntilUtc)} />
      <Fact label="Response version" value={String(response.responseVersion)} /></dl>
    <p>{response.terms}</p>
    <div className="marketplace-evidence"><strong>Evidence references</strong>
      {response.evidenceReferences.length === 0 ? <span>Not supplied</span>
        : <ul>{response.evidenceReferences.map(reference => <li key={reference}>{reference}</li>)}</ul>}
    </div>
  </section>
}

function RequestActions({ rfq, buyer, busy, send, respond, accept }: {
  rfq: MarketplaceRfq; buyer: boolean; busy: boolean
  send: (rfq: MarketplaceRfq) => void; respond: (rfq: MarketplaceRfq) => void
  accept: (rfq: MarketplaceRfq) => void
}) {
  const draft = masterDataCodes.marketplaceRfqStatuses.draft
  const sent = masterDataCodes.marketplaceRfqStatuses.sent
  const responded = masterDataCodes.marketplaceRfqStatuses.responded
  return <footer className="marketplace-inspector-actions">
    <p><Icon name="shield" />{consequenceCopy(rfq.status)}</p><div>
      {buyer && rfq.status === draft && <button className="primary-button" type="button"
        disabled={busy} onClick={() => send(rfq)}>Send to supplier</button>}
      {!buyer && rfq.status === sent && <button className="primary-button" type="button"
        disabled={busy} onClick={() => respond(rfq)}>Prepare response</button>}
      {buyer && rfq.status === responded && <button className="primary-button" type="button"
        disabled={busy} onClick={() => accept(rfq)}>Accept exact response</button>}
    </div>
  </footer>
}

function MarketplaceEmpty({ icon, title, copy }: {
  icon: 'inventory' | 'inbox'; title: string; copy: string
}) {
  return <div className="marketplace-empty"><span><Icon name={icon} /></span><div>
    <h2>{title}</h2><p>{copy}</p></div></div>
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function masterLabel(items: ReadonlyArray<{ code: string; displayLabel: string }>, code: string) {
  return mediaLabel(code) ?? items.find(item => item.code === code)?.displayLabel ?? humanizeCode(code, true)
}

function rfqStatusLabel(code: string) {
  return masterLabel(masterDataDefinitions.marketplaceRfqStatuses, code)
}

function consequenceCopy(status: string) {
  if (status === masterDataCodes.marketplaceRfqStatuses.draft) {
    return 'The supplier has not been contacted. Sending exposes this exact request in their workspace.'
  }
  if (status === masterDataCodes.marketplaceRfqStatuses.responded) {
    return 'Acceptance records this exact response only. It does not create a booking.'
  }
  if (status === masterDataCodes.marketplaceRfqStatuses.sent) {
    return 'Only the addressed supplier can submit the retained response and supporting evidence.'
  }
  return 'This exchange remains retained with its exact versions and tenant context.'
}
