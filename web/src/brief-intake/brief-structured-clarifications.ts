import type { BriefClarification } from '../api/brief-understanding-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

export function structuredBriefClarifications(values: FormData): BriefClarification[] {
  const campaignType = String(values.get('campaignType') ?? '').trim()
  const campaignTypeExplicit = String(values.get('campaignTypeExplicit') ?? '') === 'true'
  return [
    ...(campaignTypeExplicit ? campaignTypeClarifications(campaignType) : []),
    ...budgetClarification(values),
    ...optionalClarification('timing', values.get('timingClarification')),
    ...optionalClarification('geographies', values.get('geographyClarification')),
    ...goalClarifications(values.getAll('campaignGoal')),
  ]
}

function budgetClarification(values: FormData): BriefClarification[] {
  const selection = values.get('budgetClarification')
  if (selection !== 'confirmed') return optionalClarification('budget', selection)
  const amount = String(values.get('budgetExactClarification') ?? '').trim()
  return amount ? [{ fieldPath: 'budget', value: `${masterDataCodes.currencies.zar} ${amount}` }] : []
}

function campaignTypeClarifications(selectedType: string): BriefClarification[] {
  if (!selectedType) return []
  const result: BriefClarification[] = [{
    fieldPath: 'campaignMode',
    value: selectedType === masterDataCodes.campaignModes.oohOnly
      ? masterDataCodes.campaignModes.oohOnly
      : masterDataCodes.campaignModes.fullCampaign,
  }]
  if (selectedType !== masterDataCodes.campaignModes.oohOnly &&
      selectedType !== masterDataCodes.campaignModes.fullCampaign) {
    result.push({ fieldPath: 'mediaRequirements', value: selectedType })
  }
  return result
}

function optionalClarification(fieldPath: string, value: FormDataEntryValue | null): BriefClarification[] {
  const normalized = String(value ?? '').trim()
  return normalized ? [{ fieldPath, value: normalized }] : []
}

function goalClarifications(values: FormDataEntryValue[]): BriefClarification[] {
  const goals = values.map(value => String(value).trim()).filter(Boolean).slice(0, 3)
  return goals.length ? [{ fieldPath: 'objective', value: goals.join('; ') }] : []
}
