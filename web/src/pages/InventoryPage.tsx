import { useEffect, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryProductPage } from '../api/inventory-schemas'
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
import { notifications } from '../notifications/notifications'
import { formatMiB } from '../presentation/format'

const importRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
  inventoryCodes.role.supplierAdmin,
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
    canImport={importRoles.has(selected.roleCode)} />
}

function InventoryIndex({ tenantId, canImport }: { tenantId: string; canImport: boolean }) {
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
      setPage(current => cursor && current
        ? { ...result, items: [...current.items, ...result.items] }
        : result)
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
  </section>
}
