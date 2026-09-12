import type { ShortlistCandidate } from '../api/planning-schemas'
import { humanizeCode } from '../presentation/format'

export function ShortlistDecisionFunnel({ candidates, selectedIds }: {
  candidates: ShortlistCandidate[]
  selectedIds: string[]
}) {
  if (candidates.length === 0) return null
  const eligible = candidates.filter(item => item.isEligible)
  const rejected = candidates.length - eligible.length
  const selected = eligible.filter(item => selectedIds.includes(item.id))
  const suppliers = supplierCount(candidates)
  const selectedSuppliers = supplierCount(selected)
  const reasons = rejectionReasons(candidates)
  return <section className="shortlist-decision-funnel" aria-labelledby="shortlist-funnel-title">
    <header><div><p className="eyebrow">Decision funnel</p>
      <h3 id="shortlist-funnel-title">What Advertified removed before the planner decides</h3>
      <p>{decisionSentence(candidates.length, suppliers, rejected, eligible.length, selected.length)}</p></div></header>
    <div className="shortlist-funnel-steps">
      <FunnelStep label="Considered" value={candidates.length} detail={`${suppliers} supplier${suppliers === 1 ? '' : 's'}`} />
      <span aria-hidden="true">→</span>
      <FunnelStep label="Eligible" value={eligible.length} detail={`${rejected} ruled out`} />
      <span aria-hidden="true">→</span>
      <FunnelStep label="Current selection" value={selected.length} detail={`${selectedSuppliers} supplier${selectedSuppliers === 1 ? '' : 's'}`} />
    </div>
    {reasons.length > 0 && <div className="shortlist-rejection-reasons"><strong>Why options were ruled out</strong>
      <div>{reasons.map(item => <span key={item.code}><b>{item.count}</b>{humanizeCode(item.code, true)}</span>)}</div>
    </div>}
  </section>
}

function FunnelStep({ label, value, detail }: { label: string; value: number; detail: string }) {
  return <article><small>{label}</small><strong>{value}</strong><p>{detail}</p></article>
}

function supplierCount(candidates: ShortlistCandidate[]) {
  return new Set(candidates.map(item => item.supplierId ?? item.inventoryTenantId)).size
}

function rejectionReasons(candidates: ShortlistCandidate[]) {
  const counts = new Map<string, number>()
  candidates.filter(item => !item.isEligible && item.rejectionReason).forEach(item => {
    const code = item.rejectionReason!
    counts.set(code, (counts.get(code) ?? 0) + 1)
  })
  return [...counts.entries()]
    .map(([code, count]) => ({ code, count }))
    .sort((left, right) => right.count - left.count || left.code.localeCompare(right.code))
    .slice(0, 5)
}

function decisionSentence(considered: number, suppliers: number, rejected: number, eligible: number, selected: number) {
  if (rejected === 0) {
    return `Advertified evaluated ${considered} option${considered === 1 ? '' : 's'} across ${suppliers} supplier${suppliers === 1 ? '' : 's'}; all currently pass the hard eligibility rules.`
  }
  return `Advertified evaluated ${considered} option${considered === 1 ? '' : 's'} across ${suppliers} supplier${suppliers === 1 ? '' : 's'}, ruled out ${rejected}, kept ${eligible} eligible and currently carries ${selected} forward.`
}
