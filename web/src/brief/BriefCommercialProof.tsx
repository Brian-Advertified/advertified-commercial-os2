import type { CampaignMode } from '../api/planning-schemas'
import type { BriefVersion, CampaignBrief } from '../api/schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'

export function BriefCommercialProof({ record, version, campaignMode }: {
  record: CampaignBrief
  version: BriefVersion
  campaignMode: CampaignMode | null
}) {
  const unresolved = version.conflicts.filter(item => !item.resolved).length
  const blockingUnknowns = version.unknowns.filter(item => item.isBlocking).length
  const decisions = unresolved + blockingUnknowns + (campaignMode ? 0 : 1)
  const core = [
    version.businessProblem.trim(),
    version.objective.trim(),
    version.audiences.length > 0,
    version.geographies.length > 0,
    version.timing.trim(),
    !version.budgetUnknown && version.budgetMinor !== null,
  ].filter(Boolean).length
  const metrics: CommercialProofMetric[] = [
    {
      label: 'Original evidence retained', value: `${record.sources.length} source${record.sources.length === 1 ? '' : 's'}`,
      detail: 'The original client material stays connected to this Brief.', icon: 'brief', tone: 'violet',
      why: 'This count comes from the source records retained on this Campaign Brief.',
    },
    {
      label: 'Campaign understanding', value: `${core}/6 core inputs`,
      detail: 'Problem, objective, audience, geography, timing and budget are checked before planning.', icon: 'target', tone: core === 6 ? 'positive' : 'blue',
      why: 'A core input counts only when the current retained Brief version contains it; missing information is not inferred here.',
    },
    {
      label: 'Recorded facts', value: version.facts.length,
      detail: 'Facts remain separate from assumptions, unknowns and conflicts.', icon: 'evidence', tone: version.facts.length ? 'positive' : 'neutral',
      why: 'This is the number of facts retained on the current Brief version, not an AI confidence score.',
    },
    {
      label: 'Human attention', value: decisions === 0 ? 'No material blockers' : `${decisions} decision${decisions === 1 ? '' : 's'}`,
      detail: decisions === 0 ? 'The current Brief can move into audience strategy when approved.' : 'Only unresolved material items are counted here.',
      icon: 'shield', tone: decisions === 0 ? 'positive' : 'warning',
      why: 'Blocking unknowns, unresolved conflicts and an unresolved campaign mode are the only items included.',
    },
  ]
  return <CommercialValueProof title="Advertified understood the commercial requirement"
    description="Before media planning starts, the system turns the supplied request into a reviewable decision record and shows exactly what still needs human judgement."
    metrics={metrics} note="No reach, audience size, ROI or savings are claimed unless supporting evidence exists later in the journey." />
}
