import { masterDataCodes } from '../generated/master-data-codes'

const statusLabels: Record<string, string> = {
  [masterDataCodes.emailAutomationStatuses.received]: 'Received',
  [masterDataCodes.emailAutomationStatuses.processing]: 'Preparing proposal',
  [masterDataCodes.emailAutomationStatuses.sent]: 'Proposal sent',
  [masterDataCodes.emailAutomationStatuses.reviewRequired]: 'Needs review',
  [masterDataCodes.emailAutomationStatuses.failed]: 'Could not complete',
  [masterDataCodes.emailAutomationStatuses.duplicate]: 'Already received',
}

const failureLabels: Record<string, string> = {
  [masterDataCodes.automationFailureReasons.invalidMailbox]:
    'This message was sent to a mailbox that is not configured for automatic OOH proposals.',
  [masterDataCodes.automationFailureReasons.invalidProviderSignature]:
    'The incoming email notification could not be verified.',
  [masterDataCodes.automationFailureReasons.duplicateMessage]:
    'This message has already been processed.',
  [masterDataCodes.automationFailureReasons.invalidRecipient]:
    'A safe reply address could not be confirmed.',
  [masterDataCodes.automationFailureReasons.clientNotResolved]:
    'The client could not be identified from the email or mailbox setup.',
  [masterDataCodes.automationFailureReasons.nonOohRequest]:
    'The request includes media beyond OOH. Start a new full campaign from the beginning.',
  [masterDataCodes.automationFailureReasons.incompleteBrief]:
    'The email does not yet contain enough information to prepare a reliable proposal.',
  [masterDataCodes.automationFailureReasons.attachmentReviewRequired]:
    'An attachment requires review before it can be used.',
  [masterDataCodes.automationFailureReasons.stpUnready]:
    'The segmentation, targeting or positioning evidence is not ready for automatic sending.',
  [masterDataCodes.automationFailureReasons.supplyUnready]:
    'Confirmed inventory, rates or availability are not ready for every OOH selection.',
  [masterDataCodes.automationFailureReasons.planUnready]:
    'The media plan has an unresolved commercial issue.',
  [masterDataCodes.automationFailureReasons.proposalUnready]:
    'The proposal is not ready to be sent.',
  [masterDataCodes.automationFailureReasons.deliveryFailed]:
    'The proposal was prepared, but the email could not be delivered.',
}

export const automationCheckpoints = [
  [masterDataCodes.emailAutomationCheckpoints.sourceCaptured, 'Email captured'],
  [masterDataCodes.emailAutomationCheckpoints.briefApproved, 'Brief approved'],
  [masterDataCodes.emailAutomationCheckpoints.stpApproved, 'STP completed'],
  [masterDataCodes.emailAutomationCheckpoints.mixApproved, 'OOH mix approved'],
  [masterDataCodes.emailAutomationCheckpoints.shortlistApproved, 'Inventory selected'],
  [masterDataCodes.emailAutomationCheckpoints.planApproved, 'Media plan approved'],
  [masterDataCodes.emailAutomationCheckpoints.proposalApproved, 'Proposal approved'],
  [masterDataCodes.emailAutomationCheckpoints.documentRendered, 'PDF prepared'],
  [masterDataCodes.emailAutomationCheckpoints.sent, 'Proposal sent'],
] as const

export function automationStatusLabel(status: string) {
  return statusLabels[status] ?? humanize(status)
}

export function automationFailureLabel(code: string | null, detail?: string | null) {
  if (!code) return detail ?? 'Review the message and its latest completed stage.'
  return failureLabels[code] ?? detail ?? humanize(code)
}

export function checkpointIndex(checkpoint: string) {
  return Math.max(0, automationCheckpoints.findIndex(([code]) => code === checkpoint))
}

export function humanize(value: string) {
  return value.toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase())
}
