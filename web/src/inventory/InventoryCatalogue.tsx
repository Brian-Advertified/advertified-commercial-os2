import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { InventoryProductPage, InventoryProductSummary } from '../api/inventory-schemas'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { formatDateTime, formatMiB, humanizeCode } from '../presentation/format'

export type InventoryFilters = {
  search: string
  channel: string
  geography: string
}

export function InventoryCatalogueHeader({ items }: {
  items: InventoryProductSummary[]
}) {
  const suppliers = new Set(items.map(item => item.supplierId)).size
  const channels = new Set(items.map(item => item.channel)).size
  const geographies = new Set(items.map(item => item.geography)).size
  return <header className="inventory-workbench-hero"><div>
    <p className="eyebrow eyebrow-light">Published supply</p>
    <h1 id="inventory-title">Inventory</h1>
    <p>Search source-linked media products by channel, supplier and geography.</p>
  </div><dl>
    <InventorySnapshot label="Products" value={items.length} note="Current result window" />
    <InventorySnapshot label="Suppliers" value={suppliers} note="In these results" />
    <InventorySnapshot label="Channels" value={channels} note="In these results" />
    <InventorySnapshot label="Geographies" value={geographies} note="In these results" />
  </dl></header>
}

function InventorySnapshot({ label, value, note }: {
  label: string
  value: number
  note: string
}) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{note}</small></div>
}

export function InventorySearchForm({ filters, setFilters, search }: {
  filters: InventoryFilters
  setFilters: (value: InventoryFilters) => void
  search: () => void
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); search()
  }
  return <form className="inventory-filter-workbench" onSubmit={submit}>
    <header><span><Icon name="search" /></span><div><p className="eyebrow">Catalogue filters</p>
      <h2>Find the relevant media supply</h2></div></header>
    <div className="inventory-filter-grid">
      <label className="field-group">Product, supplier or code
        <input value={filters.search} maxLength={200}
          onChange={(event) => setFilters({ ...filters, search: event.target.value })}
          placeholder="Search published inventory" />
      </label>
      <label className="field-group">Channel
        <select value={filters.channel}
          onChange={(event) => setFilters({ ...filters, channel: event.target.value })}>
          <option value="">All configured channels</option>
          {masterDataDefinitions.channels.filter(item => item.isActive).map(item =>
            <option value={item.code} key={item.code}>{item.displayLabel}</option>)}
        </select>
      </label>
      <label className="field-group">Geography
        <input value={filters.geography} maxLength={500}
          onChange={(event) => setFilters({ ...filters, geography: event.target.value })}
          placeholder="City, area, route or region" />
      </label>
      <button className="primary-button" type="submit"><Icon name="search" /> Search inventory</button>
    </div>
  </form>
}

export function InventoryProductCards({ page, loadMore }: {
  page: InventoryProductPage
  loadMore: (cursor: string) => void
}) {
  if (page.items.length === 0) return <article className="inventory-empty-state">
    <span><Icon name="inventory" /></span><div><h2>No products match these filters</h2>
      <p>Adjust the channel, geography or search text. Inventory only appears after its source has been reviewed and published.</p></div>
  </article>
  return <section className="inventory-catalogue-results" aria-label="Inventory products">
    <header><div><p className="eyebrow">Published catalogue</p><h2>{page.items.length} products in this result window</h2></div>
      <span className="status-chip status-neutral">Source-linked supply</span></header>
    <div className="inventory-product-table">
      <div className="inventory-product-table-head" aria-hidden="true">
        <span>Product</span><span>Supplier</span>
        <span>Geography</span><span>Verification</span>
        <span aria-hidden="true" />
      </div>
      {page.items.map(item => <InventoryProductRow key={item.id} item={item} />)}
    </div>
    {page.nextCursor && <button className="secondary-button load-more" type="button"
      onClick={() => loadMore(page.nextCursor!)}>Load more products</button>}
  </section>
}

function InventoryProductRow({ item }: { item: InventoryProductSummary }) {
  const channel = channelLabel(item.channel)
  const verification = humanizeCode(item.verification, true)
  const updated = formatDateTime(item.updatedAtUtc)
  return <Link className="inventory-product-row" to={`/inventory/products/${item.id}`}
    aria-label={`Product: ${item.name}, ${channel}, ${humanizeCode(item.productType, true)}. Supplier: ${item.supplierName}. Geography: ${item.geography}. Verification: ${verification}, updated ${updated}.`}>
    <span className="inventory-product-identity">
      <span className="inventory-channel-icon"><MediaTypeIcon channel={item.channel} /></span>
      <span><small>{channel} · {humanizeCode(item.productType, true)}</small>
        <strong>{item.name}</strong><small>{item.productCode}</small></span>
    </span>
    <span>{item.supplierName}</span>
    <span>{item.geography}</span>
    <span className="inventory-verification-cell">
      <span className={`status-chip ${verificationTone(item.verification)}`}>
        {verification}</span>
      <small>Updated {updated}</small>
    </span>
    <Icon name="arrow" />
  </Link>
}

export function InventoryUploadForm({ busy, maximumSourceBytes, upload }: {
  busy: boolean
  maximumSourceBytes: number
  upload: (values: FormData) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); void upload(new FormData(event.currentTarget))
  }
  return <form className="inventory-import-workbench" onSubmit={submit}>
    <header><span><Icon name="shield" /></span><div><p className="eyebrow eyebrow-light">Protected intake</p>
      <h2>Import a supplier source</h2></div></header>
    <p>Preserve the supplier source, validate the file and review extracted products before anything is published.</p>
    <label className="field-group">Supplier name
      <input name="supplierName" required maxLength={300} placeholder="Supplier or media owner" />
    </label>
    <label className="inventory-file-field">Source file
      <input name="source" type="file" required
        accept=".csv,.xlsx,.pdf,.docx,.png,.jpg,.jpeg" />
      <span><Icon name="evidence" /><strong>Choose supplier source</strong>
        <small>CSV, Excel, PDF, Word or image · up to {formatMiB(maximumSourceBytes)}</small></span>
    </label>
    <div className="inventory-import-boundary"><Icon name="shield" />
      <span>Importing protects the source. It does not publish unreviewed products.</span></div>
    <button className="primary-button" disabled={busy}>
      {busy ? 'Protecting the source…' : 'Protect and import'}
    </button>
  </form>
}

function channelLabel(code: string) {
  return masterDataDefinitions.channels.find(item => item.code === code)?.displayLabel ??
    humanizeCode(code, true)
}

function verificationTone(value: string) {
  return value === masterDataCodes.verificationLevels.sourceVerified ||
    value === masterDataCodes.verificationLevels.humanVerified
    ? 'status-positive'
    : 'status-warning'
}
