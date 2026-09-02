import { request } from './client'
import {
  commercialPolicyResponseSchema,
  commercialPolicySchema,
  type CommercialPolicy,
  type CommercialPolicyInput,
} from './commercial-policy-schemas'

function policyPath(tenantId: string) {
  return `/api/v1/tenants/${tenantId}/commercial-policy`
}

export const commercialPolicyApi = {
  async getCurrent(tenantId: string): Promise<CommercialPolicy | null> {
    return (await request(policyPath(tenantId), commercialPolicyResponseSchema)).data
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
