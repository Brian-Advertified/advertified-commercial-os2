import { useState, type FormEvent } from 'react'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import {
  campaignReasonSchema,
  completionReasonSchema,
  type Campaign,
} from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'
import { DeliveryProofCard } from './DeliveryProofCard'

type CommonProps = {
  tenantId: string
  token: string
  campaign: Campaign
  busy: boolean
  run: CampaignActionRunner
}

type LiveProps = CommonProps & { canOperate: boolean }
type ProofProps = CommonProps & { canReviewProof: boolean; bookings: Booking[] }

export function LiveDeliverySection(props: LiveProps) {
  return <section id="live-stage" className="campaign-workspace-section">
    <DeliveryHeading campaign={props.campaign} />
    <DeliveryMilestones campaign={props.campaign} />
    <DeliveryAction {...props} />
  </section>
}

export function DeliveryProofSection(props: ProofProps) {
  const available = props.campaign.status === masterDataCodes.lifecycleStatuses.completed
  return <section id="proof-stage"
    className="campaign-workspace-section delivery-proof-workspace">
    <header><div><p className="eyebrow">Delivery proof</p>
      <h2>Evidence linked to the exact booked media line</h2>
      <p>{props.campaign.proofRequestReason ??
        'Supplier proof is requested when the campaign is completed.'}</p></div>
      <span className="status-chip status-neutral">
        {props.campaign.deliveryProofs.length} submitted
      </span></header>
    <DeliveryProofCoverage campaign={props.campaign} bookings={props.bookings} />
    {!available ? <LockedProof /> : <ProofRecords {...props} />}
  </section>
}

function DeliveryProofCoverage({ campaign, bookings }: {
  campaign: Campaign
  bookings: Booking[]
}) {
  const approved = campaign.deliveryProofs.filter(item =>
    item.status === masterDataCodes.lifecycleStatuses.approved)
  const submitted = campaign.deliveryProofs.filter(item =>
    item.status === masterDataCodes.lifecycleStatuses.submitted)
  const rejected = campaign.deliveryProofs.filter(item =>
    item.status === masterDataCodes.lifecycleStatuses.rejected)
  const approvedBookings = new Set(approved.map(item => item.bookingId))
  const proven = bookings.filter(item => approvedBookings.has(item.id))
  const missing = bookings.filter(item => !approvedBookings.has(item.id))
  return <section className="delivery-proof-coverage" aria-labelledby="proof-coverage-title">
    <header><div><p className="eyebrow">Delivery accountability</p>
      <h3 id="proof-coverage-title">How much of the booked campaign is actually proven?</h3>
      <p>{proofCoverageSentence(bookings.length, proven.length, submitted.length, rejected.length)}</p></div>
      <span className={`status-chip ${bookings.length > 0 && proven.length === bookings.length ? 'status-positive' : 'status-warning'}`}>
        {proven.length}/{bookings.length} bookings evidenced</span></header>
    <div className="delivery-proof-metrics">
      <ProofMetric label="Booked media lines" value={bookings.length} detail="Commercial commitments requiring evidence" />
      <ProofMetric label="Approved proof" value={approved.length} detail="Reviewed delivery evidence accepted" />
      <ProofMetric label="Awaiting review" value={submitted.length} detail="Submitted proof not yet accepted" />
      <ProofMetric label="Rejected proof" value={rejected.length} detail="Evidence requiring correction or replacement" />
    </div>
    {missing.length > 0 && <details className="delivery-proof-gaps"><summary>
      {missing.length} booking{missing.length === 1 ? '' : 's'} still lack approved delivery evidence
    </summary><ul>{missing.map(item => <li key={item.id}>
      <strong>{item.productName}</strong><span>{item.supplierName} · {humanizeCode(item.channel, true)}</span>
    </li>)}</ul></details>}
    <footer>Campaign completion records that the booked flight ended. Only approved proof establishes retained delivery evidence for a booking.</footer>
  </section>
}

function ProofMetric({ label, value, detail }: { label: string; value: number; detail: string }) {
  return <article><small>{label}</small><strong>{value}</strong><p>{detail}</p></article>
}

function proofCoverageSentence(bookings: number, proven: number, submitted: number, rejected: number) {
  if (bookings === 0) return 'No booked media lines are loaded for this campaign, so proof coverage cannot be assessed.'
  if (proven === bookings) return `Every one of the ${bookings} booked media line${bookings === 1 ? '' : 's'} has approved delivery evidence.`
  const pending = [
    submitted > 0 ? `${submitted} proof submission${submitted === 1 ? '' : 's'} await review` : null,
    rejected > 0 ? `${rejected} proof submission${rejected === 1 ? '' : 's'} were rejected` : null,
  ].filter(Boolean)
  return `${proven} of ${bookings} booked media line${bookings === 1 ? '' : 's'} currently have approved proof${pending.length ? `; ${pending.join(' and ')}` : ''}.`
}

function DeliveryHeading({ campaign }: { campaign: Campaign }) {
  return <header className="campaign-section-heading"><div><p className="eyebrow">Launch and completion</p>
    <h2>Human-controlled delivery</h2><p>A process heartbeat never starts or completes a campaign. The authorised operator records each consequential transition.</p></div>
    <span className="status-chip status-neutral">{humanizeCode(campaign.status, true)}</span></header>
}

function DeliveryMilestones({ campaign }: { campaign: Campaign }) {
  const milestones = [
    { label: 'Creative ready', value: campaign.creativeApprovedAtUtc },
    { label: 'Campaign started', value: campaign.startedAtUtc },
    { label: 'Campaign completed', value: campaign.completedAtUtc },
    { label: 'Proof requested', value: campaign.proofRequestedAtUtc },
  ]
  return <div className="delivery-milestones">{milestones.map(item => <div key={item.label}>
    <span>{item.value ? '✓' : '○'}</span><div><strong>{item.label}</strong>
      <small>{item.value ? formatDateTime(item.value) : 'Not recorded'}</small></div></div>)}</div>
}

function DeliveryAction(props: LiveProps) {
  const status = props.campaign.status
  if (status === masterDataCodes.lifecycleStatuses.ready) {
    return props.canOperate ? <StartCampaignForm {...props} /> : <WaitingAction
      title="Campaign is ready to launch" copy="An authorised campaign operator must record the launch when the booked delivery window begins." />
  }
  if (status === masterDataCodes.lifecycleStatuses.live) {
    return props.canOperate ? <CompleteCampaignForm {...props} /> : <WaitingAction
      title="Campaign is live" copy="An authorised campaign operator records completion after the booked delivery window closes." />
  }
  if (status === masterDataCodes.lifecycleStatuses.completed) {
    return <WaitingAction title="Campaign delivery is complete"
      copy="The booked delivery window is closed and the retained supplier proof request is now in review." />
  }
  return <WaitingAction title="Live delivery is not ready"
    copy="Booking and creative readiness must be completed before an authorised operator can start the campaign." />
}

function StartCampaignForm(props: LiveProps) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = campaignReasonSchema.safeParse({
      reason: new FormData(event.currentTarget).get('reason'),
    })
    if (!parsed.success) {
      setError('Record why the campaign is ready to start now.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.start(
        props.tenantId, props.campaign, parsed.data.reason, props.token),
      'The campaign launch was recorded against the exact ready version.',
    )
  }
  return <form className="campaign-next-action campaign-action-form" onSubmit={submit}>
    <div><p className="eyebrow">Ready to launch</p><h3>Record the campaign start</h3>
      <label className="field-group">Launch reason
        <textarea name="reason" required maxLength={1000} rows={3} /></label>
      {error && <p className="inline-alert" role="alert">{error}</p>}</div>
    <button className="primary-button" disabled={props.busy}>Start campaign</button>
  </form>
}

function CompleteCampaignForm(props: LiveProps) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const parsed = completionReasonSchema.safeParse({
      completionReason: values.get('completionReason'),
      proofRequestReason: values.get('proofRequestReason'),
    })
    if (!parsed.success) {
      setError('Record both the completion and supplier proof-request reasons.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.complete(
        props.tenantId, props.campaign, parsed.data.completionReason,
        parsed.data.proofRequestReason, props.token),
      'The campaign was completed and exact supplier proof was requested.',
    )
  }
  return <form className="campaign-next-action campaign-action-form" onSubmit={submit}>
    <div><p className="eyebrow">Close the delivery window</p><h3>Record completion and request proof</h3>
      <div className="campaign-action-fields"><label className="field-group">Completion reason
        <textarea name="completionReason" required maxLength={1000} rows={3} /></label>
        <label className="field-group">Supplier proof request
          <textarea name="proofRequestReason" required maxLength={1000} rows={3} /></label></div>
      {error && <p className="inline-alert" role="alert">{error}</p>}</div>
    <button className="primary-button" disabled={props.busy}>Complete and request proof</button>
  </form>
}

function ProofRecords(props: ProofProps) {
  if (props.campaign.deliveryProofs.length === 0) return <article className="campaign-section-empty">
    <Icon name="evidence" /><div><h3>Waiting for supplier proof</h3>
      <p>Each supplier must submit immutable evidence captured inside its booked flight window.</p></div></article>
  return <div className="delivery-proof-grid">{props.campaign.deliveryProofs.map(proof =>
    <DeliveryProofCard key={proof.id} tenantId={props.tenantId} token={props.token}
      proof={proof} busy={props.busy} canReview={props.canReviewProof} run={props.run} />)}</div>
}

function LockedProof() {
  return <article className="campaign-section-empty"><Icon name="evidence" /><div>
    <h3>Proof opens after delivery completes</h3>
    <p>The authorised operator must close the delivery window and record the supplier proof request first.</p>
  </div></article>
}

function WaitingAction({ title, copy }: { title: string; copy: string }) {
  return <article className="campaign-next-action"><div><p className="eyebrow">Waiting for authorised action</p>
    <h3>{title}</h3><p>{copy}</p></div></article>
}
