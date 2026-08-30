import { useState, type FormEvent } from 'react'
import type { InventoryProductSummary } from '../api/inventory-schemas'
import type { MarketplaceListing, MarketplaceRfq } from '../api/marketplace-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

export function MarketplaceSearchForm({ search }: {
  search: (filters: { search: string; channel: string; geography: string }) => void
}) {
  const [filters, setFilters] = useState({ search: '', channel: '', geography: '' })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); search(filters)
  }
  return <form className="detail-card marketplace-search" onSubmit={submit}>
    <label className="field-group">Product or supplier<input value={filters.search}
      onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    <label className="field-group">Channel<select value={filters.channel}
      onChange={(event) => setFilters({ ...filters, channel: event.target.value })}>
      <option value="">All channels</option>
      <option value={masterDataCodes.channels.ooh}>Out of Home</option>
      <option value={masterDataCodes.channels.dooh}>Digital Out of Home</option>
      <option value={masterDataCodes.channels.radio}>Radio</option>
      <option value={masterDataCodes.channels.digital}>Digital</option>
    </select></label>
    <label className="field-group">Geography<input value={filters.geography}
      onChange={(event) => setFilters({ ...filters, geography: event.target.value })} /></label>
    <button className="secondary-button">Search marketplace</button>
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
  return <form className="detail-card marketplace-publish" onSubmit={submit}>
    <p className="eyebrow">Supplier listing</p><h2>Publish reviewed inventory</h2>
    <p>Only the current reviewed product, rate and availability projection becomes visible.</p>
    <label className="field-group">Inventory product<select name="productId" required>
      <option value="">Choose reviewed inventory</option>
      {products.map((product) => <option key={product.id} value={product.id}>
        {product.name} · {product.geography}</option>)}
    </select></label>
    <label className="field-group">Marketplace terms<textarea name="terms" required
      maxLength={5000} defaultValue="Subject to final human-approved booking and availability." />
    </label>
    <button className="primary-button" disabled={busy || products.length === 0}>
      {busy ? 'Publishing…' : 'Publish listing'}
    </button>
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
    void create({
      subject: String(values.get('subject')),
      requestedStart: String(values.get('requestedStart')),
      requestedEnd: String(values.get('requestedEnd')),
      quantity: Number(values.get('quantity')),
      dueAtUtc: new Date(String(values.get('dueAtUtc'))).toISOString(),
    })
  }
  return <form className="detail-card marketplace-action-form" onSubmit={submit}>
    <div className="card-heading"><div><p className="eyebrow">New request</p>
      <h2>{listing.currentVersion?.productName}</h2></div>
      <button className="text-action" type="button" onClick={close}>Close</button></div>
    <label className="field-group">Request subject<input name="subject" required maxLength={500} /></label>
    <div className="form-grid"><label className="field-group">Start date
      <input name="requestedStart" type="date" required /></label>
      <label className="field-group">End date<input name="requestedEnd" type="date" required /></label>
      <label className="field-group">Quantity<input name="quantity" type="number" min="1"
        defaultValue="1" required /></label>
      <label className="field-group">Supplier response due
        <input name="dueAtUtc" type="datetime-local" required /></label></div>
    <button className="primary-button" disabled={busy}>
      {busy ? 'Creating…' : 'Create draft request'}</button>
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
    const evidence = String(values.get('evidence')).split('\n').map((item) => item.trim())
      .filter(Boolean)
    void respond({ amountMinor: Math.round(Number(values.get('amount')) * 100),
      currency: String(values.get('currency')), availability: String(values.get('availability')),
      terms: String(values.get('terms')),
      validUntilUtc: new Date(String(values.get('validUntilUtc'))).toISOString(),
      evidenceReferences: evidence })
  }
  return <form className="detail-card marketplace-action-form" onSubmit={submit}>
    <div className="card-heading"><div><p className="eyebrow">Supplier response</p>
      <h2>{rfq.subject}</h2></div>
      <button className="text-action" type="button" onClick={close}>Close</button></div>
    <div className="form-grid"><label className="field-group">Amount
      <input name="amount" type="number" min="0" step="0.01" required /></label>
      <label className="field-group">Currency<select name="currency"
        defaultValue={masterDataCodes.currencies.zar}>
        <option value={masterDataCodes.currencies.zar}>ZAR</option>
        <option value={masterDataCodes.currencies.usd}>USD</option></select></label>
      <label className="field-group">Availability<select name="availability">
        <option value={masterDataCodes.availabilityStatuses.available}>Available</option>
        <option value={masterDataCodes.availabilityStatuses.limited}>Limited</option>
        <option value={masterDataCodes.availabilityStatuses.unavailable}>Unavailable</option>
      </select></label>
      <label className="field-group">Valid until<input name="validUntilUtc"
        type="datetime-local" required /></label></div>
    <label className="field-group">Terms<textarea name="terms" required maxLength={5000} /></label>
    <label className="field-group">Evidence references, one per line
      <textarea name="evidence" maxLength={5000} /></label>
    <button className="primary-button" disabled={busy}>
      {busy ? 'Submitting…' : 'Submit immutable response'}</button>
  </form>
}
