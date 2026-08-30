import { request } from './client'
import {
  bookablePlanLinesSchema,
  bookingSchema,
  bookingsSchema,
  type BookablePlanLine,
  type Booking,
} from './booking-schemas'

const path = (tenantId: string, suffix = '') =>
  `/api/v1/tenants/${tenantId}/bookings${suffix}`

function command(
  tenantId: string,
  suffix: string,
  body: unknown,
  token: string,
  expectedVersion?: number,
): Promise<Booking> {
  return request(
    path(tenantId, suffix), bookingSchema,
    { method: 'POST', body: JSON.stringify(body) },
    { antiforgeryToken: token, expectedVersion, idempotencyKey: crypto.randomUUID() },
  ).then(result => result.data)
}

export const bookingApi = {
  async list(tenantId: string): Promise<Booking[]> {
    return (await request(path(tenantId), bookingsSchema)).data
  },

  async listBookableLines(tenantId: string): Promise<BookablePlanLine[]> {
    return (await request(path(tenantId, '/bookable-lines'), bookablePlanLinesSchema)).data
  },

  create(tenantId: string, line: BookablePlanLine, terms: string, token: string) {
    return command(tenantId, '', {
      proposalVersionId: line.proposalVersionId,
      proposalOptionId: line.proposalOptionId,
      mediaPlanLineId: line.mediaPlanLineId,
      terms,
    }, token)
  },

  requestConfirmation(tenantId: string, booking: Booking, token: string) {
    return command(tenantId, `/${booking.id}:request-confirmation`, {
      reason: 'Buyer requests supplier confirmation of this exact frozen booking line.',
    }, token, booking.version)
  },

  confirm(tenantId: string, booking: Booking, note: string, token: string) {
    return command(tenantId, `/${booking.id}:confirm`, {
      acceptTerms: true,
      reason: 'Supplier confirms the current rate, availability, schedule, and frozen terms.',
      note: note || null,
    }, token, booking.version)
  },
}
