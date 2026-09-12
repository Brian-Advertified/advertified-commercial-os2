import { useState } from 'react'
import { humanMessage } from '../api/client'
import { getInventoryDecisions, type InventoryDecision, type InventoryDecisionReport } from '../api/inventory-decisions-client'
import { formatDateTime } from '../presentation/format'
import { inventoryDecisionContent as copy } from './inventory-decision-content'
import './inventory-decision-history.css'

export function InventoryDecisionHistory({ tenantId, briefVersionId, inventoryProductId }: {
  tenantId: string; briefVersionId?: string; inventoryProductId?: string
}) {
  return <ScopedDecisionHistory key={`${tenantId}:${briefVersionId ?? ''}:${inventoryProductId ?? ''}`}
    tenantId={tenantId} briefVersionId={briefVersionId} inventoryProductId={inventoryProductId} />
}

function ScopedDecisionHistory({ tenantId, briefVersionId, inventoryProductId }: {
  tenantId: string; briefVersionId?: string; inventoryProductId?: string
}) {
  const [report, setReport] = useState<InventoryDecisionReport | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function load(cursor?: string) {
    setBusy(true)
    setError(null)
    try {
      const next = await getInventoryDecisions(tenantId, { briefVersionId, inventoryProductId, cursor })
      setReport(current => mergeDecisionReport(current, next, cursor))
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return <section className="inventory-decision-history" aria-label={copy.title}>
    <header><div><p className="eyebrow">Decision accountability</p><h2>{copy.title}</h2></div>
      {!report && <button className="secondary-button" type="button" disabled={busy} onClick={() => void load()}>
        {busy ? copy.loading : copy.load}</button>}</header>
    <p>{inventoryProductId ? copy.supplierDetail : copy.detail}</p>
    <p className="decision-evidence-note">{copy.source}</p>
    <DecisionHistoryBody report={report} error={error} busy={busy} onLoad={load} />
  </section>
}

function mergeDecisionReport(current: InventoryDecisionReport | null,
  next: InventoryDecisionReport, cursor?: string) {
  return cursor && current ? { ...next, items: [...current.items, ...next.items] } : next
}

function DecisionHistoryBody({ report, error, busy, onLoad }: {
  report: InventoryDecisionReport | null; error: string | null; busy: boolean
  onLoad: (cursor?: string) => Promise<void>
}) {
  if (error) return <p role="alert">{copy.unavailable} {error}</p>
  if (!report) return null
  if (report.items.length === 0) return <p role="status">{copy.empty}</p>
  return <><ol>{report.items.map(item => <DecisionRow
    key={`${item.eventId}-${item.productId}`} item={item} supplierSafe={report.supplierSafe} />)}</ol>
    {report.hasMore && report.nextCursor && <button className="secondary-button" type="button"
      disabled={busy} onClick={() => void onLoad(report.nextCursor ?? undefined)}>
      {busy ? copy.loadingOlder : copy.loadOlder}</button>}</>
}

function DecisionRow({ item, supplierSafe }: { item: InventoryDecision; supplierSafe: boolean }) {
  const removed = item.wasSelected === true && !item.isSelected
  return <li className={removed ? 'decision-removed' : undefined}>
    <div className="decision-row-heading"><h3>{item.productName}</h3><strong>{decisionLabel(item)}</strong></div>
    <p>{formatDateTime(item.decidedAtUtc)}</p>
    <p>{supplierSafe ? copy.privateReason : item.reason ?? copy.missingReason}</p>
    {!item.presentInCurrentShortlist && <p>{copy.omitted}</p>}
    <DecisionReferences item={item} />
  </li>
}

function decisionLabel(item: InventoryDecision) {
  if (!item.isSelected) return item.wasSelected === true ? copy.removed : copy.declined
  if (item.wasSelected === true) return copy.retained
  return item.wasSelected === false ? copy.added : copy.selected
}

function DecisionReferences({ item }: { item: InventoryDecision }) {
  const references = [
    ['Report event', item.eventId], ['Inventory version', item.productVersionId],
    ['Previous selection event', item.previousEventId], ['Previous inventory version', item.previousProductVersionId],
    ['Shortlist version', item.shortlistVersionId], ['Brief version', item.briefVersionId], ['Decision actor', item.actorId],
  ]
  return <details><summary>Exact retained references</summary><dl>
    {references.filter(([, value]) => value !== null).map(([label, value]) =>
      <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
  </dl></details>
}
