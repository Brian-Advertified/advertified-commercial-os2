import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import { campaignReasonSchema, type Campaign } from '../api/campaign-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDate, formatMoney, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'

type Props = {
  tenantId: string
  token: string
  campaign: Campaign
  bookings: Booking[]
  canConfirm: boolean
  busy: boolean
  run: CampaignActionRunner
}

export function BookingReadinessSection(props: Props) {
  const complete = props.campaign.status !== masterDataCodes.lifecycleStatuses.planned
  const confirmed = props.bookings.filter(item => item.status === masterDataCodes.lifecycleStatuses.confirmed)
  const pending = props.bookings.filter(item => item.status !== masterDataCodes.lifecycleStatuses.confirmed)
  const campaignValue = props.bookings.reduce((sum, item) => sum + (item.clientPriceMinor ?? 0), 0)
  const currency = props.bookings.find(item => item.currency)?.currency ?? masterDataCodes.currencies.zar

  return <section id="booking-stage" className="connected-booking-page">
    <div className="connected-booking-layout">
      <main className="connected-booking-main">
        <header className="connected-booking-tabs">
          <button type="button" className="is-active">Inventory &amp; Approvals ({props.bookings.length})</button>
          <button type="button" disabled>Supplier Confirmations ({confirmed.length})</button>
          <button type="button" disabled>Creative Approvals ({props.campaign.creative?.requirements.length ?? 0})</button>
          <button type="button" disabled>Payments</button>
        </header>
        {props.bookings.length ? <div className="connected-booking-table">
          <div className="connected-booking-head"><span>Inventory / Location</span><span>Supplier</span>
            <span>Dates</span><span>Status</span><span>Next step</span></div>
          {props.bookings.map(booking => <BookingRow key={booking.id} booking={booking} />)}
        </div> : <EmptyBookingState />}
        <footer className="connected-booking-footer"><span>{confirmed.length} confirmed · {pending.length} need attention</span>
          <div><Link className="secondary-button" to="/bookings">Open all Bookings</Link>
            {!complete && <BookingNextAction {...props} />}</div></footer>
      </main>
      <aside className="connected-booking-summary">
        <article><header><h2>Booking Summary</h2><p>An overview of selected campaign inventory.</p></header>
          <dl><div><dt>Total items</dt><dd>{props.bookings.length}</dd></div>
            <div><dt>Confirmed</dt><dd>{confirmed.length}</dd></div>
            <div><dt>Pending / review</dt><dd>{pending.length}</dd></div></dl>
          <hr /><small>Estimated campaign value</small>
          <strong>{campaignValue > 0 ? formatMoney(campaignValue, currency, 0) : 'Not established'}</strong>
          <p>Final pricing remains subject to the retained booking and supplier-confirmation evidence.</p></article>
        <article><header><h2>Key Dates</h2></header>
          <dl><div><dt>Campaign start</dt><dd>{formatDate(props.campaign.startDate)}</dd></div>
            <div><dt>Campaign end</dt><dd>{formatDate(props.campaign.endDate)}</dd></div>
            <div><dt>Bookings confirmed</dt><dd>{props.campaign.bookingsConfirmedAtUtc
              ? formatDate(props.campaign.bookingsConfirmedAtUtc) : 'Pending'}</dd></div>
            <div><dt>Creative approved</dt><dd>{props.campaign.creativeApprovedAtUtc
              ? formatDate(props.campaign.creativeApprovedAtUtc) : 'Pending'}</dd></div></dl></article>
        <article className="connected-sa-proof connected-booking-sa"><span className="connected-sa-flag">🇿🇦</span><div>
          <strong>Built for South Africa</strong><p>Real media. Trusted suppliers. Governed booking evidence.</p></div></article>
      </aside>
    </div>
  </section>
}

function BookingRow({ booking }: { booking: Booking }) {
  const confirmed = booking.status === masterDataCodes.lifecycleStatuses.confirmed
  return <div className="connected-booking-row">
    <span className="connected-booking-inventory"><MediaTypeIcon channel={booking.channel} /><div>
      <strong>{booking.productName}</strong><small>{booking.geography}</small></div></span>
    <span>{booking.supplierName}</span>
    <span>{formatDate(booking.flightStart)} – {formatDate(booking.flightEnd)}</span>
    <span><em className={confirmed ? 'is-confirmed' : 'is-pending'}>● {humanizeCode(booking.status, true)}</em></span>
    <span>{confirmed ? 'Ready for creative / delivery' : booking.requestedAtUtc ? 'Await supplier confirmation' : 'Request supplier confirmation'}</span>
  </div>
}

function EmptyBookingState() {
  return <article className="campaign-section-empty"><div>
    <h3>No Booking records are linked yet</h3>
    <p>Create and confirm every exact selected media line before confirming campaign readiness.</p>
    <Link className="secondary-button" to="/bookings">Open Bookings</Link></div></article>
}

function BookingNextAction(props: Props) {
  const ready = props.campaign.requiredBookingCount > 0 &&
    props.campaign.requiredBookingCount === props.campaign.confirmedBookingCount
  if (!ready) return null
  if (!props.canConfirm) return <span className="connected-booking-wait">Waiting for authorised confirmation</span>
  return <BookingConfirmationForm {...props} />
}

function BookingConfirmationForm(props: Props) {
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = campaignReasonSchema.safeParse({
      reason: new FormData(event.currentTarget).get('reason'),
    })
    if (!parsed.success) {
      setError('Explain why every selected media line is ready for production.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.confirmBookings(
        props.tenantId, props.campaign, parsed.data.reason, props.token),
      'Booking coverage was confirmed for the exact client-selected option.',
    )
  }
  if (!open) return <button className="primary-button" type="button" disabled={props.busy}
    onClick={() => setOpen(true)}>Confirm booking coverage</button>
  return <form className="connected-booking-confirm" onSubmit={submit}>
    <label>Confirmation reason<textarea name="reason" required maxLength={1000} rows={2} /></label>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div><button className="secondary-button" type="button" onClick={() => setOpen(false)}>Cancel</button>
      <button className="primary-button" disabled={props.busy}>Confirm</button></div>
  </form>
}
