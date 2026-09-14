import type { MediaAllocation, Shortlist } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'
import { allocationPeriods } from './planning-presentation'

type Candidate = Shortlist['candidates'][number]

export function MediaPlanRow({ allocation, candidate, currency, onEditFlight, onReplace }: {
  allocation: MediaAllocation
  candidate: Candidate | null
  currency: string
  onEditFlight: () => void
  onReplace?: () => void
}) {
  const model = rowModel(allocation, candidate, currency)
  return <div className="connected-media-plan-row"><span className="connected-plan-select"><input type="checkbox"
    checked={Boolean(candidate)} readOnly aria-label={`Selected ${model.channel}`} /></span>
    <span className="connected-media-cell"><MediaTypeIcon channel={allocation.channel} /><strong>{model.channel}</strong></span>
    <span className="connected-partner-cell"><strong>{model.supplier}</strong><small>{model.placement}</small></span>
    <span><em className={`connected-fit-pill${model.fitValue === null ? ' is-unknown' : model.fitValue >= 80 ? '' : ' is-review'}`}>
      {model.fit}</em></span><span>{model.impressions}</span><strong>{model.cost}</strong>
    <span><button type="button" className="connected-inline-edit" onClick={onEditFlight}>{model.period}</button></span>
    <span className="connected-plan-market">{model.geography}</span>
    <span className="connected-plan-row-actions"><button type="button" aria-label={`More actions for ${model.placement}`}>⋯</button>
      <div>{onReplace && <button type="button" onClick={onReplace}>Replace placement</button>}
        <button type="button" onClick={onEditFlight}>Edit flight</button></div></span></div>
}

function rowModel(allocation: MediaAllocation, candidate: Candidate | null, currency: string) {
  const fitValue = candidateFitValue(candidate)
  return {
    channel: mediaVisual(allocation.channel).label,
    supplier: candidateSupplier(candidate),
    placement: candidate?.name ?? 'Review eligible shortlist',
    fit: fitValue === null ? '—' : `${fitValue}%`,
    fitValue,
    impressions: compactNumber(candidate?.suitability?.buyAssessment?.impressions),
    cost: candidateCost(allocation, candidate, currency),
    period: allocationPeriods(allocation),
    geography: candidate?.geography ?? geographyLabel(allocation),
  }
}

function candidateSupplier(candidate: Candidate | null) {
  if (!candidate) return 'Supply not selected'
  return candidate.supplierName ?? 'Supplier pending'
}

function candidateFitValue(candidate: Candidate | null) {
  const score = candidate?.suitability?.total ?? candidate?.score ?? null
  if (score === null) return null
  return Math.round(score * (score <= 1 ? 100 : 1))
}

function candidateCost(allocation: MediaAllocation, candidate: Candidate | null, currency: string) {
  const scheduled = candidate?.suitability?.buyAssessment?.campaignSupplierCostMinor
  if (scheduled != null) return formatMoney(scheduled, candidate?.currency ?? currency, 0)
  if (candidate?.rateAmountMinor == null) return formatMoney(allocation.budgetMinor, currency, 0)
  return `${formatMoney(candidate.rateAmountMinor, candidate.currency ?? currency, 0)} unit rate`
}

function geographyLabel(allocation: MediaAllocation) {
  const values = allocation.geographyAllocations.map(item => item.geography)
  return values.length ? values.join(', ') : 'Campaign geography'
}

function compactNumber(value: number | null | undefined) {
  if (value == null) return '—'
  if (value >= 1_000_000) return `${Math.round(value / 100_000) / 10}M`
  if (value >= 1_000) return `${Math.round(value / 100) / 10}K`
  return Math.round(value).toLocaleString('en-ZA')
}
