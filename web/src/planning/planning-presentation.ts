import type { MediaAllocation, MediaMix, PlanningWorkspace } from '../api/planning-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { mediaVisual } from './media-visuals'

export function allocationPeriods(allocation: MediaAllocation) {
  if (!allocation.runningPeriods.length) return 'Not scheduled'
  return allocation.runningPeriods.map(period => `${period.start} – ${period.end}`).join(' · ')
}

export function allocationPercent(item: MediaAllocation, mix: MediaMix) {
  return mix.totalBudgetMinor ? `${Math.round(item.budgetMinor / mix.totalBudgetMinor * 100)}%` : '—'
}

export function campaignModeLabel(workspace: PlanningWorkspace) {
  return workspace.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly
    ? 'Outdoor advertising and digital screens only' : 'Full campaign'
}

export function strategyExplanation(workspace: PlanningWorkspace, mix: MediaMix) {
  const largest = [...mix.allocations].sort((a, b) => b.budgetMinor - a.budgetMinor)[0]
  const lead = largest ? `${mediaVisual(largest.channel).label} carries the largest current allocation` : 'The mix is still being shaped'
  const objective = workspace.decisionContext?.objective || 'the approved campaign objective'
  return `${lead}. The allocation is retained against ${objective}; channel roles and flight dates remain editable before supply is committed.`
}

export function isApproved(status: string | null | undefined) {
  return status === masterDataCodes.lifecycleStatuses.approved
}
