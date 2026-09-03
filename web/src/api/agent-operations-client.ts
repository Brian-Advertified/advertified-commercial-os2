import { request } from './client'
import { agentOperationsSchema, type AgentOperations } from './agent-operations-schemas'

export const agentOperationsApi = {
  async get(tenantId: string): Promise<AgentOperations> {
    return (await request(
      `/api/v1/tenants/${tenantId}/agent-operations`,
      agentOperationsSchema,
    )).data
  },
}
