import type { Campaign } from '../api/campaign-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'
import type { CampaignWorkspaceModel } from './useCampaignWorkspace'

export function CampaignCommercialProof({ model }: { model: CampaignWorkspaceModel }) {
  return <CommercialValueProof title="The commercial decision stays connected through delivery"
    description="Advertified keeps the selected proposal, supplier commitments, creative readiness, proof and measurement on one accountable campaign journey."
    metrics={campaignProofMetrics(model)}
    note={`Current campaign state: ${model.campaign.status}. No delivery or performance is inferred from workflow progress alone.`} />
}

function campaignProofMetrics(model: CampaignWorkspaceModel): CommercialProofMetric[] {
  return [
    bookingMetric(model),
    creativeMetric(model.campaign),
    deliveryMetric(model.campaign),
    measurementMetric(model.campaign),
  ]
}

function bookingMetric(model: CampaignWorkspaceModel): CommercialProofMetric {
  const { campaign, bookings } = model
  const complete = campaign.requiredBookingCount > 0 &&
    campaign.confirmedBookingCount >= campaign.requiredBookingCount
  return {
    label: 'Supplier commitments', value: `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount}`,
    detail: 'Booking progress remains tied to the exact client-selected plan.', icon: 'inventory',
    tone: complete ? 'positive' : 'warning',
    why: `The campaign records ${bookingCountLabel(campaign.requiredBookingCount)} and ${campaign.confirmedBookingCount} confirmed. ${bookingCountLabel(bookings.length)} are loaded for this plan.`,
  }
}

function creativeMetric(campaign: Campaign): CommercialProofMetric {
  if (campaign.creativeApprovedAtUtc) return {
    label: 'Creative readiness', value: 'Approved', detail: 'Creative approval is retained before execution.',
    icon: 'proposal', tone: 'positive', why: 'A persisted creative approval timestamp exists on the campaign.',
  }
  if (campaign.creativeRequestedAtUtc) return {
    label: 'Creative readiness', value: 'In progress',
    detail: 'Creative status remains visible instead of being detached from the booking.',
    icon: 'proposal', tone: 'blue', why: 'A creative request exists but no approval timestamp exists yet.',
  }
  return {
    label: 'Creative readiness', value: 'Not started',
    detail: 'Creative status remains visible instead of being detached from the booking.',
    icon: 'proposal', tone: 'neutral', why: 'No creative request or approval timestamp exists on the current campaign.',
  }
}

function deliveryMetric(campaign: Campaign): CommercialProofMetric {
  const reviewed = campaign.deliveryProofs.filter(item => item.reviewedAtUtc !== null).length
  const total = campaign.deliveryProofs.length
  return {
    label: 'Delivery evidence', value: `${reviewed}/${total} reviewed`,
    detail: total > 0 ? 'Proof remains linked to the campaign and booking evidence.' : 'No delivery proof has been retained yet.',
    icon: 'evidence', tone: reviewed > 0 ? 'positive' : 'neutral',
    why: 'Reviewed proof counts only delivery-proof records with a persisted review timestamp.',
  }
}

function measurementMetric(campaign: Campaign): CommercialProofMetric {
  const reviewed = campaign.measurementReports.filter(item => item.reviewedAtUtc !== null).length
  const total = campaign.measurementReports.length
  return {
    label: 'Measurement learning', value: `${reviewed}/${total} reviewed`,
    detail: reviewed > 0 ? 'Reviewed measurement can now inform the campaign learning record.' : 'Measurement is not claimed before evidence and review exist.',
    icon: 'chart', tone: reviewed > 0 ? 'positive' : 'neutral',
    why: 'Reviewed measurement counts only retained measurement reports with a persisted review timestamp.',
  }
}

function bookingCountLabel(count: number) {
  return `${count} booking record${count === 1 ? '' : 's'}`
}
