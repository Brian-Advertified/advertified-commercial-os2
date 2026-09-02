import { useCallback, useEffect, useState } from 'react'
import { briefApi } from '../api/brief-client'
import { campaignApi } from '../api/campaign-client'
import { planningApi } from '../api/planning-client'
import { proposalApi } from '../api/proposal-client'
import {
  campaignModeResolution,
  type CampaignFlowResolution,
} from './campaign-flow-state'
import { useCampaignFlowBinding } from './useCampaignFlow'

const loading: CampaignFlowResolution = { status: 'loading' }

export function CampaignModeBinding({ mode }: { mode: string | null }) {
  useCampaignFlowBinding(campaignModeResolution(mode))
  return null
}

export function BriefFlowBinding({ tenantId, briefId }: {
  tenantId: string
  briefId: string
}) {
  const load = useCallback(async () => {
    const brief = await briefApi.get(tenantId, briefId)
    const version = brief.versions.at(-1)
    return version ? loadBriefVersionMode(tenantId, version.id) : null
  }, [tenantId, briefId])
  const resolution = useResolvedMode(`brief:${tenantId}:${briefId}`, load)
  useCampaignFlowBinding(resolution)
  return null
}

export function BriefVersionFlowBinding({ tenantId, briefVersionId }: {
  tenantId: string
  briefVersionId: string
}) {
  const load = useCallback(
    () => loadBriefVersionMode(tenantId, briefVersionId),
    [tenantId, briefVersionId],
  )
  const resolution = useResolvedMode(`brief-version:${tenantId}:${briefVersionId}`, load)
  useCampaignFlowBinding(resolution)
  return null
}

export function ProposalFlowBinding({ tenantId, proposalId }: {
  tenantId: string
  proposalId: string
}) {
  const load = useCallback(async () => {
    const proposal = await proposalApi.get(tenantId, proposalId)
    return loadBriefVersionMode(tenantId, proposal.briefVersionId)
  }, [tenantId, proposalId])
  const resolution = useResolvedMode(`proposal:${tenantId}:${proposalId}`, load)
  useCampaignFlowBinding(resolution)
  return null
}

export function CampaignFlowBinding({ tenantId, campaignId }: {
  tenantId: string
  campaignId: string
}) {
  const load = useCallback(async () => {
    const campaign = await campaignApi.get(tenantId, campaignId)
    return loadBriefVersionMode(tenantId, campaign.briefVersionId)
  }, [tenantId, campaignId])
  const resolution = useResolvedMode(`campaign:${tenantId}:${campaignId}`, load)
  useCampaignFlowBinding(resolution)
  return null
}

function useResolvedMode(key: string, load: () => Promise<string | null>) {
  const [result, setResult] = useState<{
    key: string
    resolution: CampaignFlowResolution
  }>({ key, resolution: loading })
  useEffect(() => {
    let active = true
    void load()
      .then(mode => {
        if (active) setResult({ key, resolution: campaignModeResolution(mode) })
      })
      .catch(() => {
        if (active) setResult({ key, resolution: { status: 'unavailable' } })
      })
    return () => { active = false }
  }, [key, load])
  return result.key === key ? result.resolution : loading
}

async function loadBriefVersionMode(tenantId: string, briefVersionId: string) {
  const workspace = await planningApi.getWorkspace(tenantId, briefVersionId)
  return workspace.campaignMode?.mode ?? null
}
