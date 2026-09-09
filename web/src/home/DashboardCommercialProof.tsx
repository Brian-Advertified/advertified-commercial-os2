import type { Campaign } from '../api/campaign-schemas'
import type { PlanningSummary } from '../api/planning-schemas'
import type { ProposalSummary } from '../api/proposal-schemas'
import type { Booking } from '../api/booking-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'
import { masterDataCodes } from '../generated/master-data-codes'

export function DashboardCommercialProof({ planning, proposals, campaigns, bookings }: {
  planning: PlanningSummary[]
  proposals: ProposalSummary[]
  campaigns: Campaign[]
  bookings: Booking[]
}) {
  const approvedPlans = planning.filter(item =>
    item.mediaPlanStatus === masterDataCodes.lifecycleStatuses.approved).length
  const activeCampaigns = campaigns.filter(item =>
    item.status !== masterDataCodes.lifecycleStatuses.completed &&
    item.status !== masterDataCodes.lifecycleStatuses.cancelled).length
  const confirmedBookings = bookings.filter(item => isConfirmedBooking(item.status)).length
  const metrics: CommercialProofMetric[] = [
    {
      label: 'Campaigns in planning', value: planning.length, icon: 'plan', tone: planning.length ? 'violet' : 'neutral',
      detail: 'Campaign requirements with a retained planning workspace.',
      why: 'Counted from the current tenant planning summaries; no inferred work is included.',
    },
    {
      label: 'Approved media plans', value: approvedPlans, icon: 'evidence', tone: approvedPlans ? 'positive' : 'neutral',
      detail: 'Plans that completed audience, allocation, supply selection and commercial reconciliation.',
      why: 'Only planning summaries whose persisted media-plan status is APPROVED are counted.',
    },
    {
      label: 'Client proposals', value: proposals.length, icon: 'proposal', tone: proposals.length ? 'blue' : 'neutral',
      detail: 'Retained client proposal versions created from approved plans.',
      why: 'This is the current proposal summary count for the workspace.',
    },
    {
      label: 'Execution underway', value: activeCampaigns, icon: 'target', tone: activeCampaigns ? 'positive' : 'neutral',
      detail: `${confirmedBookings} confirmed booking${confirmedBookings === 1 ? '' : 's'} retained across campaign execution.`,
      why: 'Active campaigns exclude completed and cancelled records. Booking count uses only confirmed booking states.',
    },
  ]
  return <CommercialValueProof title="Advertified is moving work from requirement to execution"
    description={pipelineSentence(planning.length, approvedPlans, proposals.length, activeCampaigns)}
    metrics={metrics}
    note="This view measures persisted commercial progress. It does not estimate hours saved, revenue generated or campaign performance without supporting evidence." />
}

function pipelineSentence(planning: number, plans: number, proposals: number, campaigns: number) {
  if (planning + proposals + campaigns === 0) {
    return 'Start with a client requirement. Commercial progress will appear here as Advertified turns it into audience decisions, plans, proposals and executable campaigns.'
  }
  return `${planning} planning workspace${planning === 1 ? '' : 's'} have produced ${plans} approved media plan${plans === 1 ? '' : 's'}, ${proposals} client proposal${proposals === 1 ? '' : 's'} and ${campaigns} active campaign${campaigns === 1 ? '' : 's'}.`
}

function isConfirmedBooking(status: string) {
  return status === masterDataCodes.lifecycleStatuses.approved ||
    status === masterDataCodes.lifecycleStatuses.confirmed ||
    status === masterDataCodes.lifecycleStatuses.active
}
