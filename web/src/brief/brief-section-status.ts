import type { CampaignMode } from '../api/planning-schemas'
import type { CampaignBrief, BriefVersion } from '../api/schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import {
  briefSections,
  type BriefSectionId,
  type BriefSectionState,
} from './brief-section-flow-state'

export function buildSectionStates(
  record: CampaignBrief,
  version: BriefVersion,
  campaignMode: CampaignMode | null,
  approved: boolean,
): BriefSectionState[] {
  const completed = sectionCompletion(record, version, campaignMode, approved)
  return briefSections.map(([id, label]) => ({
    id,
    label,
    status: completed[id] ? 'complete' : 'attention',
  }))
}

function sectionCompletion(
  record: CampaignBrief,
  version: BriefVersion,
  campaignMode: CampaignMode | null,
  approved: boolean,
): Record<BriefSectionId, boolean> {
  return {
    overview: completeOverview(record, version),
    objectives: Boolean(version.objective.trim()) &&
      !hasOpenItem(version, ['objective']),
    [masterDataCodes.agentTypes.audience]: version.audiences.length > 0 &&
      !hasOpenItem(version, [masterDataCodes.agentTypes.audience]),
    geography: version.geographies.length > 0 &&
      !hasOpenItem(version, ['geograph', 'location']),
    timing: Boolean(version.timing.trim()) &&
      !hasOpenItem(version, ['timing', 'date', 'flight']),
    budget: completeBudget(version),
    media: campaignMode !== null &&
      !hasOpenItem(version, ['campaignmode', 'campaign_mode', 'media']),
    constraints: !hasOpenItem(version, ['constraint']),
    [masterDataCodes.agentTypes.measurement]: version.measurement.length > 0 &&
      !hasOpenItem(version, [masterDataCodes.agentTypes.measurement, 'kpi']),
    attachments: record.sources.length > 0,
    review: completeReview(version, approved),
  }
}

function completeOverview(record: CampaignBrief, version: BriefVersion) {
  return Boolean(record.brief.title.trim() && version.businessProblem.trim()) &&
    !hasOpenItem(version, ['title', 'businessproblem', 'business_problem'])
}

function completeBudget(version: BriefVersion) {
  return !version.budgetUnknown &&
    version.budgetMinor !== null &&
    Boolean(version.currency) &&
    !hasOpenItem(version, ['budget', 'currency', 'vat'])
}

function completeReview(version: BriefVersion, approved: boolean) {
  return approved &&
    version.unknowns.length === 0 &&
    version.conflicts.every(item => item.resolved)
}

function hasOpenItem(version: BriefVersion, fields: string[]) {
  const matches = (fieldPath: string) => fields.some(field =>
    fieldPath.toLowerCase().includes(field))
  return version.unknowns.some(item => matches(item.fieldPath)) ||
    version.conflicts.some(item => !item.resolved && matches(item.fieldPath))
}
