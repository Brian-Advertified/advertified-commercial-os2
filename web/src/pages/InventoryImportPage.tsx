import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes, type InventoryDecision } from '../api/inventory-constants'
import type { InventoryCandidate, InventoryImport, InventoryValues } from '../api/inventory-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { notifications } from '../notifications/notifications'
import { InventoryCandidateReview } from '../components/InventoryCandidateReview'

const reviewRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
])

export function InventoryImportPage() {
  const route = z.guid().safeParse(useParams().importId)
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!route.success) return <MessageState title="Import not found" message="Choose an inventory import again." />
  return <ImportRecord key={`${selected.tenantId}-${route.data}`} tenantId={selected.tenantId}
    importId={route.data} canReview={reviewRoles.has(selected.roleCode)} />
}

function ImportRecord({ tenantId, importId, canReview }: {
  tenantId: string; importId: string; canReview: boolean
}) {
  const { session } = useSession()
  const model = useImportRecord(tenantId, importId)
  const [busy, setBusy] = useState(false)

  async function run(action: (token: string) => Promise<unknown>, success: string) {
    if (!session) return
    setBusy(true); model.setError(null)
    try { await action(session.antiforgeryToken); notifications.success(success); await model.reload() }
    catch (failure) { model.setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }

  async function review(
    candidate: InventoryCandidate,
    decision: InventoryDecision,
    values: InventoryValues | null,
    reason: string | null,
  ) {
    await run((token) => inventoryApi.review(
      tenantId, candidate.id, candidate.version, token, decision, values, reason),
    'The candidate review is recorded.')
  }

  const { record, error } = model
  if (error && !record) return <MessageState title="Import could not be loaded" message={error} />
  if (!record) return <LoadingState label="Loading inventory import" />
  const ready = record.candidates.some((item) => item.status === inventoryCodes.candidateStatus.approved) &&
    record.candidates.every((item) => item.status !== inventoryCodes.candidateStatus.reviewRequired)
  return <section aria-labelledby="import-title">
    <Link className="text-action back-link" to="/inventory">← Inventory</Link>
    <header className="page-heading page-heading-split"><div><p className="eyebrow">Source review</p>
      <h1 id="import-title">{record.fileName}</h1><p>{record.supplierName} · SHA-256 {record.sourceHash.slice(0, 12)}…</p>
    </div><span className="status-chip">{record.status.replaceAll('_', ' ')}</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <ImportSummary record={record} />
    {record.status === inventoryCodes.importStatus.uploaded &&
    <button className="primary-button import-action" disabled={busy}
      onClick={() => void run((token) => inventoryApi.execute(tenantId, record, token),
        'The source is extracted and ready for review.')}>Extract candidates</button>}
    <div className="candidate-stack">{record.candidates.map((candidate) =>
      <InventoryCandidateReview key={candidate.id} candidate={candidate}
        canReview={canReview && candidate.status === inventoryCodes.candidateStatus.reviewRequired}
        busy={busy} review={review} />)}</div>
    {ready && record.status === inventoryCodes.importStatus.reviewRequired &&
    <section className="next-action-card publish-panel">
      <div><p className="eyebrow eyebrow-light">Publication preview</p><h2>Reviewed products are ready</h2>
        <p>{record.candidates.filter((item) =>
          item.status === inventoryCodes.candidateStatus.approved).length} approved candidate(s)
          will become versioned searchable inventory.</p></div>
      <button className="primary-button" disabled={busy}
        onClick={() => void run((token) => inventoryApi.publish(tenantId, record, token),
          'Reviewed inventory is now searchable.')}>Publish reviewed inventory</button></section>}
  </section>
}

function useImportRecord(tenantId: string, importId: string) {
  const [record, setRecord] = useState<InventoryImport | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void inventoryApi.getImport(tenantId, importId).then((value) => {
      if (active) setRecord(value)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    })
    return () => { active = false }
  }, [tenantId, importId])
  async function reload() {
    try { setRecord(await inventoryApi.getImport(tenantId, importId)) }
    catch (failure) { setError(humanMessage(failure)) }
  }
  return { record, error, setError, reload }
}

function ImportSummary({ record }: { record: InventoryImport }) {
  return <article className="detail-card import-summary"><div><span>Protection</span>
    <strong>{record.scanStatus}</strong></div><div><span>Detected type</span>
      <strong>{record.documentClass ?? 'Not classified'}</strong></div><div><span>Size</span>
      <strong>{new Intl.NumberFormat().format(record.sourceSize)} bytes</strong></div>
    <div><span>Pipeline</span><strong>{record.steps.length} completed step(s)</strong></div>
    {record.failureCode && <p className="inline-alert">The source was isolated: {record.failureCode.replaceAll('_', ' ')}.</p>}
  </article>
}
