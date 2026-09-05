import { useEffect, useState } from 'react'
import { inventoryApi } from '../api/inventory-client'
import type { InventorySupplierLifecycle } from '../api/inventory-lifecycle-schemas'
import { inventoryLifecycleCopy as copy } from '../content/inventory-lifecycle-copy'

export function InventoryReleaseSummary({ tenantId, supplierId, approvedCandidates }: {
  tenantId: string; supplierId: string | null; approvedCandidates: number
}) {
  const [state, setState] = useState<{
    key: string; result: InventorySupplierLifecycle | null; failed: boolean
  } | null>(null)
  const key = `${tenantId}:${supplierId}`
  useEffect(() => {
    let active = true
    if (supplierId) {
      void inventoryApi.getSupplierLifecycle(tenantId, supplierId)
        .then(result => { if (active) setState({ key, result, failed: false }) })
        .catch(() => { if (active) setState({ key, result: null, failed: true }) })
    }
    return () => { active = false }
  }, [tenantId, supplierId, key])
  if (!supplierId) return <p role="status">{copy.unresolved}</p>
  if (state?.key !== key) return <p role="status">{copy.loading}</p>
  if (state.failed || !state.result) return <p className="inline-alert" role="status">{copy.unavailable}</p>
  return <section className="approved-reconcile-card" aria-labelledby="replacement-impact-title">
    <header><div><p className="eyebrow">{copy.heading}</p>
      <h2 id="replacement-impact-title">{copy.title}</h2><p>{copy.explanation}</p>
    </div></header>
    <div className="approved-reconcile-grid">
      <article><strong>{state.result.currentProductCount.toLocaleString()}</strong><p>{copy.currentProducts}</p></article>
      <article><strong>{approvedCandidates.toLocaleString()}</strong><p>{copy.approvedCandidates}</p></article>
    </div>
    <p>{copy.impacts}</p>
  </section>
}
