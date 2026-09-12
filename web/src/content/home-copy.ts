export const homeCopy = {
  unavailableTitle: 'Your workspace could not be opened',
  unavailableNote: 'Some dashboard information could not be loaded. No totals or empty-workspace claims are shown until the data is available.',
  retry: 'Try again',
  noAccessTitle: 'Home is not available for this role',
  noAccessMessage: 'Choose a workspace with access to a commercial dashboard.',
  emptyIntro: 'Start with the client request. Advertified will carry the approved facts through audience, planning and proposal.',
  activeIntro: 'Your workspace brings together campaign decisions, confirmed media and the next actions that need attention.',
  startEyebrow: 'Start a campaign',
  startTitle: 'Turn the client request into an approved plan',
  startDescription: 'Paste or upload the brief, review the audience and build a media plan from the available evidence.',
  startAction: 'Start Brief',
  activeCampaigns: 'Active campaigns',
  confirmedMedia: 'Confirmed media value',
  awaitingDecision: 'Awaiting client decision',
  approvedPlans: 'Approved media plans',
  decisionNote: 'Sent proposals awaiting a client decision',
  planNote: 'Media plans approved for proposal work',
  unknownAmount: 'A confirmed price is missing or cannot be totalled safely.',
  investmentTitle: 'Confirmed media by channel',
  investmentEmpty: 'No confirmed media value is recorded in this currency yet.',
  investmentUnavailable: 'The channel breakdown will appear when all confirmed prices are available.',
  inventoryTitle: 'Inventory on this page',
  inventoryEmpty: 'No inventory is shown for this workspace.',
  inventoryNote: 'Latest updates within the loaded catalogue page; open Inventory for the full catalogue.',
  campaignState: 'Current campaign status',
  noCampaigns: 'No active campaigns yet.',
  tasksNote: 'Open the task queue to see all available work and its current status.',
  noTasks: 'No tasks are shown in this workspace.',
  activityEmpty: 'Campaign changes will appear here as work progresses.',
  clientSignal: 'Client decisions',
  clientSignalNote: 'Only sent proposals are counted as waiting for a client decision.',
  confirmedSignalNote: 'Only supplier-confirmed bookings in the workspace currency are included. This is not paid spend or campaign performance.',
} as const

export function investmentDescription(currency: string, otherCurrencies: readonly string[]) {
  const excluded = otherCurrencies.length ? ` ${otherCurrencies.join(', ')} bookings are excluded; currencies are not combined.` : ''
  return `Confirmed client-price lines in ${currency}. Fees and VAT are shown separately in each booking.${excluded}`
}
