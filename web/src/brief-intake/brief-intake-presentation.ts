import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import type { IconName } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, humanizeCode } from '../presentation/format'

export function campaignModeLabel(value: string | null) {
  if (value === masterDataCodes.campaignModes.oohOnly) return 'OOH and DOOH only'
  if (value === masterDataCodes.campaignModes.fullCampaign) return 'Full campaign'
  return 'Media scope needs confirmation'
}

export function campaignModeIcon(value: string | null): IconName {
  return value === masterDataCodes.campaignModes.oohOnly ? 'inventory' : 'globe'
}

export function understandingBudgetLabel(understanding: SuppliedBriefUnderstanding) {
  const { budgetMinor, budgetUnknown, currency } = understanding.draft
  if (budgetUnknown || budgetMinor === null || !currency) return 'Not supplied'
  return formatMoney(budgetMinor, currency)
}

export function understandingTaxLabel(understanding: SuppliedBriefUnderstanding) {
  const value = understanding.draft.vatStatus
  return value ? humanizeCode(value, true) : 'Not supplied'
}

export function suppliedText(value: string) {
  return value || 'Not supplied'
}

export function suppliedList(values: string[]) {
  return values.length > 0 ? values.join(', ') : 'Not supplied'
}
