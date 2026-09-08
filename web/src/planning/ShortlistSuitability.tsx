import type { ShortlistCandidate } from '../api/planning-schemas'
import { suitabilityContent } from './suitability-content'

export function ShortlistSuitability({ candidate }: { candidate: ShortlistCandidate }) {
  const score = candidate.suitability
  if (!score) return <p>{suitabilityContent.unassessed}</p>
  const spatial = candidate.spatialMatch
  const audience = candidate.audienceFit
  const hasAudience = [audience.languageScore, audience.lifeStageScore, audience.lsmSemScore]
    .some(value => value !== null) && audience.evidenceGaps.length === 0
  return <details className="benchmark-detail"><summary>{suitabilityContent.title}</summary>
    <p>{suitabilityContent.explanation}</p>
    <div className="benchmark-facts">
      <EvidenceFact label="Geographic requirement match" value={score.geography}
        known={!!spatial?.hasRequirements && spatial.evidenceGaps.length === 0} />
      <EvidenceFact label="Supplied audience profile match" value={score.audienceContext} known={hasAudience} />
      <EvidenceFact label="Commercial information completeness" value={score.evidenceQualityFreshness}
        known={candidate.commercialReadiness.evidenceGaps.length === 0} />
    </div>
    <div className="rejection-copy"><strong>{suitabilityContent.missingTitle}</strong>
      <ul>{suitabilityContent.uncomputed.map(item => <li key={item.label}>
        <strong>{item.label}: {suitabilityContent.needsEvidence}.</strong> {item.detail}</li>)}</ul>
    </div>
    {spatial?.hasRequirements && <p>{spatial.matchedRequiredRequirementIds.length} of {
      spatial.requiredRequirementIds.length} required areas matched.</p>}
    <p>{suitabilityContent.reachCaveat}</p>
    <small>Screening policy: {score.policyVersion}</small>
  </details>
}

function EvidenceFact({ label, value, known }: { label: string; value: number; known: boolean }) {
  return <span><strong>{known ? `${Math.round(value * 100)} / 100` : suitabilityContent.needsEvidence}</strong> {label}</span>
}
