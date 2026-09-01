import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import { campaignReasonSchema, type Campaign } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
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
  return <section id="booking-stage" className="campaign-workspace-section">
    <SectionHeading eyebrow="Supplier commitment" title="Booking coverage"
      copy="Every media-plan line must have an exact confirmed supplier booking before production can begin."
      status={complete ? 'Complete' : `${props.campaign.confirmedBookingCount}/${props.campaign.requiredBookingCount} confirmed`} />
    <div className="campaign-booking-grid">{props.bookings.map(booking =>
      <BookingSummary key={booking.id} booking={booking} />)}</div>
    {props.bookings.length === 0 && <EmptyBookingState />}
    {!complete && <BookingNextAction {...props} />}
  </section>
}

function BookingSummary({ booking }: { booking: Booking }) {
  return <article className="campaign-booking-card"><header><span><Icon name="reservation" /></span>
    <div><small>{humanizeCode(booking.channel, true)}</small><h3>{booking.productName}</h3></div>
    <span className={`status-chip ${booking.status === masterDataCodes.lifecycleStatuses.confirmed
      ? 'status-positive' : 'status-warning'}`}>{humanizeCode(booking.status, true)}</span></header>
    <dl><div><dt>Supplier</dt><dd>{booking.supplierName}</dd></div>
      <div><dt>Geography</dt><dd>{booking.geography}</dd></div>
      <div><dt>Flight</dt><dd>{formatDate(booking.flightStart)} – {formatDate(booking.flightEnd)}</dd></div>
      <div><dt>Client-approved total</dt><dd>{booking.clientPriceMinor === null
        ? 'Not available' : formatMoney(booking.clientPriceMinor, booking.currency)}</dd></div></dl>
  </article>
}

function EmptyBookingState() {
  return <article className="campaign-section-empty"><Icon name="reservation" /><div>
    <h3>No Booking records are linked yet</h3>
    <p>Create and confirm every exact selected media line before confirming campaign readiness.</p>
    <Link className="secondary-button" to="/bookings">Open Bookings</Link></div></article>
}

function BookingNextAction(props: Props) {
  const ready = props.campaign.requiredBookingCount > 0 &&
    props.campaign.requiredBookingCount === props.campaign.confirmedBookingCount
  if (!ready) return <article className="campaign-next-action"><div><p className="eyebrow">Next action</p>
    <h3>Complete supplier confirmation</h3><p>Missing, pending or changed lines must be resolved in Bookings.</p></div>
    <Link className="primary-button" to="/bookings">Open Bookings</Link></article>
  if (!props.canConfirm) return <article className="campaign-next-action">
    <div><p className="eyebrow">Waiting for authorised confirmation</p>
      <h3>All lines are confirmed</h3><p>An assigned campaign operator must confirm that the coverage is complete.</p></div></article>
  return <BookingConfirmationForm {...props} />
}

function BookingConfirmationForm(props: Props) {
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
  return <form className="campaign-next-action campaign-action-form" onSubmit={submit}>
    <div><p className="eyebrow">Confirm booking readiness</p><h3>All selected lines are confirmed</h3>
      <label className="field-group">Confirmation reason
        <textarea name="reason" required maxLength={1000} rows={3} /></label>
      {error && <p className="inline-alert" role="alert">{error}</p>}</div>
    <button className="primary-button" disabled={props.busy}>Confirm booking coverage</button>
  </form>
}

function SectionHeading({ eyebrow, title, copy, status }: {
  eyebrow: string
  title: string
  copy: string
  status: string
}) {
  return <header className="campaign-section-heading"><div><p className="eyebrow">{eyebrow}</p>
    <h2>{title}</h2><p>{copy}</p></div><span className="status-chip status-neutral">{status}</span></header>
}
