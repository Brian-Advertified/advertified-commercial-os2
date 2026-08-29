import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryProductPage } from '../api/inventory-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { notifications } from '../notifications/notifications'

const importRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
  inventoryCodes.role.supplierAdmin,
])

export function InventoryPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <InventoryIndex key={selected.tenantId} tenantId={selected.tenantId}
    canImport={importRoles.has(selected.roleCode)} />
}

function InventoryIndex({ tenantId, canImport }: { tenantId: string; canImport: boolean }) {
  const { session } = useSession()
  const navigate = useNavigate()
  const [page, setPage] = useState<InventoryProductPage | null>(null)
  const [filters, setFilters] = useState({ search: '', channel: '', geography: '' })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let active = true
    void inventoryApi.search(tenantId, {}).then((result) => {
      if (active) setPage(result)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    })
    return () => { active = false }
  }, [tenantId])

  async function load(cursor?: string) {
    try {
      setError(null)
      const result = await inventoryApi.search(tenantId, { ...filters, cursor })
      setPage((current) => cursor && current
        ? { ...result, items: [...current.items, ...result.items] }
        : result)
    } catch (failure) { setError(humanMessage(failure)) }
  }

  async function upload(values: FormData) {
    const file = values.get('source')
    if (!session || !page || !(file instanceof File) || file.size === 0) return
    if (file.size > page.maximumSourceBytes) {
      setError(`Choose a source no larger than ${formatMiB(page.maximumSourceBytes)}.`)
      return
    }
    setBusy(true); setError(null)
    try {
      const record = await inventoryApi.upload(
        tenantId, String(values.get('supplierName')), file, session.antiforgeryToken)
      notifications.success('The source is protected and ready to process.')
      navigate(`/inventory/imports/${record.id}`)
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }

  if (error && !page) return <MessageState title="Inventory could not be loaded" message={error} />
  if (!page) return <LoadingState label="Loading inventory" />
  return <section aria-labelledby="inventory-title">
    <header className="page-heading page-heading-split"><div>
      <p className="eyebrow">Reviewed media supply</p><h1 id="inventory-title">Inventory</h1>
      <p>Search published products or protect a supplier file for source-linked review.</p>
    </div><span className="status-chip">{page.items.length} loaded</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className={canImport ? 'inventory-layout' : undefined}>
      <div><SearchForm filters={filters} setFilters={setFilters} search={() => void load()} />
        <ProductCards page={page} loadMore={(cursor) => void load(cursor)} /></div>
      {canImport && <UploadForm busy={busy} maximumSourceBytes={page.maximumSourceBytes}
        upload={upload} />}
    </div>
  </section>
}

function SearchForm({ filters, setFilters, search }: {
  filters: { search: string; channel: string; geography: string }
  setFilters: (value: { search: string; channel: string; geography: string }) => void
  search: () => void
}) {
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); search() }
  return <form className="detail-card inventory-search" onSubmit={submit}>
    <label className="field-group">Product or code<input value={filters.search}
      onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    <label className="field-group">Channel<select value={filters.channel}
      onChange={(event) => setFilters({ ...filters, channel: event.target.value })}>
      <option value="">All channels</option><option value={inventoryCodes.channel.ooh}>Out of Home</option>
      <option value={inventoryCodes.channel.dooh}>Digital Out of Home</option>
      <option value={inventoryCodes.channel.radio}>Radio</option>
    </select></label>
    <label className="field-group">Geography<input value={filters.geography}
      onChange={(event) => setFilters({ ...filters, geography: event.target.value })} /></label>
    <button className="secondary-button">Search inventory</button>
  </form>
}

function ProductCards({ page, loadMore }: {
  page: InventoryProductPage; loadMore: (cursor: string) => void
}) {
  return <div className="record-stack inventory-results" aria-label="Inventory products">
    {page.items.length === 0 && <article className="detail-card"><h2>No products found</h2>
      <p>Adjust the filters or publish a reviewed source.</p></article>}
    {page.items.map((item) => <Link className="record-card" key={item.id}
      to={`/inventory/products/${item.id}`}><div><span className="status-chip">{item.channel}</span>
        <h2>{item.name}</h2></div><p>{item.supplierName} · {item.productCode}</p>
      <p>{item.geography} · {item.verification.toLowerCase().replaceAll('_', ' ')}</p>
      <span className="record-arrow" aria-hidden="true">→</span></Link>)}
    {page.nextCursor && <button className="secondary-button load-more" type="button"
      onClick={() => loadMore(page.nextCursor!)}>Load more</button>}
  </div>
}

function UploadForm({ busy, maximumSourceBytes, upload }: {
  busy: boolean; maximumSourceBytes: number; upload: (values: FormData) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); void upload(new FormData(event.currentTarget))
  }
  return <form className="detail-card opportunity-form inventory-upload" onSubmit={submit}>
    <p className="eyebrow">Protected intake</p><h2>Import supplier file</h2>
    <label className="field-group">Supplier name<input name="supplierName" required maxLength={300} /></label>
    <label className="field-group">Source file<input name="source" type="file" required
      accept=".csv,.xlsx,.pdf,.docx,.png,.jpg,.jpeg" /></label>
    <p className="field-note">CSV, Excel, PDF, Word, PNG or JPEG · maximum
      {' '}{formatMiB(maximumSourceBytes)}.</p>
    <button className="primary-button" disabled={busy}>{busy ? 'Protecting…' : 'Protect and import'}</button>
  </form>
}

function formatMiB(bytes: number): string {
  return `${new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 1 })
    .format(bytes / (1024 * 1024))} MiB`
}
