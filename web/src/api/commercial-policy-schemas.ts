import { z } from 'zod'

const requiredCode = z.string().trim().min(1)

export const commercialPolicySchema = z.object({
  id: z.guid(),
  policyId: z.guid(),
  versionNumber: z.number().int().positive(),
  markupBasisPoints: z.number().int().nonnegative(),
  managementFeeBasisPoints: z.number().int().nonnegative(),
  commissionBasisPoints: z.number().int().nonnegative(),
  vatStatus: requiredCode,
  vatRateBasisPoints: z.number().int().nonnegative(),
  pricesIncludeVat: z.boolean(),
  currency: requiredCode,
  bookingApprovalThresholdMinor: z.number().int().nonnegative(),
  allowSelfApproval: z.boolean(),
  createdBy: z.guid(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  version: z.number().int().positive(),
}).strict()

export type CommercialPolicy = z.infer<typeof commercialPolicySchema>

export type CommercialPolicyInput = Pick<CommercialPolicy,
  | 'markupBasisPoints'
  | 'managementFeeBasisPoints'
  | 'commissionBasisPoints'
  | 'vatStatus'
  | 'vatRateBasisPoints'
  | 'pricesIncludeVat'
  | 'currency'
  | 'bookingApprovalThresholdMinor'
  | 'allowSelfApproval'>
