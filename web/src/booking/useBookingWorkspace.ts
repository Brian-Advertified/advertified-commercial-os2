import { useCallback, useEffect, useState } from 'react'
import { bookingApi } from '../api/booking-client'
import type { BookablePlanLine, Booking } from '../api/booking-schemas'
import { humanMessage } from '../api/client'
import { notifications } from '../notifications/notifications'

export function useBookingWorkspace(tenantId: string, canBuy: boolean) {
  const [bookings, setBookings] = useState<Booking[] | null>(null)
  const [bookable, setBookable] = useState<BookablePlanLine[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(async () => {
    try {
      const [items, lines] = await fetchBookingWorkspace(tenantId, canBuy)
      setBookings(items); setBookable(lines); setError(null)
    } catch (failure) { setError(humanMessage(failure)) }
  }, [canBuy, tenantId])
  useEffect(() => {
    let active = true
    void fetchBookingWorkspace(tenantId, canBuy).then(([items, lines]) => {
      if (!active) return
      setBookings(items); setBookable(lines); setError(null)
    }, (failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [canBuy, tenantId])

  const run = useCallback(async (action: () => Promise<unknown>, message: string) => {
    setBusy(true); setError(null)
    try { await action(); await load(); notifications.success(message) }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }, [load])
  return { bookings, bookable, busy, error, run }
}

function fetchBookingWorkspace(tenantId: string, canBuy: boolean) {
  return Promise.all([
    bookingApi.list(tenantId),
    canBuy ? bookingApi.listBookableLines(tenantId) : Promise.resolve([]),
  ])
}
