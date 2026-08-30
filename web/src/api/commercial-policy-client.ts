import { ApiFailure, request } from './client'
import { commercialPolicySchema, type CommercialPolicy, type CommercialPolicyInput }
  from './commercial-policy-schemas'

const missingPolicyCode = 'COMMERCIAL_POLICY_NOT_CONFIGURED'

function policyPath(tenantId: string) {
  return `/api/v1/tenants/${tenantId}/commercial-policy`
}

export const commercialPolicyApi = {
  async getCurrent(tenantId: string): Promise<CommercialPolicy | null> {
    try {
      return (await request(policyPath(tenantId), commercialPolicySchema)).data
    } catch (failure) {
      if (failure instanceof ApiFailure && failure.code === missingPolicyCode) return null
      throw failure
    }
  },

  async save(
    tenantId: string,
    input: CommercialPolicyInput,
    expectedVersion: number,
    antiforgeryToken: string,
  ): Promise<CommercialPolicy> {
    return (await request(
      policyPath(tenantId),
      commercialPolicySchema,
      { method: 'PUT', body: JSON.stringify(input) },
      {
        antiforgeryToken,
        expectedVersion,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },
}
