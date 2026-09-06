import { z } from 'zod';
import { request } from './client';

export const publicIntakeSchema = z.object({
  id: z.uuid(), typeCode: z.string(), name: z.string(), email: z.email(),
  phone: z.string().nullable(), organisation: z.string(), website: z.string().nullable(),
  relationship: z.string().nullable(), message: z.string().nullable(), status: z.string(),
  createdAtUtc: z.string(), reviewedBy: z.uuid().nullable(), reviewedAtUtc: z.string().nullable(),
  reviewReason: z.string().nullable(), provisionedTenantId: z.uuid().nullable(),
  provisionedUserId: z.uuid().nullable(), version: z.number().int().positive(),
});

const pageSchema = z.object({
  items: z.array(publicIntakeSchema),
  nextCursor: z.string().nullable(),
});

export type PublicIntake = z.infer<typeof publicIntakeSchema>;

export const publicIntakeApi = {
  async list(tenantId: string, status?: string) {
    const query = new URLSearchParams({ limit: '100' });
    if (status) query.set('status', status);
    return (await request(
      `/api/v1/tenants/${tenantId}/public-intake?${query}`,
      pageSchema,
    )).data;
  },

  async provision(
    tenantId: string,
    item: PublicIntake,
    values: { legalName: string; tradingName: string; website: string | null; vatNumber: string | null; requireMfa: boolean; reason: string },
    token: string,
  ) {
    return (await request(
      `/api/v1/tenants/${tenantId}/public-intake/${item.id}:provision`,
      publicIntakeSchema,
      { method: 'POST', body: JSON.stringify(values) },
      { antiforgeryToken: token, expectedVersion: item.version, idempotencyKey: crypto.randomUUID() },
    )).data;
  },

  async reject(tenantId: string, item: PublicIntake, reason: string, token: string) {
    return (await request(
      `/api/v1/tenants/${tenantId}/public-intake/${item.id}:reject`,
      publicIntakeSchema,
      { method: 'POST', body: JSON.stringify({ reason }) },
      { antiforgeryToken: token, expectedVersion: item.version, idempotencyKey: crypto.randomUUID() },
    )).data;
  },

  async resolve(tenantId: string, item: PublicIntake, reason: string, token: string) {
    return (await request(
      `/api/v1/tenants/${tenantId}/public-intake/${item.id}:resolve`,
      publicIntakeSchema,
      { method: 'POST', body: JSON.stringify({ reason }) },
      { antiforgeryToken: token, expectedVersion: item.version, idempotencyKey: crypto.randomUUID() },
    )).data;
  },
};
