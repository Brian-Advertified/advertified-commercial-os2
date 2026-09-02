import { useState } from 'react'
import type { BookablePlanLine, Booking } from '../api/booking-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'

export function BookableLines({ items, busy, create }: {
  items: BookablePlanLine[]
  busy: boolean
  create: (line: BookablePlanLine) => Promise<void>
}) {
  const available = items.filter(item => !item.alreadyBooked)
  return <section className="booking-section" aria-labelledby="bookable-title">
    <div className="section-heading"><div><p className="eyebrow">Client-selected options</p>
      <h2 id="bookable-title">Ready to prepare</h2></div></div>
    {available.length === 0 ? <article className="detail-card booking-empty">
      <h3>No selected lines are waiting</h3>
      <p>A line appears here only after the client selects the exact proposal option.</p>
    </article> : <div className="booking-grid">{available.map(line =>
      <article className="detail-card booking-card" key={line.mediaPlanLineId}>
        <StatusHeader status="Not booked" product={line.productName} />
        <PlacementFacts item={line} />
        <div className="booking-total"><span>Client-approved total</span>
          <strong>{formatMoney(line.clientPriceMinor, line.currency)}</strong></div>
        <p className="booking-guardrail">Creating this draft does not contact or commit the supplier.</p>
        <button className="primary-button" disabled={busy} onClick={() => void create(line)}>
          Create booking draft</button>
      </article>)}</div>}
  </section>
}

export function BookingList({ tenantId, items, busy, request, confirm }: {
  tenantId: string
  items: Booking[]
  busy: boolean
  request: (booking: Booking) => Promise<void>
  confirm: (booking: Booking, note: string) => Promise<void>
}) {
  return <section className="booking-section" aria-labelledby="booking-records-title">
    <div className="section-heading"><div><p className="eyebrow">Human-controlled workflow</p>
      <h2 id="booking-records-title">Booking records</h2></div></div>
    {items.length === 0 ? <article className="detail-card booking-empty">
      <h3>No bookings in this workspace</h3>
      <p>Selected proposal lines and supplier confirmation requests will appear here.</p>
    </article> : <div className="booking-grid">{items.map(booking =>
      <BookingCard key={booking.id} tenantId={tenantId} booking={booking} busy={busy}
        request={request} confirm={confirm} />)}</div>}
  </section>
}

function BookingCard({ tenantId, booking, busy, request, confirm }: {
  tenantId: string
  booking: Booking
  busy: boolean
  request: (booking: Booking) => Promise<void>
  confirm: (booking: Booking, note: string) => Promise<void>
}) {
  const [accepted, setAccepted] = useState(false)
  const [note, setNote] = useState('')
  const buyer = booking.buyerTenantId === tenantId
  const supplier = booking.supplierTenantId === tenantId
  return <article className="detail-card booking-card">
    <StatusHeader status={statusLabel(booking.status)} product={booking.productName} />
    <PlacementFacts item={booking} />
    <BookingTotal booking={booking} buyer={buyer} />
    <BookingTerms booking={booking} />
    <BookingActions booking={booking} buyer={buyer} supplier={supplier} busy={busy}
      accepted={accepted} setAccepted={setAccepted} note={note} setNote={setNote}
      request={request} confirm={confirm} />
  </article>
}

function BookingTotal({ booking, buyer }: { booking: Booking; buyer: boolean }) {
  return <div className="booking-total"><span>{buyer
    ? 'Client-approved total' : 'Supplier amount'}</span>
    <strong>{bookingAmount(booking, buyer)}</strong></div>
}

function BookingTerms({ booking }: { booking: Booking }) {
  return <details className="booking-terms"><summary>Frozen booking terms</summary>
    <p>{booking.terms}</p>
    {booking.commercialTerms && <dl className="marketplace-facts">
      <div><dt>Rate VAT</dt><dd>{booking.vatTreatment ?? 'Not supplied'}</dd></div>
      <div><dt>Production</dt><dd>{booking.commercialTerms.productionCostMinor ?? 0}</dd></div>
      <div><dt>Installation</dt><dd>{booking.commercialTerms.installationCostMinor ?? 0}</dd></div>
      <div><dt>Conditions</dt><dd>{booking.commercialTerms.conditions.join('; ') || 'None'}</dd></div>
    </dl>}
    {booking.deliverable && <p>Deliverable: {[booking.deliverable.format,
      booking.deliverable.buyingUnit, booking.deliverable.dimensions,
      booking.deliverable.placement].filter(Boolean).join(' · ')}</p>}
  </details>
}

function BookingActions({ booking, buyer, supplier, busy, accepted, setAccepted,
  note, setNote, request, confirm }: {
  booking: Booking; buyer: boolean; supplier: boolean; busy: boolean; accepted: boolean
  setAccepted: (value: boolean) => void; note: string; setNote: (value: string) => void
  request: (booking: Booking) => Promise<void>
  confirm: (booking: Booking, note: string) => Promise<void>
}) {
  return <>{buyer && booking.status === masterDataCodes.lifecycleStatuses.draft &&
    <button className="primary-button" disabled={busy}
      onClick={() => void request(booking)}>Request supplier confirmation</button>}
  {supplier && booking.status === masterDataCodes.lifecycleStatuses.pendingSupplier &&
    <SupplierConfirmation accepted={accepted} setAccepted={setAccepted}
      note={note} setNote={setNote} busy={busy} confirm={() => confirm(booking, note)} />}
  {booking.status === masterDataCodes.lifecycleStatuses.confirmed &&
    <p className="booking-confirmed">Confirmed by both buyer workflow and supplier.</p>}</>
}

function SupplierConfirmation({ accepted, setAccepted, note, setNote, busy, confirm }: {
  accepted: boolean
  setAccepted: (value: boolean) => void
  note: string
  setNote: (value: string) => void
  busy: boolean
  confirm: () => Promise<void>
}) {
  return <div className="booking-confirmation">
    <label><input type="checkbox" checked={accepted}
      onChange={event => setAccepted(event.target.checked)} />
      I confirm the current rate, availability, schedule, and frozen terms.</label>
    <label>Supplier note <textarea value={note} maxLength={2000}
      onChange={event => setNote(event.target.value)} /></label>
    <button className="primary-button" disabled={busy || !accepted}
      onClick={() => void confirm()}>Confirm booking</button>
  </div>
}

function StatusHeader({ status, product }: { status: string; product: string }) {
  return <header className="booking-card-heading"><div><p className="eyebrow">Booking line</p>
    <h3>{product}</h3></div><span className="status-chip">{status}</span></header>
}

function PlacementFacts({ item }: { item: Pick<Booking, 'supplierName' | 'channel' |
  'geography' | 'flightStart' | 'flightEnd' | 'quantity'> }) {
  return <dl className="marketplace-facts"><div><dt>Supplier</dt><dd>{item.supplierName}</dd></div>
    <div><dt>Channel</dt><dd>{item.channel}</dd></div>
    <div><dt>Geography</dt><dd>{item.geography}</dd></div>
    <div><dt>Flight</dt><dd>{item.flightStart} – {item.flightEnd}</dd></div>
    <div><dt>Quantity</dt><dd>{item.quantity}</dd></div></dl>
}

function statusLabel(status: string) {
  if (status === masterDataCodes.lifecycleStatuses.draft) return 'Draft'
  if (status === masterDataCodes.lifecycleStatuses.pendingSupplier) return 'Supplier review'
  if (status === masterDataCodes.lifecycleStatuses.confirmed) return 'Confirmed'
  return status
}

function bookingAmount(booking: Booking, buyer: boolean) {
  const amount = buyer ? booking.clientPriceMinor : booking.supplierCostMinor
  return amount == null ? 'Not available' : formatMoney(amount, booking.currency)
}
