import { useCallback } from 'react'
import { bookingApi } from '../api/booking-client'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { proposalApi } from '../api/proposal-client'
import type { ProposalRecipient } from '../api/proposal-schemas'
import { useResourceRecord } from './useResourceRecord'

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
  const loader = useCallback(() => loadWorkspace(tenantId, campaignId, includeReviewers),
    [tenantId, campaignId, includeReviewers])
  const { record: model, error, busy, run } = useResourceRecord(loader)
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
