import { useState, type FormEvent } from 'react'
import { campaignApi } from '../api/campaign-client'
import type { DeliveryProof } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { formatDateTime, formatMiB, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'

type Props = {
  tenantId: string
  token: string
  proof: DeliveryProof
  busy: boolean
  canReview: boolean
  run: CampaignActionRunner
}

export function DeliveryProofCard(props: Props) {
  const proof = props.proof
  return <article className="delivery-proof-card">
    <header><span><Icon name="evidence" /></span><div><small>{humanizeCode(proof.proofType, true)}</small>
      <h3>{proof.fileName}</h3><p>Captured {formatDateTime(proof.capturedAtUtc)}</p></div>
      <span className={`status-chip ${proof.reviewedBy ? statusTone(proof.status) : 'status-warning'}`}>
        {humanizeCode(proof.status, true)}</span></header>
    <dl><div><dt>Location</dt><dd>{proof.locationDescription}</dd></div>
      <div><dt>Source</dt><dd>{proof.sourceReference}</dd></div>
      <div><dt>File</dt><dd>{proof.mediaType} · {formatMiB(proof.sizeBytes)}</dd></div>
      <div><dt>File-integrity evidence</dt><dd>SHA-256 {proof.contentSha256.slice(0, 16)}…</dd></div></dl>
    {proof.latitude !== null && proof.longitude !== null &&
      <p className="proof-location">Coordinates: {proof.latitude}, {proof.longitude}</p>}
    {proof.reviewedBy ? <div className="proof-review-result"><Icon name="shield" /><span>
      <strong>{humanizeCode(proof.status, true)}</strong><small>{proof.reviewReason}</small></span></div>
      : props.canReview && <ProofReviewForm {...props} />}
  </article>
}

function ProofReviewForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '').trim()
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    if (!reason || !(submitter instanceof HTMLButtonElement)) {
      setError('Record the delivery review reason before deciding.')
      return
    }
    setError(null)
    const approved = submitter.value === 'approve'
    void props.run(
      () => campaignApi.reviewDeliveryProof(
        props.tenantId, props.proof, approved, reason, props.token),
      approved ? 'The exact supplier delivery proof was approved.' : 'The proof was rejected and remains retained for audit.',
    )
  }
  return <form className="proof-review-form" onSubmit={submit}>
    <label className="field-group">Review reason
      <textarea name="reason" required maxLength={1000} rows={3} /></label>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div><button className="secondary-button" value="reject" disabled={props.busy}>Reject proof</button>
      <button className="primary-button" value="approve" disabled={props.busy}>Approve proof</button></div>
  </form>
}

function statusTone(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('approved')) return 'status-positive'
  if (normalized.includes('rejected')) return 'status-danger'
  return 'status-neutral'
}
