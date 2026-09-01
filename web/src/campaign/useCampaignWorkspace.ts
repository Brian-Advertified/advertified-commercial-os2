import { useCallback, useEffect, useState } from 'react'
import { bookingApi } from '../api/booking-client'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { proposalApi } from '../api/proposal-client'
import type { ProposalRecipient } from '../api/proposal-schemas'
import { notifications } from '../notifications/notifications'

export type CampaignWorkspaceModel = {
  campaign: Campaign
  bookings: Booking[]
  reviewers: ProposalRecipient[]
}

export function useCampaignWorkspace(
  tenantId: string,
  campaignId: string,
  includeReviewers: boolean,
) {
  const [model, setModel] = useState<CampaignWorkspaceModel | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    const value = await loadWorkspace(tenantId, campaignId, includeReviewers)
    setModel(value)
    setError(null)
  }, [tenantId, campaignId, includeReviewers])

  useEffect(() => {
    let active = true
    void loadWorkspace(tenantId, campaignId, includeReviewers)
      .then(value => { if (active) setModel(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, campaignId, includeReviewers])

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setError(null)
    try {
      await action()
      await load()
      notifications.success(success)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  return { model, error, busy, run }
}

async function loadWorkspace(
  tenantId: string,
  campaignId: string,
  includeReviewers: boolean,
): Promise<CampaignWorkspaceModel> {
  const [campaign, bookings, reviewers] = await Promise.all([
    campaignApi.get(tenantId, campaignId),
    bookingApi.list(tenantId),
    includeReviewers ? proposalApi.listRecipients(tenantId) : Promise.resolve([]),
  ])
  return {
    campaign,
    bookings: bookings.filter(booking => booking.planVersionId === campaign.planVersionId),
    reviewers,
  }
}
