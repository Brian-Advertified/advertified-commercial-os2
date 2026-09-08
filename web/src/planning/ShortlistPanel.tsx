import { useState } from 'react'
import type { Shortlist, ShortlistCandidate } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'
import { ShortlistSuitability } from './ShortlistSuitability'
import { CampaignCombinations } from './CampaignCombinations'

const shortlistPageSize = 24

export function ShortlistPanel({ shortlist, requiredChannels, busy, onConfirm }: {
  shortlist: Shortlist
  requiredChannels: string[]
  busy: boolean
  onConfirm: (selectedIds: string[]) => Promise<void>
}) {
  const eligible = shortlist.candidates.filter(item => item.isEligible)
  const [selected, setSelected] = useState<string[]>(
    eligible.filter(item => item.isSelected === true).map(item => item.id))
  const editable = shortlist.status === masterDataCodes.lifecycleStatuses.draft
  const coverage = requiredCoverage(
    shortlist.candidates, eligible, selected, requiredChannels,
  )
  function toggle(id: string) {
    setSelected(current => current.includes(id)
      ? current.filter(item => item !== id) : [...current, id])
  }
  return <section className="planning-section" aria-labelledby="shortlist-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Inventory</p>
      <h2 id="shortlist-title">Choose the placements to carry forward</h2>
      <p>{eligible.length} eligible products from {shortlist.candidates.length} considered. Rejections are available on demand.</p></div>
      {editable && <button className="primary-button" type="button"
        disabled={busy || selected.length === 0 || !coverage.selectedReady}
        onClick={() => void onConfirm(selected)}>Confirm selected inventory</button>}</div>
    <CoverageAlerts coverage={coverage} selectedCount={selected.length} />
    <CampaignCombinations shortlist={shortlist} editable={editable} busy={busy} onChoose={setSelected} />
    <CandidateList candidates={shortlist.candidates} eligible={eligible}
      editable={editable} selected={selected} onToggle={toggle} />
  </section>
}

function CoverageAlerts({ coverage, selectedCount }: {
  coverage: ReturnType<typeof requiredCoverage>
  selectedCount: number
}) {
  return <>
    {coverage.unavailableCount > 0 && <p className="rejection-copy" role="alert">
      <strong>Do not buy:</strong> eligible inventory cannot cover {
        coverage.unavailableCount} required {
        coverage.unavailableCount === 1 ? 'area' : 'areas'
      }. Keep the shortlist unconfirmed until the geography or supply gap is resolved.
    </p>}
    {coverage.unavailableChannelCount > 0 && <p className="rejection-copy" role="alert">
      <strong>Supply gap:</strong> eligible inventory cannot cover {
        coverage.unavailableChannelCount} required {
        coverage.unavailableChannelCount === 1 ? 'media channel' : 'media channels'
      }. Revise the mix or publish suitable inventory before confirming the shortlist.
    </p>}
    {coverage.unavailableCount === 0 && coverage.unavailableChannelCount === 0 &&
      selectedCount > 0 && !coverage.selectedReady &&
      <p className="rejection-copy" role="alert">Select eligible inventory that
        collectively covers every required area and media channel.</p>}
  </>
}

function CandidateList({ candidates, eligible, editable, selected, onToggle }: {
  candidates: ShortlistCandidate[]
  eligible: ShortlistCandidate[]
  editable: boolean
  selected: string[]
  onToggle: (id: string) => void
}) {
  const [showRejected, setShowRejected] = useState(false)
  const [visibleLimit, setVisibleLimit] = useState(shortlistPageSize)
  const ranked = [...(showRejected ? candidates : eligible)].sort(
    (left, right) => (right.suitability?.total ?? -1) - (left.suitability?.total ?? -1),
  )
  const visible = ranked.slice(0, visibleLimit)
  return <>
    <div className="planning-actions">
      <button className="secondary-button" type="button" onClick={() => {
        setShowRejected(current => !current)
        setVisibleLimit(shortlistPageSize)
      }}>{showRejected ? 'Show eligible only' : `Review rejected (${candidates.length - eligible.length})`}</button>
      <span>Showing {visible.length} of {ranked.length}</span>
    </div>
    <div className="shortlist-grid">{visible.map(candidate =>
      <CandidateCard key={candidate.id} candidate={candidate} editable={editable}
        selected={selected.includes(candidate.id)} onToggle={() => onToggle(candidate.id)} />)}</div>
    {visible.length < ranked.length && <button className="secondary-button" type="button"
      onClick={() => setVisibleLimit(current => current + shortlistPageSize)}>Load more inventory</button>}
  </>
}

function requiredCoverage(
  candidates: ShortlistCandidate[],
  eligible: ShortlistCandidate[],
  selectedIds: string[],
  requiredChannels: string[],
) {
  const required = new Set(candidates.flatMap(candidate =>
    candidate.spatialMatch?.requiredRequirementIds ?? []))
  const coverable = new Set(eligible.flatMap(candidate =>
    candidate.spatialMatch?.matchedRequiredRequirementIds ?? []))
  const selectedCandidates = eligible.filter(candidate => selectedIds.includes(candidate.id))
  const selected = new Set(selectedCandidates
    .flatMap(candidate => candidate.spatialMatch?.matchedRequiredRequirementIds ?? []))
  const requiredMediaChannels = new Set(requiredChannels)
  const eligibleChannels = new Set(eligible.map(candidate => candidate.channel))
  const selectedChannels = new Set(selectedCandidates.map(candidate => candidate.channel))
  return {
    unavailableCount: [...required].filter(id => !coverable.has(id)).length,
    unavailableChannelCount: [...requiredMediaChannels]
      .filter(channel => !eligibleChannels.has(channel)).length,
    selectedReady: [...required].every(id => selected.has(id)) &&
      [...requiredMediaChannels].every(channel => selectedChannels.has(channel)),
  }
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
      <strong>Recommendation rationale:</strong> {candidate.rationale}</p>}
    <CommercialDetail candidate={candidate} />
    <ShortlistSuitability candidate={candidate} />
    <PlacementDetail candidate={candidate} />
    <AudienceFitDetail candidate={candidate} />
    {candidate.benchmark && <BenchmarkDetail candidate={candidate} />}
  </article>
}

function CommercialDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const readiness = candidate.commercialReadiness
  if (readiness.evidenceGaps.length > 0) return <div className="rejection-copy">
    <strong>Pricing evidence required</strong>
    <ul>{readiness.evidenceGaps.map(gap => <li key={gap}>{commercialGapLabel(gap)}</li>)}</ul>
  </div>
  return <p>Supplier VAT: {readiness.supplierVatStatus?.replaceAll('_', ' ')} · Rate VAT: {
    readiness.vatTreatment?.replaceAll('_', ' ')}</p>
}

function PlacementDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const deliverable = candidate.deliverable
  const spatial = candidate.spatial
  if (!deliverable && !spatial && !candidate.logoAssetId) return null
  return <details className="benchmark-detail"><summary>Placement evidence</summary>
    {candidate.logoAssetId && <p>Rights-approved supplier logo available.</p>}
    {deliverable && <p>{[deliverable.format, deliverable.buyingUnit, deliverable.dimensions,
      deliverable.placement].filter(Boolean).join(' · ')}</p>}
    {spatial && <p>{[spatial.venue, spatial.road, spatial.route, spatial.trafficDirection]
      .filter(Boolean).join(' · ')}</p>}
    {spatial && spatial.pointsOfInterest.length > 0 && <ul>{spatial.pointsOfInterest.map(poi =>
      <li key={`${poi.name}-${poi.category ?? ''}`}>{poi.name}{poi.category ? ` (${poi.category})` : ''}</li>)}</ul>}
  </details>
}

function commercialGapLabel(gap: string) {
  if (gap === 'inventory.supplierCommercial.vatStatus') return 'Supplier VAT status is not verified.'
  if (gap === 'inventory.rate.vatTreatment') return 'The rate does not state whether VAT is included.'
  if (gap === 'inventory.rate.validity') return 'The published rate has no explicit validity period and requires review.'
  return gap
}

function Selection({ candidate, editable, selected, onToggle }: {
  candidate: ShortlistCandidate; editable: boolean; selected: boolean; onToggle: () => void
}) {
  if (!candidate.isEligible || !editable) return null
  return <input type="checkbox" aria-label={`Select ${candidate.name}`}
    checked={selected} onChange={onToggle} />
}

function AudienceFitDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const fit = candidate.audienceFit
  const scores = [
    ['Language', fit.languageScore],
    ['Life stage', fit.lifeStageScore],
    ['LSM / SEM', fit.lsmSemScore],
  ] as const
  if (fit.evidenceGaps.length === 0 && scores.every(([, value]) => value === null)) {
    return <details className="benchmark-detail"><summary>Audience evidence</summary>
      <p>Audience match needs evidence. No comparable target profile has been supplied.</p>
      <DeliveryMeasurements candidate={candidate} /></details>
  }
  return <details className="benchmark-detail"><summary>Audience fit</summary>
    {fit.evidenceGaps.length > 0
      ? <div><strong>Evidence required</strong><ul>{fit.evidenceGaps.map(gap =>
        <li key={gap}>{audienceGapLabel(gap)}</li>)}</ul></div>
      : <div className="benchmark-facts">{scores.filter(([, value]) => value !== null)
        .map(([label, value]) => <span key={label}><strong>{Math.round(value! * 100)}%</strong> {label}</span>)}</div>}
    {fit.measurementSource && <p>{fit.measurementSource}
      {fit.measurementPeriod ? ` · ${fit.measurementPeriod}` : ''}</p>}
    {fit.methodology && <p>{fit.methodology}</p>}
    {fit.taxonomyName && <p>LSM / SEM taxonomy: {fit.taxonomyName}
      {fit.taxonomyVersion ? ` ${fit.taxonomyVersion}` : ''}</p>}
    <DeliveryMeasurements candidate={candidate} />
  </details>
}

function DeliveryMeasurements({ candidate }: { candidate: ShortlistCandidate }) {
  const fit = candidate.audienceFit
  if (fit.deliveryEvidenceGaps.length > 0) return <div>
    <strong>Delivery evidence required</strong>
    <ul>{fit.deliveryEvidenceGaps.map(gap =>
      <li key={gap}>{audienceGapLabel(gap)}</li>)}</ul>
  </div>
  if (fit.deliveryMeasurements.length === 0) return null
  return <div><p>Published placement measurements; these are not a campaign reach forecast.</p>
    <div className="benchmark-facts">{fit.deliveryMeasurements.map(item =>
      <span key={item.metricType}>
        <strong>{item.value} {item.unit}</strong> {item.metricType.replaceAll('_', ' ')}
        <small>{[item.measurementSource, item.measurementPeriod, item.universe].filter(Boolean).join(' · ')}</small>
        {item.limitations && <small>{item.limitations}</small>}
      </span>)}</div></div>
}

function audienceGapLabel(gap: string) {
  if (gap === 'inventory.audienceProfile') return 'This product has no audience profile.'
  if (gap === 'inventory.audienceProfile.measurementEvidence') {
    return 'Audience source, period and methodology are required.'
  }
  if (gap === 'audience.lsmSem.taxonomy') {
    return 'The product and target need the same named LSM / SEM taxonomy version.'
  }
  if (gap === 'inventory.audienceProfile.deliveryMeasurements') {
    return 'Reach, listenership, footfall or impressions have not been supplied.'
  }
  if (gap === 'inventory.audienceProfile.deliveryMeasurementEvidence') {
    return 'Delivery measurements require value, unit, source, period and methodology.'
  }
  return gap
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
