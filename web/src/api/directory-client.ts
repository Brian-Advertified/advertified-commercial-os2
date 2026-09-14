import { request } from './client'
import { agencyPageSchema, clientAccountPageSchema, type Agency, type ClientAccount } from './schemas'

export const directoryApi = {
  async listAdvertisers(tenantId: string): Promise<ClientAccount[]> {
    return (await request(`/api/v1/tenants/${tenantId}/client-accounts`, clientAccountPageSchema)).data.items
  },

  async listAgencies(tenantId: string): Promise<Agency[]> {
    return (await request(`/api/v1/tenants/${tenantId}/agencies`, agencyPageSchema)).data.items
  },
}
