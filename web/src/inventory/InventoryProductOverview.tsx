import type { InventoryProduct } from '../api/inventory-schemas'
import { inventoryCodes } from '../api/inventory-constants'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'
import { inventoryArtwork, inventoryIdentityLabel } from './inventoryArtwork'

export function ProductCommercial({ record }: { record: InventoryProduct }) {
  const rateAmount = productRateAmount(record)
  const rateType = productRateType(record)
  return <section className="approved-product-commercial"><article><header>
    <h2>Rate & validity</h2><span className="approved-availability-pill">
      {humanizeCode(record.availability.status, true)}</span></header>
    <strong className="approved-product-rate">{rateAmount}</strong><small>{rateType}</small>
    <dl><Fact label="Rate source" value={record.rate?.sourceLocator ?? 'No price supplied in source'} />
      <Fact label="Published" value={formatDateTime(record.publishedAtUtc)} />
      <Fact label="Availability observed" value={optionalDate(record.availability.observedAtUtc)} />
      <Fact label="Availability valid until" value={optionalDate(record.availability.validUntilUtc)} />
      <Fact label="Rate VAT treatment" value={productVatTreatment(record)} /></dl>
    {record.availability.status === inventoryCodes.availability.unknown &&
      <p className="approved-reconfirm-note">⚠ Confirm availability before booking.</p>}
  </article><article><header><h2>Commercial history</h2></header>
    <div className="approved-rate-history">
      <div><span>Current published rate</span><strong>{record.rate ? rateAmount : 'Not supplied'}</strong></div>
      <div><span>Current basis</span><strong>{record.rate ? rateType : 'Request quote'}</strong></div>
      <div><span>Verification</span><strong>{humanizeCode(record.product.verification, true)}</strong></div>
    </div></article></section>
}

export function ProductMedia({ tenantId, record }: {
  tenantId: string
  record: InventoryProduct
}) {
  const item = record.product
  const approvedImage = record.assets.find(asset => internalPlanningEligible(asset))
  const artwork = inventoryArtwork(item)
  const media = approvedImage
    ? <img src={`/api/v1/tenants/${tenantId}/inventory-assets/${approvedImage.assetId}/content`}
      alt={`${item.supplierName} ${approvedImage.assetType.replaceAll('_', ' ')}`} />
    : artwork ? <img className="is-logo" src={artwork} alt={`${item.name} logo`} />
    : <div className="approved-empty"><MediaTypeIcon channel={item.channel} />
      <strong>{inventoryIdentityLabel(item)}</strong></div>
  return <section className="approved-product-media">{media}<dl>
    <Fact label="Supplier" value={item.supplierName} />
    <Fact label="Product code" value={item.productCode} />
    <Fact label="Product type" value={humanizeCode(item.productType, true)} />
    <Fact label="Geography" value={item.geography} />
    <Fact label="Address" value={record.address ?? 'Not supplied'} />
    <Fact label="Coordinates" value={coordinates(record)} />
  </dl></section>
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div className="product-fact"><dt>{label}</dt><dd>{value}</dd></div>
}

function productRateAmount(record: InventoryProduct) {
  return record.rate ? formatMoney(record.rate.amountMinor, record.rate.currency)
    : 'Request supplier quote'
}

function productRateType(record: InventoryProduct) {
  return record.rate ? humanizeCode(record.rate.rateType, true) : 'Price not supplied'
}

function optionalDate(value: string | null) {
  return value ? formatDateTime(value) : 'Not supplied'
}

function productVatTreatment(record: InventoryProduct) {
  return record.rate?.vatTreatment ? humanizeCode(record.rate.vatTreatment, true)
    : 'Not applicable until quoted'
}

function coordinates(record: InventoryProduct) {
  const latitude = record.latitude
  const longitude = record.longitude
  return latitude === null || longitude === null
    ? 'Not supplied' : `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`
}

function internalPlanningEligible(asset: InventoryProduct['assets'][number]) {
  const today = new Date().toISOString().slice(0, 10)
  return Boolean(asset.assetId && asset.rightsStatus === inventoryCodes.assetRights.approved &&
    asset.rightsScopes.includes(inventoryCodes.assetRightsScope.internalPlanning) &&
    asset.territoryCode === 'ZA' && asset.effectiveOn && asset.effectiveOn <= today &&
    (asset.untilRevoked || Boolean(asset.licensedUntil && asset.licensedUntil >= today)))
}
