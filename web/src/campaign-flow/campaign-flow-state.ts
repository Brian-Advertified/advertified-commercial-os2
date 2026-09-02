import { createContext } from 'react'
import { masterDataCodes } from '../generated/master-data-codes'

export type CampaignFlowMode =
  | typeof masterDataCodes.campaignModes.fullCampaign
  | typeof masterDataCodes.campaignModes.oohOnly

export type CampaignFlowResolution =
  | { status: 'unbound' }
  | { status: 'loading' }
  | { status: 'unavailable' }
  | { status: 'resolved'; mode: CampaignFlowMode | null }

export type CampaignFlowRegistration = {
  id: number
  routeKey: string
  resolution: CampaignFlowResolution
}

export type CampaignFlowContextValue = {
  routeKey: string
  resolution: CampaignFlowResolution
  register: (
    routeKey: string,
    resolution: CampaignFlowResolution,
  ) => () => void
}

export const unboundCampaignFlow: CampaignFlowResolution = { status: 'unbound' }
export const CampaignFlowContext = createContext<CampaignFlowContextValue | null>(null)

export function campaignModeResolution(mode: string | null): CampaignFlowResolution {
  if (mode === null) return { status: 'resolved', mode: null }
  if (mode === masterDataCodes.campaignModes.fullCampaign ||
      mode === masterDataCodes.campaignModes.oohOnly) {
    return { status: 'resolved', mode }
  }
  return { status: 'unavailable' }
}
