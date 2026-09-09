import type { MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'

export function PlanningCommercialProof({ workspace, mix, shortlist, plan }: {
  workspace: PlanningWorkspace
  mix: MediaMix | null
  shortlist: Shortlist | null
  plan: MediaPlan | null
}) {
  return <CommercialValueProof title="Advertified is turning strategy into a commercial decision"
    description={planningDecisionNarrative(workspace, mix, shortlist, plan)}
    metrics={planningProofMetrics(shortlist)} note={planningProofNote(plan)} />
}

function planningProofMetrics(shortlist: Shortlist | null): CommercialProofMetric[] {
  return [
    evaluatedMetric(shortlist),
    eligibleMetric(shortlist),
    supplySourceMetric(shortlist),
    commercialAlternativeMetric(shortlist),
  ]
}

function evaluatedMetric(shortlist: Shortlist | null): CommercialProofMetric {
  if (!shortlist) return {
    label: 'Supply evaluated', value: 'Pending',
    detail: 'Approve the media mix to start supply evaluation.', icon: 'inventory', tone: 'neutral',
    why: 'No shortlist exists for the current mix yet.',
  }
  return {
    label: 'Supply evaluated', value: shortlist.candidates.length,
    detail: 'Published candidates have been tested against the approved mix and hard Brief constraints.',
    icon: 'inventory', tone: 'violet',
    why: 'This is the persisted candidate count on the current shortlist version.',
  }
}

function eligibleMetric(shortlist: Shortlist | null): CommercialProofMetric {
  if (!shortlist) return {
    label: 'Eligible choices', value: 'Pending', detail: 'Eligibility is not guessed before inventory evaluation runs.',
    icon: 'evidence', tone: 'neutral',
  }
  const eligible = shortlist.candidates.filter(item => item.isEligible).length
  const selected = shortlist.candidates.filter(item => item.isSelected === true).length
  return {
    label: 'Eligible choices', value: eligible, detail: `${selected} currently selected from the eligible set.`,
    icon: 'evidence', tone: eligible > 0 ? 'positive' : 'warning',
    why: 'Eligibility and selection come directly from each retained shortlist candidate.',
  }
}

function supplySourceMetric(shortlist: Shortlist | null): CommercialProofMetric {
  if (!shortlist) return {
    label: 'Supply sources represented', value: 'Pending', detail: 'This becomes visible when real supply is shortlisted.',
    icon: 'globe', tone: 'neutral',
  }
  const count = new Set(shortlist.candidates
    .map(item => item.supplierId ?? item.inventoryTenantId)).size
  const names = [...new Set(shortlist.candidates.map(item => item.supplierName).filter(Boolean))]
  return {
    label: 'Suppliers represented', value: count,
    detail: names.length > 0
      ? names.slice(0, 3).join(' · ') + (names.length > 3 ? ` · +${names.length - 3} more` : '')
      : 'Distinct suppliers are represented in the evaluated supply.',
    icon: 'globe', tone: count > 1 ? 'blue' : 'neutral',
    why: 'This is a deduplicated supplier count from the retained shortlist. Older shortlist records fall back to the inventory owner when a supplier identifier was not yet persisted.',
  }
}

function commercialAlternativeMetric(shortlist: Shortlist | null): CommercialProofMetric {
  const count = shortlist?.campaignCombinations?.alternatives.length ?? 0
  const lowerCost = lowerCostAlternative(shortlist)
  if (lowerCost) return {
    label: 'Commercial alternatives', value: `${formatMoney(lowerCost.deltaMinor, lowerCost.currency)} lower-cost route`,
    detail: `${scenarioCountLabel(count)} compared. This is a supplier-cost alternative, not a claim of equal outcome.`,
    icon: 'money', tone: 'positive',
    why: 'The amount is the supplier-cost difference between the persisted recommended scenario and the persisted LOWER_SUPPLIER_COST scenario.',
  }
  if (count === 0) return {
    label: 'Commercial alternatives', value: 'Pending', detail: 'No alternative scenario has been retained for the current shortlist yet.',
    icon: 'money', tone: 'neutral',
  }
  return {
    label: 'Commercial alternatives', value: count, detail: `${scenarioCountLabel(count)} available for comparison.`,
    icon: 'money', tone: count > 1 ? 'blue' : 'neutral',
    why: 'This is the number of campaign-combination alternatives retained with the current shortlist.',
  }
}

function scenarioCountLabel(count: number) {
  return `${count} campaign scenario${count === 1 ? '' : 's'}`
}

type LowerCostAlternative = { deltaMinor: number; currency: string }

function lowerCostAlternative(shortlist: Shortlist | null): LowerCostAlternative | null {
  const alternatives = shortlist?.campaignCombinations?.alternatives ?? []
  const recommended = alternatives.find(item => item.scenario?.recommended)
  const lower = alternatives.find(item => item.scenario?.code === 'LOWER_SUPPLIER_COST')
  if (!recommended || !lower || lower.currency !== recommended.currency) return null
  const deltaMinor = recommended.campaignSupplierCostMinor - lower.campaignSupplierCostMinor
  return deltaMinor > 0 ? { deltaMinor, currency: lower.currency } : null
}

function planningProofNote(plan: MediaPlan | null) {
  if (!plan) return 'Counts are taken from the current persisted planning versions; no supplier availability, reach or savings are invented.'
  const lines = `${plan.lines.length} priced line${plan.lines.length === 1 ? '' : 's'}`
  return `${lines} reconcile to ${formatMoney(plan.totalMinor, plan.currency)} in the current plan. Performance is not implied by price.`
}

function planningDecisionNarrative(
  workspace: PlanningWorkspace,
  mix: MediaMix | null,
  shortlist: Shortlist | null,
  plan: MediaPlan | null,
) {
  if (!shortlist) return planningStageDescription(workspace, mix)
  const candidates = shortlist.candidates
  const eligible = candidates.filter(item => item.isEligible)
  const selected = candidates.filter(item => item.isSelected === true)
  const suppliers = new Set(candidates.map(item => item.supplierId ?? item.inventoryTenantId)).size
  const required = new Set(candidates.flatMap(item => item.spatialMatch?.requiredRequirementIds ?? []))
  const selectedCoverage = new Set(selected.flatMap(item =>
    item.spatialMatch?.matchedRequiredRequirementIds ?? []))
  const channels = (mix?.allocations ?? []).filter(item => item.budgetMinor > 0)
    .map(item => mediaVisual(item.channel).label)
  const coverage = required.size > 0
    ? ` Current selection covers ${selectedCoverage.size}/${required.size} required areas.`
    : ''
  const channelCopy = channels.length > 0 ? ` across ${channels.join(', ')}` : ''
  const planCopy = plan
    ? ` The reconciled plan contains ${plan.lines.length} priced line${plan.lines.length === 1 ? '' : 's'} totalling ${formatMoney(plan.totalMinor, plan.currency)}.`
    : ''
  return `Advertified evaluated ${candidates.length} media option${candidates.length === 1 ? '' : 's'} from ${suppliers} supplier${suppliers === 1 ? '' : 's'}, found ${eligible.length} eligible and currently carries ${selected.length} forward${channelCopy}.${coverage}${planCopy}`
}

function planningStageDescription(workspace: PlanningWorkspace, mix: MediaMix | null) {
  if (mix) return 'The approved audience strategy now has explicit channel roles, investment allocations and independent running periods.'
  return workspace.audience
    ? 'The approved audience strategy is ready to become an explainable media investment decision.'
    : 'Planning evidence will appear here as the campaign advances.'
}
