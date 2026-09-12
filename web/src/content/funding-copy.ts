import { masterDataCodes } from '../generated/master-data-codes'

export const fundingCopy = {
  method: 'Payment method',
  heading: 'Record expected payment',
  start: 'Start payment record',
  opened: 'The payment record was opened for independent reconciliation.',
  unavailable: 'VodaPay is awaiting provider details.',
  methods: {
    [masterDataCodes.paymentMethods.manualEft]: {
      label: 'Manual EFT',
      description: 'Use the banking details supplied with the invoice. An independent reviewer must reconcile the receipt before funding is confirmed.',
      reference: 'Bank reference',
      evidence: 'Receipt evidence',
      invalid: 'Provide the bank reference, reconciliation reason and receipt evidence.',
    },
    [masterDataCodes.paymentMethods.advertiseNowPayLater]: {
      label: 'Advertise Now, Pay Later',
      description: 'Refer the invoice to the partner manually. The partner contacts the client and emails the application outcome. An independent administrator reviews that notification against this invoice before confirming funding.',
      reference: 'Partner email reference',
      evidence: 'Partner email evidence (PDF or image)',
      invalid: 'Provide the partner email reference, reconciliation reason and notification evidence.',
    },
  },
} as const

export type ManualFundingMethod = keyof typeof fundingCopy.methods

export function paymentEvidenceCopy(method: string) {
  return fundingCopy.methods[method as ManualFundingMethod] ?? {
    reference: 'Payment reference',
    evidence: 'Payment evidence',
    invalid: 'Provide the payment reference, reconciliation reason and supporting evidence.',
  }
}
