import { z } from 'zod'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'

const policySchema = z.object({
  minimumOptions: z.number().int().positive(),
  maximumOptions: z.number().int().positive(),
  defaultValidityDays: z.number().int().positive(),
  maximumValidityDays: z.number().int().positive(),
}).refine(value => value.maximumOptions >= value.minimumOptions &&
  value.maximumValidityDays >= value.defaultValidityDays)

const definition = masterDataDefinitions.proposalPolicies.find(item =>
  item.code === masterDataCodes.proposalPolicies.clientOptionsV1 && item.isActive)

if (!definition) throw new Error('The proposal policy is unavailable.')

export const proposalPolicy = policySchema.parse(definition.metadata)
