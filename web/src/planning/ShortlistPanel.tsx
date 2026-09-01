import { useState } from 'react'
import type { Shortlist, ShortlistCandidate } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'

export function ShortlistPanel({ shortlist, busy, onConfirm }: {
  shortlist: Shortlist
  busy: boolean
  onConfirm: (selectedIds: string[]) => Promise<void>
}) {
  const eligible = shortlist.candidates.filter(item => item.isEligible)
  const [selected, setSelected] = useState<string[]>(
    eligible.filter(item => item.isSelected === true).map(item => item.id))
  const editable = shortlist.status === masterDataCodes.lifecycleStatuses.draft
  function toggle(id: string) {
    setSelected(current => current.includes(id)
      ? current.filter(item => item !== id) : [...current, id])
  }
  return <section className="planning-section" aria-labelledby="shortlist-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Inventory</p>
      <h2 id="shortlist-title">Choose the placements to carry forward</h2>
      <p>{eligible.length} eligible products from {shortlist.candidates.length} considered. Rejections remain visible.</p></div>
      {editable && <button className="primary-button" type="button" disabled={busy || selected.length === 0}
        onClick={() => void onConfirm(selected)}>Confirm selected inventory</button>}</div>
    <div className="shortlist-grid">{shortlist.candidates.map(candidate =>
      <CandidateCard key={candidate.id} candidate={candidate} editable={editable}
        selected={selected.includes(candidate.id)} onToggle={() => toggle(candidate.id)} />)}</div>
  </section>
}

function CandidateCard({ candidate, editable, selected, onToggle }: {
  candidate: ShortlistCandidate
  editable: boolean
  selected: boolean
  onToggle: () => void
}) {
  const visual = mediaVisual(candidate.channel)
  const rate = candidate.rateAmountMinor === null || !candidate.currency
    ? 'Rate unavailable' : formatMoney(candidate.rateAmountMinor, candidate.currency)
  const eligibility = candidate.isEligible
    ? 'Eligible' : candidate.rejectionReason?.replaceAll('_', ' ')
  return <article className={`shortlist-card media-tone-${visual.tone} ${candidate.isEligible ? '' : 'is-rejected'}`}>
    <div className="shortlist-card-head"><div className="media-identity"><MediaTypeIcon channel={candidate.channel} />
      <div><span>{visual.label}</span><h3>{candidate.name}</h3></div></div>
      <Selection candidate={candidate} editable={editable} selected={selected} onToggle={onToggle} /></div>
    <p>{candidate.geography}</p>
    <div className="shortlist-facts"><span>{rate}</span><span>{eligibility}</span></div>
    {candidate.rejectionDetail && <p className="rejection-copy">{candidate.rejectionDetail}</p>}
    {candidate.rationale && <p className="inventory-rationale">
      <strong>Inventory Intelligence:</strong> {candidate.rationale}</p>}
    {candidate.benchmark && <BenchmarkDetail candidate={candidate} />}
  </article>
}

function Selection({ candidate, editable, selected, onToggle }: {
  candidate: ShortlistCandidate; editable: boolean; selected: boolean; onToggle: () => void
}) {
  if (!candidate.isEligible || !editable) return null
  return <input type="checkbox" aria-label={`Select ${candidate.name}`}
    checked={selected} onChange={onToggle} />
}

function BenchmarkDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const benchmark = candidate.benchmark
  if (!benchmark) return null
  return <details className="benchmark-detail"><summary>Market comparison</summary>
    <div className="benchmark-facts"><span><strong>{benchmark.cohortSize}</strong> comparable sites</span>
      <span><strong>{benchmark.position.replaceAll('_', ' ')}</strong> market position</span>
      <span><strong>{benchmark.percentile ?? '—'}</strong> price percentile</span>
      <span><strong>{Math.round(benchmark.confidence * 100)}%</strong> benchmark confidence</span></div>
    <p>{benchmark.geographyBasis.replaceAll('_', ' ')}</p></details>
}
