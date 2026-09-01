import { useState, type FormEvent } from 'react'
import type { InventoryProductSummary } from '../api/inventory-schemas'
import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { majorAmountToMinor } from '../presentation/format'

export function MarketplaceSearchForm({ search }: {
  search: (filters: { search: string; channel: string; geography: string }) => void
}) {
  const [filters, setFilters] = useState({ search: '', channel: '', geography: '' })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); search(filters)
  }
  return <form className="marketplace-filter-bar" onSubmit={submit} aria-labelledby="market-filter-title">
    <header><span><Icon name="search" /></span><div><p className="eyebrow">Supply filters</p>
      <h2 id="market-filter-title">Find published inventory</h2></div></header>
    <div className="marketplace-filter-fields">
      <label className="field-group">Product or supplier<input value={filters.search}
        onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
      <label className="field-group">Channel<select value={filters.channel}
        onChange={(event) => setFilters({ ...filters, channel: event.target.value })}>
        <option value="">All channels</option>
        {masterDataDefinitions.channels.filter(item => item.isActive).map(item =>
          <option value={item.code} key={item.code}>{item.displayLabel}</option>)}
      </select></label>
      <label className="field-group">Geography<input value={filters.geography}
        onChange={(event) => setFilters({ ...filters, geography: event.target.value })} /></label>
      <button className="secondary-button">Search marketplace</button>
    </div>
  </form>
}

export function PublishProductForm({ products, busy, publish }: {
  products: InventoryProductSummary[]; busy: boolean
  publish: (productId: string, terms: string) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    void publish(String(values.get('productId')), String(values.get('terms')))
  }
  return <form className="marketplace-publish" onSubmit={submit}
    id="marketplace-publish-panel" aria-labelledby="marketplace-publish-title">
    <header><span><Icon name="inventory" /></span><div><p className="eyebrow">Supplier listing</p>
      <h2 id="marketplace-publish-title">Publish reviewed inventory</h2>
      <p>Choose an inventory record and expose only its current reviewed commercial projection.</p>
    </div></header>
    <div className="marketplace-publish-fields">
      <label className="field-group">Inventory product<select name="productId" required>
        <option value="">Choose reviewed inventory</option>
        {products.map(product => <option key={product.id} value={product.id}>
          {product.name} · {product.geography}</option>)}
      </select></label>
      <label className="field-group">Marketplace terms<textarea name="terms" required
        maxLength={5000} defaultValue="Subject to final human-approved booking and availability." />
      </label>
      <button className="primary-button" disabled={busy || products.length === 0}>
        {busy ? 'Publishing…' : 'Publish listing'}</button>
    </div>
    <p className="marketplace-boundary-note"><Icon name="shield" />Publication does not expose the
      supplier source file, review history or private inventory record.</p>
  </form>
}

export function RfqForm({ listing, busy, close, create }: {
  listing: MarketplaceListing; busy: boolean; close: () => void
  create: (values: { subject: string; requestedStart: string; requestedEnd: string;
    quantity: number; dueAtUtc: string }) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    void create({ subject: String(values.get('subject')),
      requestedStart: String(values.get('requestedStart')),
      requestedEnd: String(values.get('requestedEnd')),
      quantity: Number(values.get('quantity')),
      dueAtUtc: new Date(String(values.get('dueAtUtc'))).toISOString() })
  }
  const version = listing.currentVersion
  return <form className="marketplace-action-form" onSubmit={submit}
    aria-labelledby="marketplace-rfq-editor-title">
    <header><div><p className="eyebrow">New request</p>
      <h2 id="marketplace-rfq-editor-title">{version?.productName}</h2>
      <p>{version?.supplierName} · {version?.geography}</p></div>
      <button className="text-action" type="button" onClick={close}>Close</button></header>
    <label className="field-group marketplace-field-wide">Request subject
      <input name="subject" required maxLength={500} /></label>
    <div className="marketplace-editor-grid">
      <label className="field-group">Start date<input name="requestedStart" type="date" required /></label>
      <label className="field-group">End date<input name="requestedEnd" type="date" required /></label>
      <label className="field-group">Quantity<input name="quantity" type="number" min="1"
        defaultValue="1" required /></label>
      <label className="field-group">Supplier response due
        <input name="dueAtUtc" type="datetime-local" required /></label>
    </div>
    <footer><p>Creates a draft for review. The supplier is not contacted yet.</p>
      <button className="primary-button" disabled={busy}>
        {busy ? 'Creating…' : 'Create draft request'}</button></footer>
  </form>
}

export function ResponseForm({ rfq, busy, close, respond }: {
  rfq: MarketplaceRfq; busy: boolean; close: () => void
  respond: (values: { amountMinor: number; currency: string; availability: string;
    terms: string; validUntilUtc: string; evidenceReferences: string[] }) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const evidence = String(values.get('evidence')).split('\n').map(item => item.trim()).filter(Boolean)
    const currency = String(values.get('currency'))
    void respond({ amountMinor: majorAmountToMinor(Number(values.get('amount')), currency), currency,
      availability: String(values.get('availability')), terms: String(values.get('terms')),
      validUntilUtc: new Date(String(values.get('validUntilUtc'))).toISOString(),
      evidenceReferences: evidence })
  }
  return <form className="marketplace-action-form marketplace-response-form" onSubmit={submit}
    aria-labelledby="marketplace-response-editor-title">
    <header><div><p className="eyebrow">Supplier response</p>
      <h2 id="marketplace-response-editor-title">{rfq.subject}</h2>
      <p>{rfq.productName} · {rfq.supplierName}</p></div>
      <button className="text-action" type="button" onClick={close}>Close</button></header>
    <div className="marketplace-editor-grid">
      <label className="field-group">Amount<input name="amount" type="number" min="0"
        step="any" required /></label>
      <label className="field-group">Currency<select name="currency" defaultValue="" required>
        <option value="">Choose currency</option>
        {masterDataDefinitions.currencies.filter(item => item.isActive).map(item =>
          <option value={item.code} key={item.code}>{item.displayLabel}</option>)}</select></label>
      <label className="field-group">Availability<select name="availability">
        <option value={masterDataCodes.availabilityStatuses.available}>Available</option>
        <option value={masterDataCodes.availabilityStatuses.limited}>Limited</option>
        <option value={masterDataCodes.availabilityStatuses.unavailable}>Unavailable</option>
      </select></label>
      <label className="field-group">Valid until<input name="validUntilUtc"
        type="datetime-local" required /></label>
    </div>
    <label className="field-group marketplace-field-wide">Terms
      <textarea name="terms" required maxLength={5000} /></label>
    <label className="field-group marketplace-field-wide">Evidence references, one per line
      <textarea name="evidence" maxLength={5000} /></label>
    <footer><p>The submitted response is retained as an immutable commercial version.</p>
      <button className="primary-button" disabled={busy}>
        {busy ? 'Submitting…' : 'Submit immutable response'}</button></footer>
  </form>
}
