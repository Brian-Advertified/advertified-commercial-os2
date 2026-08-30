import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryProduct } from '../api/inventory-schemas'
import { useWorkspace } from '../auth/workspace-state'
import { InventoryBenchmarkSection } from '../components/InventoryBenchmarkSection'
import { LoadingState, MessageState } from '../components/PageState'
import { formatMoney } from '../presentation/format'

export function InventoryProductPage() {
  const route = z.guid().safeParse(useParams().productId)
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!route.success) return <MessageState title="Product not found" message="Choose an inventory product again." />
  return <ProductRecord tenantId={selected.tenantId} productId={route.data} />
}

function ProductRecord({ tenantId, productId }: { tenantId: string; productId: string }) {
  const [record, setRecord] = useState<InventoryProduct | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { let active = true; void inventoryApi.getProduct(tenantId, productId)
    .then((value) => { if (active) setRecord(value) })
    .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false } }, [tenantId, productId])
  if (error) return <MessageState title="Product could not be loaded" message={error} />
  if (!record) return <LoadingState label="Loading inventory product" />
  const item = record.product
  return <section aria-labelledby="product-title"><Link className="text-action back-link" to="/inventory">← Inventory</Link>
    <header className="page-heading page-heading-split"><div><p className="eyebrow">{item.channel} · version {record.versionNumber}</p>
      <h1 id="product-title">{item.name}</h1><p>{item.supplierName} · {item.productCode}</p></div>
      <span className="status-chip">{item.verification.replaceAll('_', ' ')}</span></header>
    <div className="product-detail-grid"><article className="detail-card"><h2>Placement</h2>
      <Fact label="Product type" value={item.productType} /><Fact label="Geography" value={item.geography} />
      <Fact label="Address" value={record.address ?? 'Not supplied'} />
      <Fact label="Coordinates" value={record.latitude === null ? 'Not supplied' : `${record.latitude}, ${record.longitude}`} />
    </article><article className="detail-card"><h2>Commercial facts</h2>
      <Fact label="Rate" value={formatMoney(
        record.rate.amountMinor, record.rate.currency, 2)} />
      <Fact label="Rate type" value={record.rate.rateType} />
      <Fact label="Availability" value={record.availability.status} />
      {record.availability.status === inventoryCodes.availability.unknown &&
      <p className="inline-alert">Confirm availability before booking.</p>}
    </article></div>
    <InventoryBenchmarkSection tenantId={tenantId} productId={productId} channel={item.channel} />
    <article className="detail-card source-lineage"><p className="eyebrow">Source lineage</p><h2>Why this product is trusted</h2>
      <p>Published {new Date(record.publishedAtUtc).toLocaleString()} from import {record.sourceImportId}.</p>
      {record.assets.map((asset) => <details key={asset.contentHash}><summary>{asset.assetType.replaceAll('_', ' ')}</summary>
        <p>{asset.sourceReference} · SHA-256 {asset.contentHash}</p></details>)}</article>
  </section>
}

function Fact({ label, value }: { label: string; value: string }) {
  return <p className="product-fact"><span>{label}</span><strong>{value}</strong></p>
}
