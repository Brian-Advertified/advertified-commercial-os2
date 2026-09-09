import type { AudienceSet } from '../api/planning-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'

export function AudienceCommercialProof({ audience, approved }: {
  audience: AudienceSet | null
  approved: boolean
}) {
  if (!audience) return null
  const evidenceBacked = audience.definitions.filter(item => item.evidenceItemIds.length > 0).length
  const geographies = new Set(audience.definitions.flatMap(item => item.geographies).filter(Boolean))
  const targets = audience.targetAudienceIds.length
  const metrics: CommercialProofMetric[] = [
    {
      label: 'Audience candidates', value: audience.definitions.length,
      detail: 'Distinct audience hypotheses are available for comparison rather than one generic target.', icon: 'users', tone: 'violet',
      why: 'This is the number of persisted audience definitions in the current audience-set version.',
    },
    {
      label: 'Evidence connected', value: `${evidenceBacked}/${audience.definitions.length}`,
      detail: evidenceBacked === audience.definitions.length ? 'Every candidate has retained supporting evidence.' : 'Candidates without retained evidence remain explicit hypotheses.',
      icon: 'evidence', tone: evidenceBacked === audience.definitions.length ? 'positive' : 'warning',
      why: 'An audience counts here only when it references one or more retained evidence items.',
    },
    {
      label: 'Geographic signals', value: geographies.size,
      detail: 'Distinct supplied or evidenced audience geographies are carried into planning.', icon: 'globe', tone: geographies.size ? 'blue' : 'neutral',
      why: 'This count is deduplicated from the geography values on the current audience definitions.',
    },
    {
      label: approved ? 'Approved targets' : 'Recommended targets', value: targets,
      detail: approved ? 'These audiences now govern the media-planning step.' : 'The recommendation remains editable until a human approves it.',
      icon: 'target', tone: approved ? 'positive' : 'blue',
      why: approved ? 'These are the target audience IDs retained on the approved audience set.' : 'These are the current persisted recommendations; review can still change their roles.',
    },
  ]
  return <CommercialValueProof title="Audience decisions are now explainable"
    description="Advertified separates audience hypotheses from evidence, exposes buying context and geography, and keeps the final targeting decision human-owned."
    metrics={metrics} note="Open any audience card to review its need state, buying context, structured profile, evidence position and exclusions before approving it." />
}
