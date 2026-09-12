import type { OpportunityDetail } from '../api/schemas'
import { opportunityCodes } from '../api/opportunity-constants'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'

type SelectedAngle = OpportunityDetail['angles'][number] | undefined

export function OpportunityCommercialProof({ detail }: { detail: OpportunityDetail }) {
  const selectedAngle = detail.angles.find(item => item.status === opportunityCodes.angleStatus.selected)
  const unresolved = detail.strategy?.objections.filter(item => !item.resolution).length ?? 0
  const metrics = commercialMetrics(detail, selectedAngle, unresolved)
  return <CommercialValueProof title="Advertified turned evidence into a campaign opportunity"
    description={opportunityNarrative(detail, selectedAngle?.title, unresolved)} metrics={metrics}
    note="This summary proves retained commercial work. It does not claim that the opportunity will convert, generate revenue or outperform alternatives." />
}

function commercialMetrics(detail: OpportunityDetail, selectedAngle: SelectedAngle, unresolved: number): CommercialProofMetric[] {
  return [evidenceMetric(detail), angleMetric(detail, selectedAngle), strategyMetric(detail, unresolved), conversionMetric(detail)]
}

function evidenceMetric(detail: OpportunityDetail): CommercialProofMetric {
  const sourceLabel = plural(detail.sources.length, 'source')
  const claimLabel = plural(detail.evidenceItems.length, 'retained claim')
  return {
    label: 'Evidence base', value: sourceLabel,
    detail: `${claimLabel} available for commercial interpretation.`,
    icon: 'evidence', tone: detail.sources.length > 0 ? 'violet' : 'neutral',
    why: 'Counts come directly from retained opportunity evidence records; source volume is not treated as evidence quality.',
  }
}

function angleMetric(detail: OpportunityDetail, selectedAngle: SelectedAngle): CommercialProofMetric {
  const hasAngles = detail.angles.length > 0
  return {
    label: 'Commercial angles', value: detail.angles.length,
    detail: selectedAngle ? `Selected direction: ${selectedAngle.title}` : 'No commercial angle has been selected yet.',
    icon: 'target', tone: selectedAngle ? 'positive' : hasAngles ? 'blue' : 'neutral',
    why: selectedAngle?.rationale ?? 'Angles remain alternatives until a retained selection exists.',
  }
}

function strategyMetric(detail: OpportunityDetail, unresolved: number): CommercialProofMetric {
  if (!detail.strategy) return {
    label: 'Strategy challenge', value: 'Not created',
    detail: 'Strategy has not yet been created from this opportunity.', icon: 'shield', tone: 'neutral',
  }
  return {
    label: 'Strategy challenge', value: unresolved > 0 ? `${unresolved} unresolved` : 'Clear',
    detail: 'The strategy has been challenged against retained critic objections.',
    icon: 'shield', tone: unresolved > 0 ? 'warning' : 'positive',
    why: 'Only critic objections without a retained resolution are counted as unresolved.',
  }
}

function conversionMetric(detail: OpportunityDetail): CommercialProofMetric {
  if (!detail.briefId) return {
    label: 'Campaign conversion', value: 'Opportunity only',
    detail: 'The opportunity remains pre-Brief and has not entered media planning.',
    icon: 'brief', tone: 'neutral', why: 'No linked Brief identifier exists yet.',
  }
  return {
    label: 'Campaign conversion', value: 'Brief created',
    detail: 'The commercial opportunity has become a governed campaign Brief.',
    icon: 'brief', tone: 'positive', why: 'A canonical Brief identifier is linked to this opportunity.',
  }
}

function opportunityNarrative(detail: OpportunityDetail, selectedAngle: string | undefined, unresolved: number) {
  if (detail.briefId) return convertedNarrative(detail, selectedAngle, unresolved)
  if (selectedAngle) return selectedNarrative(detail, selectedAngle)
  return `Advertified has retained ${plural(detail.sources.length, 'source')} and ${plural(detail.evidenceItems.length, 'evidence claim')}. The commercial direction still needs to be developed and selected.`
}

function convertedNarrative(detail: OpportunityDetail, selectedAngle: string | undefined, unresolved: number) {
  const selection = selectedAngle ? `, selected “${selectedAngle}”` : ''
  const challenge = unresolved > 0 ? ` with ${plural(unresolved, 'unresolved objection')}` : ''
  return `Advertified retained ${plural(detail.sources.length, 'source')}, developed ${plural(detail.angles.length, 'commercial angle')}${selection}, challenged the strategy${challenge}, and converted the opportunity into a campaign Brief.`
}

function selectedNarrative(detail: OpportunityDetail, selectedAngle: string) {
  return `Advertified retained the evidence, developed ${plural(detail.angles.length, 'commercial angle')} and selected “${selectedAngle}”. The opportunity has not yet been converted into a campaign Brief.`
}

function plural(value: number, noun: string) {
  return `${value} ${noun}${value === 1 ? '' : 's'}`
}
