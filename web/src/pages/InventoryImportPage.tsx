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
import { formatDateTime, humanizeCode } from '../presentation/format'

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
  return <section className="inventory-import-page approved-inventory-import" aria-labelledby="import-title">
    <div className="approved-inventory-pagebar"><Link className="text-action" to="/inventory">← Inventory</Link>
      <span>{humanizeCode(record.status, true)}</span></div>
    <header className="approved-import-hero"><div><p className="eyebrow">Inventory Import</p>
      <h1 id="import-title">{record.fileName}</h1><p>{record.supplierName} · original source and hash retained</p></div>
      <dl><div><dt>Protection</dt><dd>{humanizeCode(record.scanStatus, true)}</dd></div>
        <div><dt>Detected type</dt><dd>{record.documentClass ?? 'Pending'}</dd></div>
        <div><dt>Source size</dt><dd>{new Intl.NumberFormat().format(record.sourceSize)} bytes</dd></div>
        <div><dt>Updated</dt><dd>{formatDateTime(record.updatedAtUtc)}</dd></div></dl></header>
    <ApprovedImportSteps record={record} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <ExtractionAttemptPanel tenantId={tenantId} record={record} actions={actions} />
    <div className="approved-import-metrics">
      <Metric label="Rows found" value={record.candidateCounts.total} />
      <Metric label="Needs review" value={record.candidateCounts.reviewRequired} tone="warning" />
      <Metric label="Approved" value={record.candidateCounts.approved} tone="positive" />
      <Metric label="Blocking" value={record.candidateCounts.blocking} tone="danger" />
    </div>
    {record.status === inventoryCodes.importStatus.uploaded
      ? <ApprovedClassifyStage tenantId={tenantId} record={record} actions={actions} />
      : <ApprovedCandidateStage record={record} canReview={canReview} actions={actions} />}
    <div id="publication-action"><ImportCompletionActions tenantId={tenantId} record={record}
      model={model} actions={actions} /></div>
  </section>
}

const retryableExtractionStatuses = new Set<string>([
  inventoryCodes.extractionStatus.failedTerminal,
  inventoryCodes.extractionStatus.timedOut,
  inventoryCodes.extractionStatus.cancelled,
])
const cancellableExtractionStatuses = new Set<string>([
  inventoryCodes.extractionStatus.pending,
  inventoryCodes.extractionStatus.submitting,
  inventoryCodes.extractionStatus.running,
  inventoryCodes.extractionStatus.failedRetryable,
  inventoryCodes.extractionStatus.reconciliationRequired,
])

function ExtractionAttemptPanel({ tenantId, record, actions }: {
  tenantId: string
  record: InventoryImport
  actions: ReturnType<typeof useImportActions>
}) {
  const [reason, setReason] = useState('')
  const [externalTaskId, setExternalTaskId] = useState('')
  const attempt = record.extractionAttempts[0]
  if (!attempt) return null
  return <section className="next-action-card" aria-labelledby="extraction-attempt-title">
    <div><p className="eyebrow">Durable extraction attempt {attempt.attemptNumber}</p>
      <h2 id="extraction-attempt-title">{humanizeCode(attempt.status, true)}</h2>
      <p>Provider {attempt.providerName} {attempt.providerVersion} · task {attempt.externalTaskId ?? 'not durably identified'}</p>
      <p>Last checkpoint {attempt.lastPolledAtUtc ? formatDateTime(attempt.lastPolledAtUtc) : 'not polled'} · {attempt.providerErrorCode ?? attempt.failureClassification ?? 'no recorded failure'}</p>
      {attempt.reconciliationNotes && <p>{attempt.reconciliationNotes}</p>}
      <label className="field-group">Operator reason<input value={reason}
        onChange={event => setReason(event.target.value)} /></label>
      {attempt.status === inventoryCodes.extractionStatus.reconciliationRequired &&
        <label className="field-group">
        Confirmed Docling task ID<input value={externalTaskId}
          onChange={event => setExternalTaskId(event.target.value)} /></label>}
    </div><ExtractionAttemptActions tenantId={tenantId} record={record} actions={actions}
      reason={reason} externalTaskId={externalTaskId} />
  </section>
}

function ExtractionAttemptActions({ tenantId, record, actions, reason, externalTaskId }: {
  tenantId: string
  record: InventoryImport
  actions: ReturnType<typeof useImportActions>
  reason: string
  externalTaskId: string
}) {
  const attempt = record.extractionAttempts[0]
  if (!attempt) return null
  const act = (request: (token: string) => Promise<unknown>, message: string) =>
    void actions.run(request, message)
  return <div className="button-row">
      <button className="secondary-button" type="button" onClick={() => void actions.run(
        async () => record, 'Extraction state refreshed.')}>Refresh</button>
      {retryableExtractionStatuses.has(attempt.status) && <button className="primary-button"
        disabled={actions.busy || !reason.trim()} onClick={() => act(token =>
          inventoryApi.retryExtraction(tenantId, record, token, reason),
        'A new extraction attempt is queued for the same source.')}>Retry as new attempt</button>}
      {attempt.status === inventoryCodes.extractionStatus.reconciliationRequired &&
        <button className="secondary-button"
        disabled={actions.busy || !reason.trim()} onClick={() => act(token =>
          inventoryApi.reconcileExtraction(tenantId, record, token, reason,
            externalTaskId.trim() || null), 'The reconciliation decision is recorded.')}>Reconcile</button>}
      {cancellableExtractionStatuses.has(attempt.status) && <button className="secondary-button"
        disabled={actions.busy || !reason.trim()} onClick={() => act(token =>
          inventoryApi.cancelExtraction(tenantId, record, token, reason),
        'The extraction attempt is terminal and retained in history.')}>Mark unrecoverable</button>}
    </div>
}

function ApprovedImportSteps({ record }: { record: InventoryImport }) {
  const steps = [
    ['Upload Protection', 'Protect source'],
    ['Classification', 'Classify & Render'],
    ['Extraction', 'Extract Candidates'],
    ['Normalization', 'Normalize'],
    ['Validation', 'Validate & Reconcile'],
    ['Review', 'Human Review'],
    ['Publication', 'Publish Inventory'],
  ] as const
  const completed = new Set(record.steps.filter(step => step.completedAtUtc).map(step => step.stepType.toUpperCase()))
  const activeIndex = Math.max(0, steps.findIndex(([code]) => !completed.has(code.replaceAll(' ', '_').toUpperCase())))
  return <ol className="approved-import-stepbar">{steps.map(([code, label], index) =>
    <li key={code} className={completed.has(code.replaceAll(' ', '_').toUpperCase()) ? 'is-complete' : index === activeIndex ? 'is-active' : ''}>
      <span>{completed.has(code.replaceAll(' ', '_').toUpperCase()) ? '✓' : index + 1}</span><strong>{label}</strong></li>)}</ol>
}

function Metric({ label, value, tone = 'neutral' }: { label: string; value: number; tone?: 'neutral' | 'warning' | 'positive' | 'danger' }) {
  return <article className={`approved-import-metric tone-${tone}`}><span>{label}</span><strong>{new Intl.NumberFormat().format(value)}</strong></article>
}

function ApprovedClassifyStage({ tenantId, record, actions }: {
  tenantId: string
  record: InventoryImport
  actions: ReturnType<typeof useImportActions>
}) {
  return <section className="approved-classify-stage">
    <article className="approved-source-preview"><header><h2>Classify & Render</h2><span>Layout preserved</span></header>
      <div className="approved-document-preview"><aside>{[1,2,3,4].map(page => <span key={page}>{page}</span>)}</aside>
        <div><p className="approved-document-title">{record.supplierName.toUpperCase()}</p><h3>{record.fileName}</h3>
          <table><tbody><tr><td>Source hash</td><td>{record.sourceHash.slice(0, 14)}…</td></tr>
            <tr><td>Declared type</td><td>{record.declaredMediaType}</td></tr><tr><td>Detected class</td><td>{record.documentClass ?? 'Pending classification'}</td></tr>
            <tr><td>Protection</td><td>{humanizeCode(record.scanStatus, true)}</td></tr></tbody></table></div></div>
    </article>
    <article className="approved-source-structure"><header><h2>Detected document</h2></header><dl>
      <div><dt>Supplier</dt><dd>{record.supplierName}</dd></div>
      <div><dt>Document class</dt><dd>{record.documentClass ?? 'To be detected'}</dd></div>
      <div><dt>Integrity</dt><dd>Original source retained</dd></div>
      <div><dt>Next action</dt><dd>Extract candidate commercial facts</dd></div></dl>
      <button className="primary-button" disabled={actions.busy}
        onClick={() => void actions.run((token) => inventoryApi.execute(tenantId, record, token), 'The source is extracted and ready for review.')}>
        {actions.busy ? 'Extracting…' : 'Extract candidates →'}</button></article>
  </section>
}

function ApprovedCandidateStage({ record, canReview, actions }: {
  record: InventoryImport
  canReview: boolean
  actions: ReturnType<typeof useImportActions>
}) {
  return <>
    <section className="approved-candidate-table-card"><header><div><h2>Extract Candidates</h2>
      <p>{record.candidateCounts.total.toLocaleString()} candidate record{record.candidateCounts.total === 1 ? '' : 's'} extracted with source coordinates.</p></div>
      <span>Field confidence and validation retained</span></header>
      {record.candidates.length === 0 ? <p className="approved-empty">No candidate rows are available in this page yet.</p> :
        <div className="approved-candidate-table"><div className="approved-candidate-head"><span>Product</span><span>Channel</span><span>Location</span><span>Rate</span><span>Availability</span><span>Issues</span></div>
          {record.candidates.slice(0, 12).map(candidate => <div key={candidate.id}><strong>{candidate.values.name ?? candidate.values.productCode ?? `Row ${candidate.rowNumber}`}</strong>
            <span>{candidate.values.channel ?? 'Unknown'}</span><span>{candidate.values.geography ?? 'Unknown'}</span>
            <span>{candidate.values.rateAmountMinor !== null && candidate.values.currency ? formatCandidateMoney(candidate.values.rateAmountMinor, candidate.values.currency) : 'Missing'}</span>
            <span className="approved-availability-pill">{candidate.values.availability ?? 'Unknown'}</span>
            <em className={candidate.validation.some(issue => issue.isBlocking) ? 'is-blocking' : ''}>{candidate.validation.length}</em></div>)}</div>}
    </section>
    <section className="approved-reconcile-card"><header><div><h2>Validate & Reconcile</h2><p>Deterministic validation separates safe normalization from material review.</p></div></header>
      <div className="approved-reconcile-grid"><article><strong>Resolved</strong><p>Safe transformations and proven duplicate matches can resolve automatically.</p></article>
        <article><strong>Comparable</strong><p>Same product context, but commercial terms differ. Preserve both and compare.</p></article>
        <article><strong>Conflict</strong><p>Materially incompatible evidence stays unresolved until reviewed.</p></article></div></section>
    <section className="inventory-candidate-ledger approved-human-review" id="candidate-review"><header><div><p className="eyebrow">Human Review</p><h2>Review only the exceptions</h2></div><span>{record.candidateCounts.reviewRequired} awaiting review</span></header>
      {record.candidates.length === 0 ? <p className="inventory-candidate-empty">No candidates have been extracted from this source yet.</p> :
        <div className="candidate-stack">{record.candidates.map(candidate => <InventoryCandidateReview key={candidate.id} candidate={candidate}
          canReview={canReview && candidate.status === inventoryCodes.candidateStatus.reviewRequired} busy={actions.busy} review={actions.review} />)}</div>}
    </section>
  </>
}

function formatCandidateMoney(amountMinor: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 0 }).format(amountMinor / 100)
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
