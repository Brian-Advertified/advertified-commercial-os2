import type { Campaign } from '../api/campaign-schemas'
import type { IconName } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'

export type CampaignDeliveryTab =
  | 'funding'
  | 'bookings'
  | 'creativeStage'
  | 'live'
  | 'proof'
  | 'measurementStage'

export type CampaignDeliveryStage = {
  tab: CampaignDeliveryTab
  id: string
  label: string
  detail: string
  icon: IconName
  complete: boolean
  current: boolean
}

const stageIds: Readonly<Record<CampaignDeliveryTab, string>> = {
  funding: 'funding-stage',
  bookings: 'booking-stage',
  creativeStage: 'creative-stage',
  live: 'live-stage',
  proof: 'proof-stage',
  measurementStage: 'measurement-stage',
}

const stageDefinitions = [
  ['funding', 'Funding', 'Confirmed purchase order and payment', 'money'],
  ['bookings', 'Bookings', 'Exact supplier coverage', 'reservation'],
  ['creativeStage', 'Readiness', 'Creative approval for booked formats', 'proposal'],
  ['live', 'Live delivery', 'Human-controlled start and completion', 'plan'],
  ['proof', 'Delivery proof', 'Supplier evidence and buyer review', 'evidence'],
  ['measurementStage', 'Measurement', 'Sourced facts and approved report', 'chart'],
] as const

export function campaignDeliveryStages(campaign: Campaign): CampaignDeliveryStage[] {
  const completed = completedStages(campaign)
  const firstIncomplete = completed.findIndex(value => !value)
  const active = firstIncomplete === -1 ? completed.length - 1 : firstIncomplete
  return stageDefinitions.map(([tab, label, detail, icon], index) => ({
    tab,
    id: campaignDeliveryStageId(tab),
    label,
    detail,
    icon,
    complete: completed[index] ?? false,
    current: index === active,
  }))
}

export function currentCampaignDeliveryTab(campaign: Campaign): CampaignDeliveryTab {
  return campaignDeliveryStages(campaign).find(stage => stage.current)?.tab ?? 'funding'
}

export function campaignDeliveryStageId(tab: CampaignDeliveryTab) {
  return stageIds[tab]
}

export function campaignDeliveryTabFromHash(hash: string): CampaignDeliveryTab | null {
  const entry = Object.entries(stageIds).find(([, id]) => hash === `#${id}`)
  return entry ? entry[0] as CampaignDeliveryTab : null
}

function completedStages(campaign: Campaign): boolean[] {
  const status = campaign.status
  const bookingComplete = status !== masterDataCodes.lifecycleStatuses.planned
  const creativeComplete = status === masterDataCodes.lifecycleStatuses.ready
    || status === masterDataCodes.lifecycleStatuses.live
    || status === masterDataCodes.lifecycleStatuses.completed
  const liveComplete = status === masterDataCodes.lifecycleStatuses.completed
  const proofComplete = campaign.deliveryProofs.some(proof =>
    proof.status === masterDataCodes.lifecycleStatuses.approved)
  const measurementComplete = campaign.measurementReports.some(report =>
    report.status === masterDataCodes.lifecycleStatuses.approved)
  return [true, bookingComplete, creativeComplete, liveComplete,
    proofComplete, measurementComplete]
}
