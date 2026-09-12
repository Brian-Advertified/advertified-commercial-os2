import type { PlanningWorkspace, Shortlist, ShortlistCandidate } from '../api/planning-schemas'
import { humanizeCode } from '../presentation/format'
import './planning-measurement-readiness.css'

export function PlanningMeasurementReadiness({ workspace, shortlist }: {
  workspace: PlanningWorkspace
  shortlist: Shortlist | null
}) {
  if (!shortlist) return null
  const context = workspace.decisionContext
  const eligible = shortlist.candidates.filter(item => item.isEligible)
  const selected = eligible.filter(item => item.isSelected === true)
  const pool = selected.length > 0 ? selected : eligible
  const measured = pool.filter(hasDeliveryMeasurement)
  const metrics = distinctMetrics(measured)
  const sources = distinctSources(measured)
  const gaps = pool.filter(item => item.audienceFit.deliveryEvidenceGaps.length > 0).length
  const success = context?.successMeasures ?? []
  return <section className="planning-measurement-readiness" aria-labelledby="measurement-readiness-title">
    <header><div><p className="eyebrow">Measurement readiness</p>
      <h2 id="measurement-readiness-title">Can this plan prove what happened?</h2>
      <p>{readinessSentence(pool.length, measured.length, gaps, selected.length > 0)}</p></div>
      <span className={`status-chip ${measured.length === pool.length && pool.length > 0 ? 'status-positive' : ''}`}>
        {measured.length}/{pool.length} with delivery evidence</span></header>
    <div className="planning-measurement-grid">
      <article><small>The Brief says success means</small>
        {success.length > 0 ? <ul>{success.map(item => <li key={item}>{item}</li>)}</ul> :
          <p>No explicit business success measure is retained.</p>}</article>
      <article><small>The media can currently evidence</small>
        {metrics.length > 0 ? <><strong>{metrics.join(' · ')}</strong>
          <p>{sources.length > 0 ? `Sources: ${sources.join(' · ')}` : 'Measurement sources are not named.'}</p></> :
          <p>No placement delivery measurement is retained for this supply yet.</p>}</article>
      <article><small>Before this becomes a measurement claim</small>
        <p>{nextMeasurementAction(success, pool.length, measured.length)}</p></article>
    </div>
    <footer>Supplier reach, listenership, footfall or impression evidence can support media-delivery reporting. It does not by itself prove the client’s business outcome or causality.</footer>
  </section>
}

function hasDeliveryMeasurement(candidate: ShortlistCandidate) {
  return candidate.audienceFit.deliveryMeasurements.some(item => item.value !== null)
}

function distinctMetrics(candidates: ShortlistCandidate[]) {
  return [...new Set(candidates.flatMap(item => item.audienceFit.deliveryMeasurements
    .filter(metric => metric.value !== null)
    .map(metric => humanizeCode(metric.metricType, true))))]
}

function distinctSources(candidates: ShortlistCandidate[]) {
  return [...new Set(candidates.flatMap(item => item.audienceFit.deliveryMeasurements
    .map(metric => metric.measurementSource).filter((value): value is string => Boolean(value))))]
    .slice(0, 4)
}

function readinessSentence(pool: number, measured: number, gaps: number, selected: boolean) {
  const scope = selected ? 'selected placements' : 'eligible supply'
  if (pool === 0) return 'No eligible supply exists yet, so measurement readiness cannot be assessed.'
  if (measured === pool) return `All ${pool} ${scope} carry at least one supplied delivery measurement. Business-outcome tracking still remains separate.`
  return `${measured} of ${pool} ${scope} carry supplied delivery measurements; ${gaps} currently expose explicit delivery-evidence gaps.`
}

function nextMeasurementAction(success: string[], pool: number, measured: number) {
  if (success.length === 0) return 'Define the client’s business success measure before the proposal is treated as measurement-ready.'
  if (pool === 0) return 'Resolve the supply gap before designing the final measurement approach.'
  if (measured < pool) return 'Close placement delivery-evidence gaps and retain the source, period, methodology and limitations.'
  return `Retain an outcome data source that can assess “${success[0]}” independently from supplier delivery metrics.`
}
