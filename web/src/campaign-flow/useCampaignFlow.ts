import { useContext, useLayoutEffect } from 'react'
import {
  CampaignFlowContext,
  type CampaignFlowMode,
  type CampaignFlowResolution,
} from './campaign-flow-state'

export function useCampaignFlowResolution() {
  return useRequiredCampaignFlowContext().resolution
}

export function useCampaignFlowBinding(resolution: CampaignFlowResolution) {
  const { register, routeKey } = useRequiredCampaignFlowContext()
  const status = resolution.status
  const mode = status === 'resolved' ? resolution.mode : null
  useLayoutEffect(() => {
    const current = resolutionFrom(status, mode)
    return register(routeKey, current)
  }, [register, routeKey, status, mode])
}

function resolutionFrom(
  status: CampaignFlowResolution['status'],
  mode: CampaignFlowMode | null,
): CampaignFlowResolution {
  return status === 'resolved' ? { status, mode } : { status }
}

function useRequiredCampaignFlowContext() {
  const context = useContext(CampaignFlowContext)
  if (!context) throw new Error('Campaign flow binding requires CampaignFlowProvider.')
  return context
}
