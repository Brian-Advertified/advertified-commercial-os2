import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryProduct } from '../api/inventory-schemas'
import { useWorkspace } from '../auth/workspace-state'
import { useSession } from '../auth/session-state'
import { InventoryBenchmarkSection } from '../components/InventoryBenchmarkSection'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { InventoryAudienceProfile } from '../inventory/InventoryAudienceProfile'
import { SemanticDuplicateRecall } from '../inventory/SemanticDuplicateRecall'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'

export function InventoryProductPage() {
  const route = z.guid().safeParse(useParams().productId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!route.success) return <MessageState title="Product not found" message="Choose an inventory product again." />
  if (!session) return <LoadingState />
  const canUpload = uploadRoles.has(selected.roleCode)
  const canReview = reviewRoles.has(selected.roleCode)
  const canReviewRights = rightsReviewRoles.has(selected.roleCode)
  return <ProductRecord tenantId={selected.tenantId} productId={route.data}
    token={session.antiforgeryToken}
    canUpload={canUpload} canReview={canReview} canReviewRights={canReviewRights}
    canBackfill={selected.roleCode === inventoryCodes.role.platformAdmin}
    roleCode={selected.roleCode} />
}

const uploadRoles = new Set<string>([inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations, inventoryCodes.role.supplierAdmin])
const reviewRoles = new Set<string>([inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations])
const rightsReviewRoles = new Set<string>([inventoryCodes.role.platformAdmin,
  inventoryCodes.role.supplierAdmin])

function ProductRecord({ tenantId, productId, token, canUpload, canReview,
  canReviewRights, canBackfill, roleCode }: {
  tenantId: string; productId: string; token: string; canUpload: boolean
  canReview: boolean; canReviewRights: boolean; canBackfill: boolean; roleCode: string
}) {
  const [record, setRecord] = useState<InventoryProduct | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { let active = true; void inventoryApi.getProduct(tenantId, productId)
    .then((value) => { if (active) setRecord(value) })
    .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false } }, [tenantId, productId])
  if (error) return <MessageState title="Product could not be loaded" message={error} />
  if (!record) return <LoadingState label="Loading inventory product" />
  return <ProductRecordView tenantId={tenantId} productId={productId} record={record}
    token={token} canUpload={canUpload} canReview={canReview}
    canReviewRights={canReviewRights} canBackfill={canBackfill}
    roleCode={roleCode} onUpdated={setRecord} />
}

function ProductRecordView({ tenantId, productId, record, token, canUpload, canReview,
  canReviewRights, canBackfill, roleCode, onUpdated }: {
  tenantId: string
  productId: string
  record: InventoryProduct
  token: string
  canUpload: boolean
  canReview: boolean
  canReviewRights: boolean
  canBackfill: boolean
  roleCode: string
  onUpdated: (value: InventoryProduct) => void
}) {
  const item = record.product
  const approvedImage = record.assets.find(asset => internalPlanningEligible(asset))
  return <section className="inventory-record-page approved-inventory-detail" aria-labelledby="product-title">
    <ProductHeading record={record} />
    <nav className="approved-product-tabs"><a href="#product-overview" className="is-active">Overview</a>
      <a href="#audience-profile">Audience</a><a href="#source-evidence">Evidence</a>
      <a href="#market-comparison">Benchmark</a></nav>
    <div className="approved-product-detail-grid" id="product-overview">
      <section className="approved-product-media">{approvedImage
        ? <img src={`/api/v1/tenants/${tenantId}/inventory-assets/${approvedImage.assetId}/content`}
          alt={`${item.supplierName} ${approvedImage.assetType.replaceAll('_', ' ')}`} />
        : <div className="approved-empty">Rights-approved product imagery is not supplied.</div>}
        <dl><Fact label="Supplier" value={item.supplierName} /><Fact label="Product code" value={item.productCode} />
          <Fact label="Product type" value={humanizeCode(item.productType, true)} /><Fact label="Geography" value={item.geography} />
          <Fact label="Address" value={record.address ?? 'Not supplied'} /><Fact label="Coordinates" value={coordinates(record)} /></dl></section>
      <section className="approved-product-commercial"><article><header><h2>Rate & validity</h2><span className="approved-availability-pill">{humanizeCode(record.availability.status, true)}</span></header>
        <strong className="approved-product-rate">{formatMoney(record.rate.amountMinor, record.rate.currency)}</strong><small>{humanizeCode(record.rate.rateType, true)}</small>
        <dl><Fact label="Rate source" value={record.rate.sourceLocator} /><Fact label="Published" value={formatDateTime(record.publishedAtUtc)} />
          <Fact label="Availability observed" value={record.availability.observedAtUtc ? formatDateTime(record.availability.observedAtUtc) : 'Not supplied'} />
          <Fact label="Availability valid until" value={record.availability.validUntilUtc ? formatDateTime(record.availability.validUntilUtc) : 'Not supplied'} />
          <Fact label="Rate VAT treatment" value={record.rate.vatTreatment ? humanizeCode(record.rate.vatTreatment, true) : 'Not supplied'} /></dl>
        {record.availability.status === inventoryCodes.availability.unknown && <p className="approved-reconfirm-note">⚠ Confirm availability before booking.</p>}
      </article>
      <article><header><h2>Commercial history</h2></header><div className="approved-rate-history">
        <div><span>Current published rate</span><strong>{formatMoney(record.rate.amountMinor, record.rate.currency)}</strong></div>
        <div><span>Current basis</span><strong>{humanizeCode(record.rate.rateType, true)}</strong></div>
        <div><span>Verification</span><strong>{humanizeCode(item.verification, true)}</strong></div></div></article></section>
      <aside className="approved-product-intelligence">
        <div id="market-comparison"><InventoryBenchmarkSection tenantId={tenantId} productId={productId} channel={item.channel} /></div>
        <SemanticDuplicateRecall tenantId={tenantId} token={token} record={record}
          canNominate={canReview} canBackfill={canBackfill} onUpdated={onUpdated} />
        <article className="approved-evidence-timeline"><header><h2>Recent evidence & freshness</h2></header>
          <ul><li>Rate source retained</li><li>Inventory candidate reviewed</li><li>Published to catalogue</li>
            {record.availability.observedAtUtc && <li>Availability observed</li>}</ul></article>
        <article className="approved-product-quick-actions"><header><h2>Quick actions</h2></header><a href="#market-comparison">View benchmark</a><a href="#source-evidence">View evidence trail</a><Link to="/inventory">Return to catalogue</Link></article>
      </aside>
    </div>
    <StructuredInventory record={record} />
    <AvailabilityExceptions tenantId={tenantId} token={token} record={record}
      canManage={canReview} onUpdated={onUpdated} />
    <InventoryAudienceProfile profile={record.audienceProfile} />
    <ProductSourceEvidence tenantId={tenantId} token={token} record={record}
      canUpload={canUpload} canReviewRights={canReviewRights} roleCode={roleCode}
      onUpdated={onUpdated} />
  </section>
}

function ProductHeading({ record }: { record: InventoryProduct }) {
  const item = record.product
  return <><div className="approved-inventory-pagebar">
    <Link className="text-action" to="/inventory">← Back to catalogue</Link>
    <div><button className="primary-button" type="button" disabled
      title="Choose inventory from an approved campaign mix.">Use in shortlist</button>
      <a className="secondary-button" href="#source-evidence">Open evidence</a></div></div>
    <header className="approved-product-title"><div>
      <span>{humanizeCode(item.channel, true)}</span>
      <span>{humanizeCode(item.productType, true)}</span></div>
      <h1 id="product-title">{item.name}</h1><p>{item.geography}</p></header></>
}

function AvailabilityExceptions({ tenantId, token, record, canManage, onUpdated }: {
  tenantId: string; token: string; record: InventoryProduct; canManage: boolean
  onUpdated: (value: InventoryProduct) => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    setBusy(true); setError(null)
    try {
      await inventoryApi.recordAvailabilityException(
        tenantId, record.product.id, record.product.version, {
          productVersionId: record.productVersionId,
          exceptionType: String(form.get('exceptionType')),
          startsOn: String(form.get('startsOn')), endsOn: String(form.get('endsOn')),
          sourceLocator: String(form.get('sourceLocator')).trim(),
          evidenceHash: String(form.get('evidenceHash')).trim().toLowerCase(),
        }, token)
      onUpdated(await inventoryApi.getProduct(tenantId, record.product.id))
      formElement.reset()
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(false) }
  }
  return <section className="inventory-record-section"><p className="eyebrow">Availability</p>
    <h2>Explicit not-available periods</h2>
    {record.availabilityExceptions.length === 0
      ? <p>No exception overlaps are recorded. This product is planning-available by default.</p>
      : <ul>{record.availabilityExceptions.map(item => <li key={item.id}>
          {humanizeCode(item.exceptionType, true)}: {item.startsOn} to {item.endsOn}
          {' · '}{item.sourceLocator}</li>)}</ul>}
    {canManage && <form onSubmit={submit}><label>Exception type<select name="exceptionType">
      {Object.values(masterDataCodes.availabilityExceptionTypes).map(code =>
        <option key={code} value={code}>{humanizeCode(code, true)}</option>)}</select></label>
      <label>Starts on<input name="startsOn" type="date" required /></label>
      <label>Ends on<input name="endsOn" type="date" required /></label>
      <label>Evidence reference<input name="sourceLocator" maxLength={1000} required /></label>
      <label>Evidence SHA-256<input name="evidenceHash" maxLength={64}
        pattern="[A-Fa-f0-9]{64}" required /></label>
      <button type="submit" disabled={busy}>{busy ? 'Recording…' : 'Record exception'}</button>
    </form>}
    {error && <p role="alert">{error}</p>}
  </section>
}

function StructuredInventory({ record }: { record: InventoryProduct }) {
  return <section className="inventory-record-section"><p className="eyebrow">Structured inventory</p>
    <h2>Deliverable, location and commercial facts</h2>
    <dl><SupplierCommercialFacts record={record} />
      <DeliveryAndLocationFacts record={record} /></dl>
  </section>
}

function SupplierCommercialFacts({ record }: { record: InventoryProduct }) {
  const { vatStatus, vatNumber, paymentTerms, cancellationTerms } =
    record.supplierCommercial ?? {}
  const rateCancellation = record.rate.commercialTerms?.cancellationTerms
  return <><Fact label="Supplier VAT status" value={vatStatus
    ? humanizeCode(vatStatus, true) : 'Not supplied'} />
  <Fact label="Supplier VAT number" value={vatNumber ?? 'Not supplied'} />
  <Fact label="Payment terms" value={paymentTerms ?? 'Not supplied'} />
  <Fact label="Cancellation terms" value={cancellationTerms ??
    rateCancellation ?? 'Not supplied'} />
  <Fact label="Supplier contacts" value={record.supplierContacts.map(item =>
    item.name ?? item.email ?? item.phone).filter(Boolean).join(', ') || 'Not supplied'} /></>
}

function DeliveryAndLocationFacts({ record }: { record: InventoryProduct }) {
  const { format, buyingUnit, dimensions, placement } = record.deliverable ?? {}
  const { route, road, trafficDirection, pointsOfInterest = [] } = record.spatial ?? {}
  return <><Fact label="Deliverable" value={joinFacts(
    format, buyingUnit, dimensions, placement)} />
  <Fact label="Route / direction" value={joinFacts(route, road, trafficDirection)} />
  <Fact label="POIs" value={pointsOfInterest.map(item => item.name)
    .join(', ') || 'Not supplied'} />
  <Fact label="Packages" value={record.packages.map(item => item.name).join(', ') || 'None'} /></>
}

function joinFacts(...values: Array<string | null | undefined>) {
  return values.filter(Boolean).join(' · ') || 'Not supplied'
}

function ProductSourceEvidence({ tenantId, token, record, canUpload, canReviewRights,
  roleCode, onUpdated }: {
  tenantId: string; token: string; record: InventoryProduct
  canUpload: boolean; canReviewRights: boolean; roleCode: string
  onUpdated: (value: InventoryProduct) => void
}) {
  return <section className="inventory-record-section source-lineage" id="source-evidence">
    <p className="eyebrow">Source lineage</p><h2>Why this product is trusted</h2>
    <p>Published {formatDateTime(record.publishedAtUtc)} from its retained supplier source.</p>
    {canUpload && <AssetUpload tenantId={tenantId} token={token} record={record}
      onUpdated={onUpdated} />}
    <div className="inventory-asset-ledger">{record.assets.map((asset) =>
      <details key={asset.contentHash}><summary>{asset.assetType.replaceAll('_', ' ')}</summary>
        <p>File-integrity evidence: SHA-256 {asset.contentHash}</p>
        <p>Rights: {asset.rightsStatus ? humanizeCode(asset.rightsStatus, true) : 'Not reviewed'}.
          {asset.rightsBasis ? ` ${asset.rightsBasis}` : ''}</p>
        {asset.assetId && canReviewRights && <AssetRightsReview tenantId={tenantId}
          token={token} productId={record.product.id} asset={asset}
          attestorRole={roleCode} onUpdated={onUpdated} />}
      </details>)}</div>
  </section>
}

function AssetUpload({ tenantId, token, record, onUpdated }: {
  tenantId: string; token: string; record: InventoryProduct
  onUpdated: (value: InventoryProduct) => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    const source = form.get('source')
    if (!(source instanceof File) || source.size === 0) return
    setBusy(true); setError(null)
    try {
      await inventoryApi.uploadAsset(
        tenantId, record.product.id, record.productVersionId, record.product.version,
        String(form.get('assetType')), source, token)
      onUpdated(await inventoryApi.getProduct(tenantId, record.product.id))
      formElement.reset()
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(false) }
  }
  const types = [masterDataCodes.assetTypes.logo, masterDataCodes.assetTypes.productImage,
    masterDataCodes.assetTypes.oohPhoto]
  return <form onSubmit={submit}><label>Asset type<select name="assetType">
    {types.map(code => <option value={code} key={code}>{humanizeCode(code, true)}</option>)}
    </select></label><label>PNG or JPEG<input name="source" type="file"
      accept=".png,.jpg,.jpeg,image/png,image/jpeg" required /></label>
    <button type="submit" disabled={busy}>{busy ? 'Uploading…' : 'Upload asset'}</button>
    <small>Upload does not grant usage rights. A separate authorised attestor must record written permission.</small>
    {error && <p role="alert">{error}</p>}
  </form>
}

function AssetRightsReview({ tenantId, token, productId, asset, attestorRole, onUpdated }: {
  tenantId: string; token: string; productId: string
  asset: InventoryProduct['assets'][number]
  attestorRole: string
  onUpdated: (value: InventoryProduct) => void
}) {
  const [basis, setBasis] = useState(asset.rightsBasis ?? '')
  const [scopes, setScopes] = useState<string[]>(asset.rightsScopes ?? [])
  const [effectiveOn, setEffectiveOn] = useState(asset.effectiveOn ?? '')
  const [licensedUntil, setLicensedUntil] = useState(asset.licensedUntil ?? '')
  const [untilRevoked, setUntilRevoked] = useState(asset.untilRevoked)
  const [evidenceReference, setEvidenceReference] = useState('')
  const [evidenceHash, setEvidenceHash] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const form = { basis, scopes, effectiveOn, licensedUntil, untilRevoked,
    attestorRole, evidenceReference, evidenceHash }
  async function review(status: AssetRightsStatus) {
    if (!asset.assetId) return
    const input = buildRightsInput(status, form)
    if (!input) return
    setBusy(true); setError(null)
    try {
      await inventoryApi.reviewAssetRights(tenantId, asset.assetId, asset.rightsVersion,
        input, token)
      onUpdated(await inventoryApi.getProduct(tenantId, productId))
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(false) }
  }
  return <div><label>Rights basis<input value={basis} maxLength={1000}
    onChange={event => setBasis(event.target.value)} /></label>
    <fieldset><legend>Permitted use</legend>{Object.values(inventoryCodes.assetRightsScope)
      .map(scope => <label key={scope}><input type="checkbox" checked={scopes.includes(scope)}
        onChange={event => setScopes(current => event.target.checked
          ? [...new Set([...current, scope])] : current.filter(item => item !== scope))} />
        {humanizeCode(scope, true)}</label>)}</fieldset>
    <label>Effective on<input type="date" value={effectiveOn}
      onChange={event => setEffectiveOn(event.target.value)} /></label>
    <label><input type="checkbox" checked={untilRevoked}
      onChange={event => setUntilRevoked(event.target.checked)} />Valid until revoked</label>
    {!untilRevoked && <label>Licensed until<input type="date" value={licensedUntil}
      onChange={event => setLicensedUntil(event.target.value)} /></label>}
    <p>Attestor role: {humanizeCode(attestorRole, true)}</p>
    <label>Written permission reference<input value={evidenceReference} maxLength={1000}
      onChange={event => setEvidenceReference(event.target.value)} /></label>
    <label>Permission SHA-256<input value={evidenceHash} maxLength={64}
      pattern="[A-Fa-f0-9]{64}" onChange={event => setEvidenceHash(event.target.value)} /></label>
    <RightsActions busy={busy} canApprove={canApproveRights(form)}
      hasEvidence={hasWrittenRightsEvidence(form)} review={review} />
    {error && <p role="alert">{error}</p>}</div>
}

type AssetRightsStatus = typeof inventoryCodes.assetRights[
  keyof typeof inventoryCodes.assetRights]

type RightsForm = {
  basis: string; scopes: string[]; effectiveOn: string; licensedUntil: string
  untilRevoked: boolean; attestorRole: string; evidenceReference: string
  evidenceHash: string
}

function buildRightsInput(status: AssetRightsStatus, form: RightsForm) {
  const approving = status === inventoryCodes.assetRights.approved
  if (!hasWrittenRightsEvidence(form) || approving && !canApproveRights(form)) return null
  return { rightsStatus: status, rightsBasis: form.basis.trim(),
    licensedUntil: form.untilRevoked ? null : form.licensedUntil || null,
    scopeCodes: approving ? form.scopes : [], territoryCode: 'ZA',
    effectiveOn: approving ? form.effectiveOn : null,
    untilRevoked: form.untilRevoked, attestorRole: form.attestorRole,
    evidenceReference: form.evidenceReference.trim(),
    evidenceHash: form.evidenceHash.toLowerCase() }
}

function hasWrittenRightsEvidence(form: RightsForm) {
  return Boolean(form.basis.trim() && form.evidenceReference.trim() &&
    /^[a-f\d]{64}$/i.test(form.evidenceHash))
}

function canApproveRights(form: RightsForm) {
  return hasWrittenRightsEvidence(form) && form.scopes.length > 0 &&
    Boolean(form.effectiveOn) && (form.untilRevoked || Boolean(form.licensedUntil))
}

function RightsActions({ busy, canApprove, hasEvidence, review }: {
  busy: boolean; canApprove: boolean; hasEvidence: boolean
  review: (status: AssetRightsStatus) => Promise<void>
}) {
  return <><button type="button" disabled={busy || !canApprove}
    onClick={() => void review(inventoryCodes.assetRights.approved)}>Approve rights</button>
    <button type="button" disabled={busy || !hasEvidence}
      onClick={() => void review(inventoryCodes.assetRights.restricted)}>Restrict use</button>
    <button type="button" disabled={busy || !hasEvidence}
      onClick={() => void review(inventoryCodes.assetRights.revoked)}>Revoke rights</button></>
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div className="product-fact"><dt>{label}</dt><dd>{value}</dd></div>
}

function coordinates(record: InventoryProduct) {
  return record.latitude === null || record.longitude === null
    ? 'Not supplied'
    : `${record.latitude}, ${record.longitude}`
}

function internalPlanningEligible(asset: InventoryProduct['assets'][number]) {
  const today = new Date().toISOString().slice(0, 10)
  return Boolean(asset.assetId && asset.rightsStatus === inventoryCodes.assetRights.approved &&
    asset.rightsScopes.includes(inventoryCodes.assetRightsScope.internalPlanning) &&
    asset.territoryCode === 'ZA' && asset.effectiveOn && asset.effectiveOn <= today &&
    (asset.untilRevoked || Boolean(asset.licensedUntil && asset.licensedUntil >= today)))
}
