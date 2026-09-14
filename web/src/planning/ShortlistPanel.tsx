import { useState } from 'react'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { InventoryIntelligenceInterpretation, Shortlist, ShortlistCandidate } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'
import { ShortlistSuitability } from './ShortlistSuitability'
import { ShortlistDecisionFunnel } from './ShortlistDecisionFunnel'
import { CampaignCombinations } from './CampaignCombinations'
import { inventoryDecisionContent } from '../reporting/inventory-decision-content'
import '../reporting/inventory-decision-history.css'

const shortlistPageSize = 24

export function ShortlistPanel({ tenantId, token, shortlist, requiredChannels, busy, onConfirm }: {
  tenantId: string
  token: string
  shortlist: Shortlist
  requiredChannels: string[]
  busy: boolean
  onConfirm: (selectedIds: string[], reason: string) => Promise<void>
}) {
  const eligible = shortlist.candidates.filter(item => item.isEligible)
  const [reason, setReason] = useState('')
  const [selected, setSelected] = useState<string[]>(
    eligible.filter(item => item.isSelected === true).map(item => item.id))
  const [interpretations, setInterpretations] = useState<InventoryIntelligenceInterpretation[]>([])
  const [interpreting, setInterpreting] = useState(false)
  const [interpretationError, setInterpretationError] = useState<string | null>(null)
  const editable = shortlist.status === masterDataCodes.lifecycleStatuses.draft
  const coverage = requiredCoverage(
    shortlist.candidates, eligible, selected, requiredChannels,
  )
  function toggle(id: string) {
    setSelected(current => current.includes(id)
      ? current.filter(item => item !== id) : [...current, id])
  }
  async function explain() {
    setInterpreting(true); setInterpretationError(null)
    try {
      const result = await planningApi.explainShortlist(tenantId, shortlist.id, token)
      setInterpretations(result.interpretations)
    } catch (failure) {
      setInterpretationError(humanMessage(failure))
    } finally { setInterpreting(false) }
  }
  const advisory = new Map(interpretations.map(item => [item.candidateId, item]))
  const suppliers = new Set(shortlist.candidates
    .map(item => item.supplierId ?? item.inventoryTenantId)).size
  return <section className="planning-section" aria-labelledby="shortlist-title">
    <ShortlistHeading eligibleCount={eligible.length} totalCount={shortlist.candidates.length}
      suppliers={suppliers} editable={editable} busy={busy} interpreting={interpreting}
      canConfirm={selected.length > 0 && coverage.selectedReady && Boolean(reason.trim())}
      onExplain={explain} onConfirm={() => onConfirm(selected, reason.trim())} />
    <InventoryIntelligenceNotice error={interpretationError} count={interpretations.length} />
    <ShortlistReason editable={editable} reason={reason} setReason={setReason} />
    <CoverageAlerts coverage={coverage} selectedCount={selected.length} />
    <ShortlistDecisionFunnel candidates={shortlist.candidates} selectedIds={selected} />
    <CampaignCombinations shortlist={shortlist} editable={editable} busy={busy} onChoose={setSelected} />
    <CandidateList candidates={shortlist.candidates} eligible={eligible}
      editable={editable} selected={selected} advisory={advisory} onToggle={toggle} />
  </section>
}

function ShortlistHeading({ eligibleCount, totalCount, suppliers, editable, busy, interpreting,
  canConfirm, onExplain, onConfirm }: {
  eligibleCount: number; totalCount: number; suppliers: number; editable: boolean
  busy: boolean; interpreting: boolean; canConfirm: boolean
  onExplain: () => Promise<void>; onConfirm: () => Promise<void>
}) {
  return <div className="planning-section-heading"><div><p className="eyebrow">Inventory</p>
    <h2 id="shortlist-title">Choose the placements to carry forward</h2>
    <p>{eligibleCount} eligible products from {totalCount} considered across {suppliers} supplier{suppliers === 1 ? '' : 's'}. Rejections are available on demand.</p></div>
    <div className="planning-actions"><button className="secondary-button" type="button"
      disabled={busy || interpreting} onClick={() => void onExplain()}>
      {interpreting ? 'Explaining shortlist…' : 'Explain shortlist with AI'}</button>
      {editable && <button className="primary-button" type="button" disabled={busy || !canConfirm}
        onClick={() => void onConfirm()}>Confirm selected inventory</button>}</div></div>
}

function InventoryIntelligenceNotice({ error, count }: { error: string | null; count: number }) {
  if (error) return <p className="rejection-copy" role="alert">{error}</p>
  if (count === 0) return null
  return <p className="inventory-rationale"><strong>AI advisory only:</strong> These explanations restate the governed shortlist and strategy. They do not change eligibility, scores, availability or your selection.</p>
}

function ShortlistReason({ editable, reason, setReason }: {
  editable: boolean; reason: string; setReason: (value: string) => void
}) {
  if (!editable) return null
  return <label className="shortlist-decision-reason">{inventoryDecisionContent.reasonLabel}
    <textarea value={reason} maxLength={2000} onChange={event => setReason(event.target.value)} />
    <small>{inventoryDecisionContent.reasonHelp}</small></label>
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

function CandidateList({ candidates, eligible, editable, selected, advisory, onToggle }: {
  candidates: ShortlistCandidate[]
  eligible: ShortlistCandidate[]
  editable: boolean
  selected: string[]
  advisory: Map<string, InventoryIntelligenceInterpretation>
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
        advisory={advisory.get(candidate.id)} selected={selected.includes(candidate.id)}
        onToggle={() => onToggle(candidate.id)} />)}</div>
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

function CandidateCard({ candidate, editable, selected, advisory, onToggle }: {
  candidate: ShortlistCandidate
  editable: boolean
  selected: boolean
  advisory?: InventoryIntelligenceInterpretation
  onToggle: () => void
}) {
  const visual = mediaVisual(candidate.channel)
  const unitRate = candidate.rateAmountMinor === null || !candidate.currency
    ? null : formatMoney(candidate.rateAmountMinor, candidate.currency)
  const scheduledCost = candidate.suitability?.buyAssessment?.campaignSupplierCostMinor
  const rate = scheduledCost == null
    ? unitRate ?? 'Rate unavailable'
    : `${formatMoney(scheduledCost, candidate.currency ?? 'ZAR')} scheduled${unitRate ? ` · ${unitRate} unit rate` : ''}`
  const eligibility = candidate.isEligible
    ? 'Eligible' : candidate.rejectionReason?.replaceAll('_', ' ')
  return <article className={`shortlist-card media-tone-${visual.tone} ${candidate.isEligible ? '' : 'is-rejected'}`}>
    <div className="shortlist-card-head"><div className="media-identity"><MediaTypeIcon channel={candidate.channel} />
      <div><span>{visual.label}</span><h3>{candidate.name}</h3>
        {candidate.supplierName && <small>{candidate.supplierName}</small>}</div></div>
      <Selection candidate={candidate} editable={editable} selected={selected} onToggle={onToggle} /></div>
    <p>{candidate.geography}</p>
    <div className="shortlist-facts"><span>{rate}</span><span>{eligibility}</span></div>
    <CandidateNarratives candidate={candidate} advisory={advisory} />
    <CommercialDetail candidate={candidate} />
    <ShortlistSuitability candidate={candidate} />
    <PlacementDetail candidate={candidate} />
    <AudienceFitDetail candidate={candidate} />
    <BenchmarkBoundary candidate={candidate} />
  </article>
}

function CandidateNarratives({ candidate, advisory }: {
  candidate: ShortlistCandidate; advisory?: InventoryIntelligenceInterpretation
}) {
  return <>
    {candidate.rejectionDetail && <p className="rejection-copy">{candidate.rejectionDetail}</p>}
    {candidate.rationale && <p className="inventory-rationale">
      <strong>Deterministic recommendation rationale:</strong> {candidate.rationale}</p>}
    {advisory && <p className="inventory-rationale"><strong>AI recommendation:</strong> {advisory.rationale}</p>}
  </>
}

function BenchmarkBoundary({ candidate }: { candidate: ShortlistCandidate }) {
  return candidate.benchmark ? <BenchmarkDetail candidate={candidate} /> : null
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
  </details>
}

function AudienceFitDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const fit = candidate.audienceFit
  if (fit.evidenceGaps.length === 0 && fit.deliveryEvidenceGaps.length === 0) return null
  return <details className="benchmark-detail"><summary>Audience evidence</summary>
    {fit.evidenceGaps.length > 0 && <ul>{fit.evidenceGaps.map(gap => <li key={gap}>{gap}</li>)}</ul>}
    {fit.deliveryEvidenceGaps.length > 0 && <ul>{fit.deliveryEvidenceGaps.map(gap => <li key={gap}>{gap}</li>)}</ul>}
  </details>
}

function BenchmarkDetail({ candidate }: { candidate: ShortlistCandidate }) {
  const benchmark = candidate.benchmark!
  return <details className="benchmark-detail"><summary>Market comparison</summary>
    <p>{benchmark.position.replaceAll('_', ' ')} · {benchmark.geographyBasis.replaceAll('_', ' ')}</p>
    <p>Policy {benchmark.policyVersion.replaceAll('_', ' ')}</p>
  </details>
}

function Selection({ candidate, editable, selected, onToggle }: {
  candidate: ShortlistCandidate
  editable: boolean
  selected: boolean
  onToggle: () => void
}) {
  if (!candidate.isEligible || !editable) return null
  return <label className="shortlist-selection"><input type="checkbox" checked={selected}
    onChange={onToggle} /> Carry forward</label>
}

function commercialGapLabel(value: string) {
  return value.replaceAll('.', ' › ').replaceAll('_', ' ')
}
