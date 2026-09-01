import { useState, type FormEvent } from 'react'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import {
  creativeRequestInputSchema,
  type Campaign,
  type CreativeRequestInput,
} from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'
import { CreativeAssetCard } from './CreativeAssetCard'

type Props = {
  tenantId: string
  token: string
  campaign: Campaign
  bookings: Booking[]
  busy: boolean
  canRequest: boolean
  canUpload: boolean
  canBrandReview: boolean
  canApprove: boolean
  run: CampaignActionRunner
}

export function CreativeSection(props: Props) {
  const status = props.campaign.status
  const unlocked = status !== masterDataCodes.lifecycleStatuses.planned
  return <section id="creative-stage" className="campaign-workspace-section">
    <CreativeHeading campaign={props.campaign} />
    {!unlocked && <LockedCreative />}
    {status === masterDataCodes.lifecycleStatuses.booked && !props.campaign.creative &&
      <CreativeRequestBoundary {...props} />}
    {props.campaign.creative && <CreativeRequirements {...props} />}
    {status === masterDataCodes.lifecycleStatuses.creativePending &&
      props.campaign.creative?.readyForApproval && props.canApprove &&
      <CreativeApprovalForm {...props} />}
  </section>
}

function CreativeHeading({ campaign }: { campaign: Campaign }) {
  const requirements = campaign.creative?.requirements.length ?? 0
  const ready = campaign.creative?.readyForApproval ?? false
  return <header className="campaign-section-heading"><div><p className="eyebrow">Production readiness</p>
    <h2>Booked-format creative</h2>
    <p>Each confirmed booking receives an exact format requirement, versioned file and separate buyer and supplier review.</p></div>
    <span className={`status-chip ${ready ? 'status-positive' : 'status-neutral'}`}>
      {requirements === 0 ? 'Not requested' : `${requirements} requirements`}</span></header>
}

function LockedCreative() {
  return <article className="campaign-section-empty"><Icon name="proposal" /><div>
    <h3>Creative starts after Booking coverage is confirmed</h3>
    <p>Production requirements cannot be created from provisional or partially confirmed media lines.</p></div></article>
}

function CreativeRequestBoundary(props: Props) {
  if (!props.canRequest) return <article className="campaign-next-action"><div>
    <p className="eyebrow">Waiting for production requirements</p><h3>Bookings are confirmed</h3>
    <p>An authorised campaign operator must define the exact file required for every booked line.</p></div></article>
  return <CreativeRequestForm {...props} />
}

function CreativeRequestForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const input = creativeInput(new FormData(event.currentTarget), props.bookings)
    const parsed = creativeRequestInputSchema.safeParse(input)
    if (!parsed.success || parsed.data.requirements.length !== props.bookings.length) {
      setError('Complete one valid production requirement for every confirmed Booking.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.requestCreative(
        props.tenantId, props.campaign, parsed.data, props.token),
      'Booked-format creative requirements were created for every confirmed media line.',
    )
  }
  return <form className="creative-request-form" onSubmit={submit}>
    <header><div><p className="eyebrow">Define production requirements</p>
      <h3>One exact requirement for each Booking</h3><p>Do not use a proposal concept as final artwork. Enter the supplier-approved technical requirement.</p></div></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className="creative-request-grid">{props.bookings.map(booking =>
      <CreativeRequirementFields booking={booking} key={booking.id} />)}</div>
    <label className="field-group">Why production is being requested
      <textarea name="requestReason" required maxLength={1000} rows={3}
        defaultValue="All exact client-selected media lines are booked and ready for production artwork." /></label>
    <footer><span><Icon name="shield" /> Requirements are locked to these exact Booking versions.</span>
      <button className="primary-button" disabled={props.busy}>Request production creative</button></footer>
  </form>
}

function CreativeRequirementFields({ booking }: { booking: Booking }) {
  const prefix = booking.id
  return <fieldset className="creative-request-card"><legend>{booking.productName}</legend>
    <p>{humanizeCode(booking.channel, true)} · {booking.supplierName}</p>
    <div><label className="field-group">Format code
      <input name={`${prefix}:formatCode`} required maxLength={100} placeholder="Supplier format code" /></label>
      <label className="field-group">Required file type
        <select name={`${prefix}:mediaType`} required defaultValue="">
          <option value="" disabled>Choose file type</option>
          <option value="image/jpeg">JPEG image</option>
          <option value="image/png">PNG image</option>
          <option value="application/pdf">PDF artwork</option>
        </select></label>
      <label className="field-group">Width
        <input name={`${prefix}:width`} type="number" required min="1" max="100000" /></label>
      <label className="field-group">Height
        <input name={`${prefix}:height`} type="number" required min="1" max="100000" /></label>
      <label className="field-group">Maximum size (MiB)
        <input name={`${prefix}:maximumMiB`} type="number" required min="0.1" max="100" step="0.1" /></label></div>
    <label className="field-group">Supplier instructions
      <textarea name={`${prefix}:instructions`} required maxLength={5000} rows={3} /></label>
  </fieldset>
}

function CreativeRequirements(props: Props) {
  return <div className="creative-requirement-grid">{props.campaign.creative!.requirements.map(requirement =>
    <CreativeAssetCard key={requirement.id} tenantId={props.tenantId} token={props.token}
      campaign={props.campaign} requirement={requirement} busy={props.busy}
      canUpload={props.canUpload} canBrandReview={props.canBrandReview} run={props.run} />)}</div>
}

function CreativeApprovalForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '').trim()
    if (!reason) {
      setError('Explain why every current creative version is ready for campaign delivery.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.approveCreative(
        props.tenantId, props.campaign, reason, props.token),
      'The current booked-format creative was approved and the campaign is ready to launch.',
    )
  }
  return <form className="campaign-next-action campaign-action-form" onSubmit={submit}>
    <div><p className="eyebrow">Client readiness approval</p><h3>Every production file passed its current reviews</h3>
      <label className="field-group">Approval reason
        <textarea name="reason" required maxLength={1000} rows={3} /></label>
      {error && <p className="inline-alert" role="alert">{error}</p>}</div>
    <button className="primary-button" disabled={props.busy}>Approve creative readiness</button>
  </form>
}

function creativeInput(values: FormData, bookings: Booking[]): CreativeRequestInput {
  return {
    reason: String(values.get('requestReason') ?? ''),
    requirements: bookings.map(booking => ({
      bookingId: booking.id,
      formatCode: String(values.get(`${booking.id}:formatCode`) ?? ''),
      width: Number(values.get(`${booking.id}:width`)),
      height: Number(values.get(`${booking.id}:height`)),
      requiredMediaType: String(values.get(`${booking.id}:mediaType`) ?? ''),
      maximumBytes: Math.round(Number(values.get(`${booking.id}:maximumMiB`)) * 1024 * 1024),
      instructions: String(values.get(`${booking.id}:instructions`) ?? ''),
    })),
  }
}
