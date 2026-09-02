import { useEffect, useState } from 'react'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProduct, InventorySemanticRecall } from '../api/inventory-schemas'

export function SemanticDuplicateRecall({ tenantId, token, record, canNominate,
  canBackfill, onUpdated }: {
  tenantId: string; token: string; record: InventoryProduct
  canNominate: boolean; canBackfill: boolean
  onUpdated: (value: InventoryProduct) => void
}) {
  const [peers, setPeers] = useState<InventorySemanticRecall[]>([])
  const [reasons, setReasons] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState<string | null>(null)
  const [forceBackfill, setForceBackfill] = useState(false)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void inventoryApi.semanticRecall(tenantId, record.product.id)
      .then(value => { if (active) setPeers(value) })
      .catch(failure => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [record.product.id, record.product.version, tenantId])
  async function generate() {
    setBusy('embedding'); setError(null)
    try {
      await inventoryApi.submitEmbedding(
        tenantId, record.product.id, record.productVersionId,
        record.product.version, forceBackfill, token)
      onUpdated(await inventoryApi.getProduct(tenantId, record.product.id))
      setForceBackfill(false)
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(null) }
  }
  async function nominate(peer: InventorySemanticRecall) {
    const reason = reasons[peer.productId]?.trim()
    if (!reason) return
    setBusy(peer.productId); setError(null)
    try {
      await inventoryApi.nominateSemanticDuplicate(
        tenantId, record.product.id, record.product.version,
        { productVersionId: record.productVersionId, peerProductId: peer.productId,
          peerProductVersionId: peer.productVersionId, reason }, token)
      setPeers(current => current.filter(item => item.productId !== peer.productId))
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(null) }
  }
  return <article><header><h2>Semantic duplicate recall</h2></header>
    {canNominate && <EmbeddingControl busy={busy} canBackfill={canBackfill}
      forceBackfill={forceBackfill} setForceBackfill={setForceBackfill}
      generate={generate} />}
    {peers.length === 0 && <p>No current semantic peers require nomination.</p>}
    {peers.map(peer => <div key={peer.productId} className="product-fact">
      <p><strong>{peer.name}</strong> · {Math.round(peer.similarity * 100)}% recall similarity</p>
      {canNominate && <><label>Nomination reason<input maxLength={2000}
        value={reasons[peer.productId] ?? ''}
        onChange={event => setReasons(current => ({ ...current,
          [peer.productId]: event.target.value }))} /></label>
      <button type="button" disabled={busy !== null || !(reasons[peer.productId] ?? '').trim()}
        onClick={() => void nominate(peer)}>Send to duplicate review</button></>}
    </div>)}
    {error && <p role="alert">{error}</p>}
  </article>
}

function EmbeddingControl({ busy, canBackfill, forceBackfill, setForceBackfill,
  generate }: {
  busy: string | null; canBackfill: boolean; forceBackfill: boolean
  setForceBackfill: (value: boolean) => void; generate: () => Promise<void>
}) {
  const label = busy === 'embedding' ? 'Generating…' :
    forceBackfill ? 'Run explicit backfill' : 'Generate semantic index'
  return <div><button type="button" disabled={busy !== null}
    onClick={() => void generate()}>{label}</button>
    {canBackfill && <label><input type="checkbox" checked={forceBackfill}
      onChange={event => setForceBackfill(event.target.checked)} />
      Explicitly regenerate an unchanged index</label>}
    <small>Generation uses canonical non-personal inventory text and the governed cost cap.</small>
  </div>
}
