import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

export function MarketplaceListings({ listings, tenantId, canBuy, canSupply,
  request, archive }: {
  listings: MarketplaceListing[]; tenantId: string; canBuy: boolean; canSupply: boolean
  request: (listing: MarketplaceListing) => void
  archive: (listing: MarketplaceListing) => void
}) {
  if (listings.length === 0) return <article className="detail-card marketplace-empty">
    <h2>No published supply found</h2><p>Adjust the search or ask a supplier to publish
      current reviewed inventory.</p></article>
  return <div className="marketplace-grid" aria-label="Published marketplace inventory">
    {listings.map((listing) => {
      const version = listing.currentVersion
      if (!version) return null
      const owned = listing.supplierTenantId === tenantId
      return <article className="detail-card marketplace-listing" key={listing.id}>
        <div className="card-heading"><span className="status-chip">{version.channel}</span>
          <span className="subtle-copy">{version.availability.replaceAll('_', ' ')}</span></div>
        <h2>{version.productName}</h2><p>{version.supplierName}</p>
        <dl className="marketplace-facts"><div><dt>Geography</dt><dd>{version.geography}</dd></div>
          <div><dt>Current rate</dt><dd>{money(version.amountMinor, version.currency)}</dd></div>
          <div><dt>Rate type</dt><dd>{version.rateType.replaceAll('_', ' ')}</dd></div></dl>
        <p className="field-note">{version.terms}</p>
        <div className="button-row">
          {canBuy && !owned && <button className="primary-button" type="button"
            onClick={() => request(listing)}>Request availability</button>}
          {canSupply && owned && <button className="secondary-button" type="button"
            onClick={() => archive(listing)}>Archive listing</button>}
        </div>
      </article>
    })}
  </div>
}

export function MarketplaceRequests({ items, tenantId, busy, send, respond, accept }: {
  items: MarketplaceRfq[]; tenantId: string; busy: boolean
  send: (rfq: MarketplaceRfq) => void; respond: (rfq: MarketplaceRfq) => void
  accept: (rfq: MarketplaceRfq) => void
}) {
  return <section className="marketplace-requests" aria-labelledby="marketplace-requests-title">
    <div className="section-heading"><div><p className="eyebrow">Buyer–supplier exchange</p>
      <h2 id="marketplace-requests-title">Requests</h2></div>
      <span className="status-chip">{items.length} open or retained</span></div>
    {items.length === 0 && <article className="detail-card"><h3>No requests yet</h3>
      <p>Requests appear here after a buyer chooses published inventory.</p></article>}
    <div className="record-stack">{items.map((rfq) => {
      const buyer = rfq.buyerTenantId === tenantId
      return <article className="detail-card marketplace-request" key={rfq.id}>
        <div className="card-heading"><div><span className="status-chip">{rfq.status}</span>
          <h3>{rfq.subject}</h3></div><span className="subtle-copy">
            Due {date(rfq.dueAtUtc)}</span></div>
        <p><strong>{rfq.productName}</strong> · {rfq.supplierName}</p>
        <p>{rfq.requestedStart} to {rfq.requestedEnd} · quantity {rfq.quantity}</p>
        {rfq.response && <div className="marketplace-response"><strong>
          Supplier response: {money(rfq.response.amountMinor, rfq.response.currency)}</strong>
          <p>{rfq.response.availability.replaceAll('_', ' ')} · valid until
            {' '}{date(rfq.response.validUntilUtc)}</p><p>{rfq.response.terms}</p></div>}
        <div className="button-row">
          {buyer && rfq.status === masterDataCodes.marketplaceRfqStatuses.draft &&
            <button className="primary-button" type="button"
            disabled={busy} onClick={() => send(rfq)}>Send to supplier</button>}
          {!buyer && rfq.status === masterDataCodes.marketplaceRfqStatuses.sent &&
            <button className="primary-button" type="button"
            disabled={busy} onClick={() => respond(rfq)}>Prepare response</button>}
          {buyer && rfq.status === masterDataCodes.marketplaceRfqStatuses.responded &&
            <button className="primary-button" type="button"
            disabled={busy} onClick={() => accept(rfq)}>Accept exact response</button>}
        </div>
      </article>
    })}</div>
  </section>
}

function money(amountMinor: number, currency: string): string {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency })
    .format(amountMinor / 100)
}

function date(value: string): string {
  return new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeStyle: 'short' })
    .format(new Date(value))
}
