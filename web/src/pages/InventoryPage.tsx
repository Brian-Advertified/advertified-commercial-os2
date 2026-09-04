import { useEffect, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryDuplicateCandidate, InventoryProductPage } from '../api/inventory-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import {
  InventoryCatalogueHeader,
  InventoryProductCards,
  InventorySearchForm,
  InventoryUploadForm,
  type InventoryFilters,
} from '../inventory/InventoryCatalogue'
import {
  InventorySemanticPreflightPanel,
} from '../inventory/InventorySemanticPreflightPanel'
import { notifications } from '../notifications/notifications'
import { formatMiB } from '../presentation/format'

const importRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
  inventoryCodes.role.supplierAdmin,
])

const reviewRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
])

const emptyFilters: InventoryFilters = {
  search: '',
  channel: '',
  geography: '',
}

export function InventoryPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <InventoryIndex key={selected.tenantId} tenantId={selected.tenantId}
    canImport={importRoles.has(selected.roleCode)}
    canReview={reviewRoles.has(selected.roleCode)} />
}

function InventoryIndex({ tenantId, canImport, canReview }: {
  tenantId: string; canImport: boolean; canReview: boolean
}) {
  const { session } = useSession()
  const navigate = useNavigate()
  const [page, setPage] = useState<InventoryProductPage | null>(null)
  const [filters, setFilters] = useState<InventoryFilters>(emptyFilters)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let active = true
    void inventoryApi.search(tenantId, {})
      .then(result => { if (active) setPage(result) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId])
  async function load(cursor?: string) {
    try {
      setError(null)
      const result = await inventoryApi.search(tenantId, { ...filters, cursor })
      setPage(current => mergePage(current, result, cursor))
    } catch (failure) {
      setError(humanMessage(failure))
    }
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
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }
  if (error && !page) return <MessageState title="Inventory could not be loaded" message={error} />
  if (!page) return <LoadingState label="Loading inventory" />
  return <InventoryWorkbench tenantId={tenantId} page={page} filters={filters}
    error={error} busy={busy} canImport={canImport} canReview={canReview}
    token={session?.antiforgeryToken ?? null} setFilters={setFilters} load={load}
    upload={upload} setBusy={setBusy} setError={setError} />
}

function InventoryWorkbench({ tenantId, page, filters, error, busy, canImport,
  canReview, token, setFilters, load, upload, setBusy, setError }: {
  tenantId: string; page: InventoryProductPage; filters: InventoryFilters
  error: string | null; busy: boolean; canImport: boolean; canReview: boolean
  token: string | null; setFilters: (value: InventoryFilters) => void
  load: (cursor?: string) => Promise<void>; upload: (values: FormData) => Promise<void>
  setBusy: (value: boolean) => void; setError: (value: string | null) => void
}) {
  return <section className="inventory-workbench-page" aria-labelledby="inventory-title">
    <InventoryCatalogueHeader items={page.items} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className={canImport ? 'inventory-workbench-layout' : undefined}>
      <div className="inventory-catalogue-column">
        <InventorySearchForm filters={filters} setFilters={setFilters}
          search={() => void load()} />
        <InventoryProductCards page={page} loadMore={(cursor) => void load(cursor)} />
      </div>
      {canImport && <InventoryUploadForm busy={busy}
        maximumSourceBytes={page.maximumSourceBytes} upload={upload} />}
    </div>
    {canReview &&
      <InventorySemanticPreflightPanel tenantId={tenantId} />}
    {canReview && token && <DuplicateReviewPanel tenantId={tenantId}
      token={token} busy={busy} setBusy={setBusy} reportError={setError} />}
  </section>
}

function mergePage(current: InventoryProductPage | null, next: InventoryProductPage,
  cursor?: string) {
  return cursor && current ? { ...next, items: [...current.items, ...next.items] } : next
}

function DuplicateReviewPanel({ tenantId, token, busy, setBusy, reportError }: {
  tenantId: string; token: string; busy: boolean
  setBusy: (value: boolean) => void
  reportError: (value: string | null) => void
}) {
  const [duplicates, setDuplicates] = useState<InventoryDuplicateCandidate[]>([])
  useEffect(() => { let active = true
    void inventoryApi.listDuplicateCandidates(tenantId)
      .then(result => { if (active) setDuplicates(result) })
      .catch(() => undefined)
    return () => { active = false } }, [tenantId])
  async function review(candidate: InventoryDuplicateCandidate, decision: string,
    canonicalProductId: string | null, reason: string) {
    setBusy(true); reportError(null)
    try {
      await inventoryApi.reviewDuplicate(tenantId, candidate.id, candidate.version,
        { decision, canonicalProductId, reason }, token)
      setDuplicates(current => current.filter(item => item.id !== candidate.id))
      notifications.success('The duplicate review decision was recorded.')
    } catch (failure) { reportError(humanMessage(failure)) } finally { setBusy(false) }
  }
  return <DuplicateReview candidates={duplicates} busy={busy} review={review} />
}

function DuplicateReview({ candidates, busy, review }: {
  candidates: InventoryDuplicateCandidate[]
  busy: boolean
  review: (candidate: InventoryDuplicateCandidate, decision: string,
    canonicalProductId: string | null, reason: string) => Promise<void>
}) {
  const [reasons, setReasons] = useState<Record<string, string>>({})
  if (candidates.length === 0) return null
  return <section className="inventory-record-section"><p className="eyebrow">Identity review</p>
    <h2>Possible duplicate inventory</h2>
    <p>Records remain separate until a human confirms they represent the same product.</p>
    {candidates.map(candidate => <article className="detail-card" key={candidate.id}>
      <h3>{candidate.leftName} / {candidate.rightName}</h3>
      <p>{candidate.method.replaceAll('_', ' ')}{candidate.similarity === null
        ? '' : ` · ${Math.round(candidate.similarity * 100)}% similarity`}</p>
      <label>Review reason<textarea maxLength={2000} value={reasons[candidate.id] ?? ''}
        onChange={event => setReasons(current => ({ ...current,
          [candidate.id]: event.target.value }))} /></label>
      <div><button type="button" disabled={busy || !(reasons[candidate.id] ?? '').trim()}
        onClick={() => void review(candidate, 'CONFIRMED_SAME_IDENTITY',
          candidate.leftProductId, reasons[candidate.id])}>Keep left as canonical</button>
        <button type="button" disabled={busy || !(reasons[candidate.id] ?? '').trim()}
          onClick={() => void review(candidate, 'CONFIRMED_SAME_IDENTITY',
            candidate.rightProductId, reasons[candidate.id])}>Keep right as canonical</button>
        <button type="button" disabled={busy || !(reasons[candidate.id] ?? '').trim()}
          onClick={() => void review(candidate, 'DISMISSED', null,
            reasons[candidate.id])}>Not a duplicate</button></div>
    </article>)}
  </section>
}
