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

type LiveProps = CommonProps & { canOperate: boolean; bookings: Booking[] }
type ProofProps = CommonProps & { canReviewProof: boolean; bookings: Booking[] }

export function LiveDeliverySection(props: LiveProps) {
  const markets = [...new Set(props.bookings.map(item => item.geography).filter(Boolean))]
  const suppliers = [...new Set(props.bookings.map(item => item.supplierName).filter(Boolean))]
  const creativeReady = props.campaign.creative?.requirements.filter(item => item.asset !== null).length ?? 0
  const creativeTotal = props.campaign.creative?.requirements.length ?? 0
  const checklist = launchChecklist(props.campaign)
  const completeCount = checklist.filter(item => item.complete).length
  return <section id="live-stage" className="connected-launch-page">
    <div className="connected-launch-grid">
      <article className="connected-launch-status"><header><Icon name="plan" /><h2>Campaign status</h2>
        <span className={`status-chip ${props.campaign.status === masterDataCodes.lifecycleStatuses.live
          ? 'status-positive' : 'status-neutral'}`}>{humanizeCode(props.campaign.status, true)}</span></header>
        <strong>{launchStatusTitle(props.campaign)}</strong><p>{launchStatusCopy(props.campaign)}</p>
        <div className="connected-launch-action"><DeliveryAction {...props} /></div>
      </article>
      <article className="connected-launch-media"><header><Icon name="chart" /><h2>In-flight media</h2></header>
        <div className="connected-launch-media-kpis"><div><strong>{props.bookings.length}</strong><span>Booked lines</span></div>
          <div><strong>{suppliers.length}</strong><span>Suppliers</span></div>
          <div><strong>{creativeReady}/{creativeTotal}</strong><span>Creative ready</span></div></div>
        <div className="connected-launch-channel-grid">{channelCounts(props.bookings).map(([channel, count]) =>
          <div key={channel}><span>{humanizeCode(channel, true)}</span><strong>{count}</strong></div>)}</div>
      </article>
      <article className="connected-launch-markets"><header><Icon name="globe" /><h2>Active markets</h2></header>
        <strong>{markets.length}</strong><span>Retained booking geographies</span>
        <div className="connected-launch-market-list">{markets.slice(0, 10).map(item => <span key={item}>● {item}</span>)}</div>
      </article>
      <article className="connected-launch-activity"><header><Icon name="bell" /><h2>Live activity</h2></header>
        <div>{campaignActivity(props.campaign).map(item => <p key={item.label}><span className={item.done ? 'is-done' : ''}>●</span>
          <strong>{item.label}</strong><small>{item.value}</small></p>)}</div>
      </article>
    </div>
    <div className="connected-launch-lower">
      <article className="connected-launch-checklist"><header><div><Icon name="tasks" /><h2>Launch checklist</h2></div>
        <span>{completeCount} / {checklist.length} complete</span></header>
        <div className="connected-launch-progress"><i style={{ width: `${checklist.length ? completeCount / checklist.length * 100 : 0}%` }} /></div>
        <ul>{checklist.map(item => <li key={item.label} className={item.complete ? 'is-complete' : ''}>
          <span>{item.complete ? '✓' : '○'}</span><strong>{item.label}</strong><small>{item.detail}</small></li>)}</ul>
      </article>
      <article className="connected-launch-assets"><header><div><Icon name="evidence" /><h2>Creative assets</h2></div>
        <span>{creativeReady}/{creativeTotal} ready</span></header>
        {props.campaign.creative?.requirements.length ? <div>{props.campaign.creative.requirements.slice(0, 6).map(requirement => <article key={requirement.id}>
          <span className="connected-launch-asset-icon"><Icon name="proposal" /></span><div><strong>{humanizeCode(requirement.channel, true)}</strong>
            <small>{requirement.formatCode} · {requirement.asset ? 'Asset retained' : 'Awaiting asset'}</small></div></article>)}</div>
          : <p className="connected-launch-empty">No creative requirements are retained yet.</p>}
      </article>
      <article className="connected-launch-tasks"><header><div><Icon name="tasks" /><h2>Launch tasks</h2></div></header>
        <ul>{props.bookings.slice(0, 6).map(item => <li key={item.id}><span>{item.status === masterDataCodes.lifecycleStatuses.confirmed ? '✓' : '○'}</span>
          <div><strong>{item.productName}</strong><small>{item.supplierName} · {humanizeCode(item.status, true)}</small></div></li>)}</ul>
      </article>
    </div>
    <footer className="connected-launch-banner"><Icon name="chart" /><div><strong>Real campaigns. Real audiences. Real results.</strong>
      <span>Campaign delivery stays tied to retained booking, creative, proof and measurement evidence.</span></div></footer>
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

function launchStatusTitle(campaign: Campaign) {
  if (campaign.status === masterDataCodes.lifecycleStatuses.live) return 'Campaign is live'
  if (campaign.status === masterDataCodes.lifecycleStatuses.ready) return 'Ready to launch'
  if (campaign.status === masterDataCodes.lifecycleStatuses.completed) return 'Campaign completed'
  return 'Preparing for launch'
}

function launchStatusCopy(campaign: Campaign) {
  if (campaign.startedAtUtc) return `Launch recorded ${formatDateTime(campaign.startedAtUtc)}.`
  return 'Launch remains human-controlled and can begin only after booking and creative readiness are retained.'
}

function launchChecklist(campaign: Campaign) {
  const creative = campaign.creative?.requirements ?? []
  return [
    { label: 'Funding confirmed', complete: Boolean(campaign.paymentIntentId), detail: humanizeCode(campaign.fundingStatus, true) },
    { label: 'Supplier bookings confirmed', complete: campaign.requiredBookingCount > 0 &&
      campaign.confirmedBookingCount === campaign.requiredBookingCount,
      detail: `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount} confirmed` },
    { label: 'Creative assets approved', complete: Boolean(campaign.creativeApprovedAtUtc),
      detail: creative.length ? `${creative.filter(item => item.asset).length}/${creative.length} assets retained` : 'No requirements retained' },
    { label: 'Campaign start recorded', complete: Boolean(campaign.startedAtUtc),
      detail: campaign.startedAtUtc ? formatDateTime(campaign.startedAtUtc) : 'Not recorded' },
    { label: 'Delivery proof retained', complete: campaign.deliveryProofs.some(item =>
      item.status === masterDataCodes.lifecycleStatuses.approved),
      detail: `${campaign.deliveryProofs.length} proof record${campaign.deliveryProofs.length === 1 ? '' : 's'}` },
    { label: 'Measurement report approved', complete: campaign.measurementReports.some(item =>
      item.status === masterDataCodes.lifecycleStatuses.approved),
      detail: `${campaign.measurementReports.length} report version${campaign.measurementReports.length === 1 ? '' : 's'}` },
  ]
}

function channelCounts(bookings: Booking[]) {
  const counts = new Map<string, number>()
  bookings.forEach(item => counts.set(item.channel, (counts.get(item.channel) ?? 0) + 1))
  return [...counts.entries()].sort((a, b) => b[1] - a[1])
}

function campaignActivity(campaign: Campaign) {
  return [
    { label: 'Bookings confirmed', value: campaign.bookingsConfirmedAtUtc
      ? formatDateTime(campaign.bookingsConfirmedAtUtc) : 'Pending', done: Boolean(campaign.bookingsConfirmedAtUtc) },
    { label: 'Creative approved', value: campaign.creativeApprovedAtUtc
      ? formatDateTime(campaign.creativeApprovedAtUtc) : 'Pending', done: Boolean(campaign.creativeApprovedAtUtc) },
    { label: 'Campaign started', value: campaign.startedAtUtc
      ? formatDateTime(campaign.startedAtUtc) : 'Pending', done: Boolean(campaign.startedAtUtc) },
    { label: 'Campaign completed', value: campaign.completedAtUtc
      ? formatDateTime(campaign.completedAtUtc) : 'Pending', done: Boolean(campaign.completedAtUtc) },
    { label: 'Proof requested', value: campaign.proofRequestedAtUtc
      ? formatDateTime(campaign.proofRequestedAtUtc) : 'Pending', done: Boolean(campaign.proofRequestedAtUtc) },
  ]
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
