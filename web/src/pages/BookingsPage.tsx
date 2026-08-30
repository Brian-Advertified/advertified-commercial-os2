import { Navigate } from 'react-router-dom'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { bookingApi } from '../api/booking-client'
import { LoadingState, MessageState } from '../components/PageState'
import { BookableLines, BookingList } from '../booking/BookingRecords'
import { bookingBuyerRoles, bookingViewerRoles } from '../booking/booking-roles'
import { useBookingWorkspace } from '../booking/useBookingWorkspace'

const defaultTerms = 'This draft is limited to the exact client-selected proposal line. ' +
  'The supplier must separately confirm the current rate, availability, schedule, and terms.'

export function BookingsPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!bookingViewerRoles.has(selected.roleCode)) return <MessageState
    title="Bookings are not available"
    message="This workspace role cannot view or act on bookings." />
  return <BookingWorkspace key={selected.tenantId} tenantId={selected.tenantId}
    canBuy={bookingBuyerRoles.has(selected.roleCode)} token={session!.antiforgeryToken} />
}

function BookingWorkspace({ tenantId, canBuy, token }: {
  tenantId: string
  canBuy: boolean
  token: string
}) {
  const model = useBookingWorkspace(tenantId, canBuy)
  if (model.error && !model.bookings) return <MessageState
    title="Bookings could not be loaded" message={model.error} />
  if (!model.bookings) return <LoadingState label="Loading bookings" />
  return <section aria-labelledby="bookings-title">
    <header className="page-heading page-heading-split"><div><p className="eyebrow">
      Selected option to supplier confirmation</p><h1 id="bookings-title">Bookings</h1>
      <p>Every step is explicit. Drafting does not contact a supplier, and only the assigned
        supplier can confirm the frozen booking line.</p></div>
      <span className="status-chip">{model.bookings.length} records</span></header>
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    {canBuy && <BookableLines items={model.bookable} busy={model.busy}
      create={(line) => model.run(
        () => bookingApi.create(tenantId, line, defaultTerms, token),
        'Booking draft created. No supplier commitment has been made.')} />}
    <BookingList tenantId={tenantId} items={model.bookings} busy={model.busy}
      request={(booking) => model.run(
        () => bookingApi.requestConfirmation(tenantId, booking, token),
        'The exact booking line is ready for supplier confirmation.')}
      confirm={(booking, note) => model.run(
        () => bookingApi.confirm(tenantId, booking, note, token),
        'The supplier confirmed this exact booking line.')} />
  </section>
}
