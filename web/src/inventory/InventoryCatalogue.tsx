import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { InventoryProductPage, InventoryProductSummary } from '../api/inventory-schemas'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { formatDateTime, formatMiB, humanizeCode } from '../presentation/format'

export type InventoryFilters = { search: string; channel: string; geography: string }

export function InventoryCatalogueHeader({ items }: { items: InventoryProductSummary[] }) {
  const suppliers = new Set(items.map(item => item.supplierId)).size
  const verified = items.filter(item => item.verification === masterDataCodes.verificationLevels.sourceVerified ||
    item.verification === masterDataCodes.verificationLevels.humanVerified).length
  const ooh = items.filter(item => item.channel === masterDataCodes.channels.ooh ||
    item.channel === masterDataCodes.channels.dooh).length
  return <header className="approved-catalogue-hero"><div><p className="eyebrow">Published Inventory Catalogue</p>
    <h1 id="inventory-title">Media inventory</h1><p>Search published, source-linked media products by supplier, channel and geography.</p></div>
    <dl><Snapshot label="Inventory" value={items.length} /><Snapshot label="Verified" value={verified} />
      <Snapshot label="Suppliers" value={suppliers} /><Snapshot label="OOH / DOOH" value={ooh} /></dl></header>
}

function Snapshot({ label, value }: { label: string; value: number }) {
  return <div><dt>{label}</dt><dd>{new Intl.NumberFormat().format(value)}</dd><small>Current result window</small></div>
}

export function InventorySearchForm({ filters, setFilters, search }: {
  filters: InventoryFilters
  setFilters: (value: InventoryFilters) => void
  search: () => void
}) {
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); search() }
  return <form className="approved-catalogue-filterbar" onSubmit={submit}>
    <label className="approved-catalogue-search"><Icon name="search" /><input value={filters.search}
      onChange={event => setFilters({ ...filters, search: event.target.value })}
      placeholder="Search inventory by product, location, supplier…" maxLength={200} /></label>
    <select aria-label="Channel" value={filters.channel}
      onChange={event => setFilters({ ...filters, channel: event.target.value })}>
      <option value="">Channel · All</option>{masterDataDefinitions.channels.filter(item => item.isActive).map(item =>
        <option value={item.code} key={item.code}>{item.displayLabel}</option>)}</select>
    <input aria-label="Geography" value={filters.geography} maxLength={500}
      onChange={event => setFilters({ ...filters, geography: event.target.value })} placeholder="Geography · All regions" />
    <button className="primary-button" type="submit">Update results</button>
  </form>
}

export function InventoryProductCards({ page, loadMore }: {
  page: InventoryProductPage
  loadMore: (cursor: string) => void
}) {
  if (page.items.length === 0) return <article className="approved-inventory-empty"><Icon name="inventory" />
    <div><h2>No products match these filters</h2><p>Adjust the filters. Only reviewed, published inventory appears here.</p></div></article>
  const suppliers = [...new Map(page.items.map(item => [item.supplierId, item.supplierName])).values()].slice(0, 7)
  const geographies = [...new Set(page.items.map(item => item.geography))].slice(0, 9)
  return <section className="approved-catalogue-results" aria-label="Published inventory">
    <div className="approved-catalogue-controls"><span>{page.items.length.toLocaleString()} inventory items in this result window</span></div>
    <div className="approved-catalogue-body">
      <aside className="approved-supplier-list"><header>Suppliers in this result window</header>{suppliers.map((supplier, index) => <div key={supplier}>
        <span className={`supplier-dot tone-${(index % 4) + 1}`}>●</span><strong>{supplier}</strong>
        <small>{page.items.filter(item => item.supplierName === supplier).length} items</small></div>)}</aside>
      <div className="approved-product-card-grid">{page.items.map(item =>
        <InventoryCard key={item.id} item={item} />)}</div>
      <aside className="approved-catalogue-map"><header><span>Published geographies</span></header>
        <div className="approved-map-canvas"><strong>{geographies.length} represented in this window</strong>
          <small>{geographies.join(' · ') || 'No geography supplied'}</small>
          <p>A spatial map requires verified product geometry. Open a product to inspect its coordinates.</p></div></aside>
    </div>
    {page.nextCursor && <button className="secondary-button approved-load-more" type="button" onClick={() => loadMore(page.nextCursor!)}>Load more products</button>}
  </section>
}

function InventoryCard({ item }: { item: InventoryProductSummary }) {
  return <Link className="approved-inventory-card" to={`/inventory/products/${item.id}`}>
    <div className="approved-inventory-card-media" aria-hidden="true"><MediaTypeIcon channel={item.channel} /></div>
    <div className="approved-inventory-card-copy"><small>{item.geography}</small>
      <strong>{item.name}</strong><span>{humanizeCode(item.productType, true)}</span></div>
    <footer><em className={verificationTone(item.verification)}>{humanizeCode(item.verification, true)}</em>
      <small>{item.supplierName}</small><time>{relative(item.updatedAtUtc)}</time></footer></Link>
}

export function InventoryUploadForm({ busy, maximumSourceBytes, upload }: {
  busy: boolean
  maximumSourceBytes: number
  upload: (values: FormData) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); void upload(new FormData(event.currentTarget)) }
  return <form className="approved-import-source-card" onSubmit={submit}><header><span>1</span><div><h2>Inventory Import</h2><p>Import new inventory</p></div></header>
    <p>Upload files from any supported source. Advertified preserves the original and structures the commercial data.</p>
    <label className="approved-import-dropzone"><input name="source" type="file" required accept=".csv,.xlsx,.pdf,.docx,.pptx,.png,.jpg,.jpeg" />
      <Icon name="evidence" /><strong>Drag & drop files here</strong><small>or browse files to upload</small></label>
    <div className="approved-source-types"><span>PDF<small>Documents</small></span><span>Office<small>.xlsx .docx .pptx</small></span><span>CSV<small>.csv</small></span><span>Scans<small>OCR review</small></span><span>Images<small>.png .jpg</small></span></div>
    <label className="field-group">Supplier / media owner<input name="supplierName" required maxLength={300} placeholder="Supplier name" /></label>
    <small className="approved-import-limit">Maximum source size {formatMiB(maximumSourceBytes)}. Importing does not publish unreviewed inventory.</small>
    <button className="primary-button" disabled={busy}>{busy ? 'Starting import…' : 'Start import →'}</button></form>
}

function relative(value: string) { const hours = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 3600000)); return hours < 24 ? `${hours}h` : `${Math.round(hours / 24)}d` }
function verificationTone(value: string) { return value === masterDataCodes.verificationLevels.sourceVerified || value === masterDataCodes.verificationLevels.humanVerified ? 'is-verified' : 'is-unverified' }

// Retain this component for accessible media-channel semantics in product identity contexts.
export function InventoryProductIdentity({ item }: { item: InventoryProductSummary }) {
  return <span className="inventory-product-identity"><span className="inventory-channel-icon"><MediaTypeIcon channel={item.channel} /></span>
    <span><strong>{item.name}</strong><small>{formatDateTime(item.updatedAtUtc)}</small></span></span>
}
