import { useState } from 'react'
import { inventoryCodes, type InventoryDecision } from '../api/inventory-constants'
import type { InventoryCandidate, InventoryValues } from '../api/inventory-schemas'
import { formatMoney } from '../presentation/format'
import { inventoryAcceptanceCopy } from '../content/inventory-acceptance-copy'

export function InventoryCandidateReview({ candidate, canReview, busy, review }: {
  candidate: InventoryCandidate
  canReview: boolean
  busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
  return <article className="detail-card candidate-card">
    <header className="page-heading-split"><div><p className="eyebrow">Source row {candidate.rowNumber}</p>
      <h2>{candidate.values.name ?? 'Product identity missing'}</h2>
      <p>{candidate.sourceLocator}</p></div><span className="status-chip">{candidate.status}</span></header>
    <div className="candidate-facts">
      <Fact label="Code" value={candidate.values.productCode} /><Fact label="Channel" value={candidate.values.channel} />
      <Fact label="Geography" value={candidate.values.geography} />
      <Fact label="Rate" value={money(candidate.values)} />
      <Fact label="Availability" value={candidate.values.availability} />
    </div>
    {candidate.values.extension?.acceptanceevaluation && <details><summary>{inventoryAcceptanceCopy.checks}</summary>
      <pre>{candidate.values.extension.acceptanceevaluation}</pre></details>}
    {candidate.validation.length > 0 && <ul className="validation-list">
      {candidate.validation.map((issue) => <li className={issue.isBlocking ? 'blocking' : ''}
        key={`${issue.fieldName}-${issue.code}`}>{issue.message}</li>)}</ul>}
    <details className="evidence-panel"><summary>View {candidate.evidence.length} source-linked fields</summary>
      <div className="evidence-table">{candidate.evidence.map((field) => <div key={field.fieldName}>
        <strong>{field.fieldName.replaceAll('_', ' ')}</strong><span>{field.rawValue ?? 'Not supplied'}</span>
        <small>{field.sourceLocator} · {field.transformation.toLowerCase().replaceAll('_', ' ')}</small>
      </div>)}</div></details>
    {canReview && <div className="candidate-actions">
      <RejectButton candidate={candidate} busy={busy} review={review} />
    </div>}
  </article>
}

function RejectButton({ candidate, busy, review }: {
  candidate: InventoryCandidate; busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
  const [reason, setReason] = useState<string>(inventoryCodes.rejectionReason.missingInformation)
  return <div className="reject-action"><label>Rejection reason<select value={reason}
    onChange={(event) => setReason(event.target.value)}>
    <option value={inventoryCodes.rejectionReason.missingInformation}>Missing information</option>
    <option value={inventoryCodes.rejectionReason.duplicate}>Duplicate</option>
    <option value={inventoryCodes.rejectionReason.qualityIssue}>Quality issue</option>
    <option value={inventoryCodes.rejectionReason.staleRate}>Stale rate</option></select></label>
    <button className="text-action" type="button" disabled={busy}
      onClick={() => void review(candidate, inventoryCodes.decision.reject, null, reason)}>Reject candidate</button></div>
}

function Fact({ label, value }: { label: string; value: string | null }) {
  return <div><span>{label}</span><strong>{value ?? 'Not supplied'}</strong></div>
}

function money(values: InventoryValues): string | null {
  if (values.rateAmountMinor === null || !values.currency) return null
  return formatMoney(values.rateAmountMinor, values.currency)
}
