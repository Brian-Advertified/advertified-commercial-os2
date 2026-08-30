import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes, type InventoryDecision } from '../api/inventory-constants'
import type { InventoryCandidate, InventoryImport, InventoryValues } from '../api/inventory-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { InventoryCandidateReview } from '../components/InventoryCandidateReview'
import { LoadingState, MessageState } from '../components/PageState'
import { notifications } from '../notifications/notifications'

const reviewRoles = new Set<string>([
  inventoryCodes.role.platformAdmin,
  inventoryCodes.role.inventoryOperations,
])

export function InventoryImportPage() {
  const route = z.guid().safeParse(useParams().importId)
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!route.success) return <MessageState title="Import not found"
    message="Choose an inventory import again." />
  return <ImportRecord key={`${selected.tenantId}-${route.data}`}
    tenantId={selected.tenantId} importId={route.data}
    canReview={reviewRoles.has(selected.roleCode)} />
}

function ImportRecord({ tenantId, importId, canReview }: {
  tenantId: string; importId: string; canReview: boolean
}) {
  const model = useImportRecord(tenantId, importId)
  const actions = useImportActions(tenantId, model)
  const { record, error } = model
  if (error && !record) return <MessageState title="Import could not be loaded" message={error} />
  if (!record) return <LoadingState label="Loading inventory import" />
  return <section aria-labelledby="import-title">
    <Link className="text-action back-link" to="/inventory">← Inventory</Link>
    <header className="page-heading page-heading-split"><div><p className="eyebrow">Source review</p>
      <h1 id="import-title">{record.fileName}</h1>
      <p>{record.supplierName} · SHA-256 {record.sourceHash.slice(0, 12)}…</p>
    </div><span className="status-chip">{record.status.replaceAll('_', ' ')}</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <ImportSummary record={record} />
    <ImportExtractAction tenantId={tenantId} record={record} actions={actions} />
    <div className="candidate-stack">{record.candidates.map((candidate) =>
      <InventoryCandidateReview key={candidate.id} candidate={candidate}
        canReview={canReview && candidate.status === inventoryCodes.candidateStatus.reviewRequired}
        busy={actions.busy} review={actions.review} />)}</div>
    <ImportCompletionActions tenantId={tenantId} record={record}
      model={model} actions={actions} />
  </section>
}

function useImportActions(
  tenantId: string,
  model: ReturnType<typeof useImportRecord>,
) {
  const { session } = useSession()
  const [busy, setBusy] = useState(false)

  async function run(action: (token: string) => Promise<unknown>, success: string) {
    if (!session) return
    setBusy(true); model.setError(null)
    try {
      await action(session.antiforgeryToken)
      notifications.success(success)
      await model.reload()
    } catch (failure) {
      model.setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  async function review(candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) {
    await run((token) => inventoryApi.review(
      tenantId, candidate.id, candidate.version, token, decision, values, reason),
    'The candidate review is recorded.')
  }

  return { busy, run, review }
}

function ImportExtractAction({ tenantId, record, actions }: {
  tenantId: string
  record: InventoryImport
  actions: ReturnType<typeof useImportActions>
}) {
  if (record.status !== inventoryCodes.importStatus.uploaded) return null
  return <button className="primary-button import-action" disabled={actions.busy}
    onClick={() => void actions.run((token) => inventoryApi.execute(tenantId, record, token),
      'The source is extracted and ready for review.')}>Extract candidates</button>
}

function ImportCompletionActions({ tenantId, record, model, actions }: {
  tenantId: string
  record: InventoryImport
  model: ReturnType<typeof useImportRecord>
  actions: ReturnType<typeof useImportActions>
}) {
  const publish = record.status === inventoryCodes.importStatus.reviewRequired &&
    isReadyToPublish(record)
  return <>
    {record.nextCandidateCursor && <button className="secondary-button import-action"
      type="button" disabled={model.loadingMore} onClick={() => void model.loadMore()}>
      {model.loadingMore ? 'Loading more…' : 'Load more candidates'}
    </button>}
    {publish && <section className="next-action-card publish-panel">
      <div><p className="eyebrow eyebrow-light">Publication preview</p>
        <h2>Reviewed products are ready</h2>
        <p>{record.candidateCounts.approved} approved candidate(s) will become
          versioned searchable inventory.</p></div>
      <button className="primary-button" disabled={actions.busy}
        onClick={() => void actions.run((token) => inventoryApi.publish(tenantId, record, token),
          'Reviewed inventory is now searchable.')}>Publish reviewed inventory</button>
    </section>}
  </>
}

const isReadyToPublish = (record: InventoryImport): boolean =>
  record.candidateCounts.approved > 0 &&
  record.candidateCounts.reviewRequired === 0 &&
  record.candidateCounts.blocking === 0

function useImportRecord(tenantId: string, importId: string) {
  const [record, setRecord] = useState<InventoryImport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadingMore, setLoadingMore] = useState(false)
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

  async function loadMore() {
    if (!record?.nextCandidateCursor) return
    setLoadingMore(true); setError(null)
    try {
      const next = await inventoryApi.getImport(
        tenantId, importId, record.nextCandidateCursor)
      setRecord(current => current ? mergeCandidatePage(current, next) : next)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setLoadingMore(false)
    }
  }

  return { record, error, loadingMore, setError, reload, loadMore }
}

function mergeCandidatePage(current: InventoryImport, next: InventoryImport): InventoryImport {
  const candidates = new Map(current.candidates.map(item => [item.id, item]))
  next.candidates.forEach(item => candidates.set(item.id, item))
  return { ...next, candidates: [...candidates.values()] }
}

function ImportSummary({ record }: { record: InventoryImport }) {
  return <article className="detail-card import-summary"><div><span>Protection</span>
    <strong>{record.scanStatus}</strong></div><div><span>Detected type</span>
      <strong>{record.documentClass ?? 'Not classified'}</strong></div><div><span>Size</span>
      <strong>{new Intl.NumberFormat().format(record.sourceSize)} bytes</strong></div>
    <div><span>Candidates</span><strong>{record.candidateCounts.total}</strong></div>
    <div><span>Awaiting review</span><strong>{record.candidateCounts.reviewRequired}</strong></div>
    <div><span>Pipeline</span><strong>{record.steps.length} completed step(s)</strong></div>
    {record.failureCode && <p className="inline-alert">The source was isolated:
      {' '}{record.failureCode.replaceAll('_', ' ')}.</p>}
  </article>
}
