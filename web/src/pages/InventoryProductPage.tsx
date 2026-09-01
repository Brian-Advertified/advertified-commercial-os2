import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryProduct } from '../api/inventory-schemas'
import { useWorkspace } from '../auth/workspace-state'
import { InventoryBenchmarkSection } from '../components/InventoryBenchmarkSection'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'

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
  return <ProductRecordView tenantId={tenantId} productId={productId} record={record} />
}

function ProductRecordView({ tenantId, productId, record }: {
  tenantId: string
  productId: string
  record: InventoryProduct
}) {
  const item = record.product
  return <section className="inventory-record-page" aria-labelledby="product-title">
    <Link className="text-action back-link" to="/inventory">← Inventory</Link>
    <ProductHeader record={record} />
    <nav className="inventory-record-tabs" aria-label="Inventory product sections">
      <a href="#product-overview">Product facts</a>
      <a href="#market-comparison">Market comparison</a>
      <a href="#source-evidence">Source evidence</a>
    </nav>
    <dl className="inventory-record-strip">
      <Metric label="Channel" value={humanizeCode(item.channel, true)} />
      <Metric label="Rate" value={formatMoney(record.rate.amountMinor, record.rate.currency)} />
      <Metric label="Rate basis" value={humanizeCode(record.rate.rateType, true)} />
      <Metric label="Availability" value={humanizeCode(record.availability.status, true)} />
    </dl>
    <ProductOverview record={record} />
    <div id="market-comparison" className="inventory-record-anchor">
      <InventoryBenchmarkSection tenantId={tenantId} productId={productId} channel={item.channel} />
    </div>
    <ProductSourceEvidence record={record} />
  </section>
}

function ProductHeader({ record }: { record: InventoryProduct }) {
  const item = record.product
  return <header className="inventory-record-header">
    <span className="inventory-record-icon"><MediaTypeIcon channel={item.channel} /></span>
    <div><p className="eyebrow">{humanizeCode(item.channel, true)} inventory</p>
      <h1 id="product-title">{item.name}</h1><p>{item.supplierName} · {item.productCode}</p></div>
    <div className="inventory-record-state">
      <span className="status-chip">{humanizeCode(item.verification, true)}</span>
      <small>Version {record.versionNumber}</small>
    </div>
  </header>
}

function ProductOverview({ record }: { record: InventoryProduct }) {
  const item = record.product
  return <div className="inventory-record-columns" id="product-overview">
    <section className="inventory-record-section"><p className="eyebrow">Placement</p>
      <h2>Product and location</h2><dl className="product-fact-list">
        <Fact label="Product type" value={humanizeCode(item.productType, true)} />
        <Fact label="Geography" value={item.geography} />
        <Fact label="Address" value={record.address ?? 'Not supplied'} />
        <Fact label="Coordinates" value={coordinates(record)} />
      </dl>
    </section>
    <section className="inventory-record-section"><p className="eyebrow">Commercial truth</p>
      <h2>Rate and availability evidence</h2><dl className="product-fact-list">
        <Fact label="Published rate" value={formatMoney(record.rate.amountMinor, record.rate.currency)} />
        <Fact label="Rate type" value={humanizeCode(record.rate.rateType, true)} />
        <Fact label="Availability" value={humanizeCode(record.availability.status, true)} />
        <Fact label="Observed" value={record.availability.observedAtUtc
          ? formatDateTime(record.availability.observedAtUtc) : 'Not supplied'} />
      </dl>
      {record.availability.status === inventoryCodes.availability.unknown &&
        <p className="inline-alert">Confirm availability before booking.</p>}
    </section>
  </div>
}

function ProductSourceEvidence({ record }: { record: InventoryProduct }) {
  return <section className="inventory-record-section source-lineage" id="source-evidence">
    <p className="eyebrow">Source lineage</p><h2>Why this product is trusted</h2>
    <p>Published {formatDateTime(record.publishedAtUtc)} from its retained supplier source.</p>
    <div className="inventory-asset-ledger">{record.assets.map((asset) =>
      <details key={asset.contentHash}><summary>{asset.assetType.replaceAll('_', ' ')}</summary>
        <p>File-integrity evidence: SHA-256 {asset.contentHash}</p></details>)}</div>
  </section>
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div className="product-fact"><dt>{label}</dt><dd>{value}</dd></div>
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function coordinates(record: InventoryProduct) {
  return record.latitude === null || record.longitude === null
    ? 'Not supplied'
    : `${record.latitude}, ${record.longitude}`
}
